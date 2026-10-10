using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod content registration surface over the real mod stack: definitions
/// are registered per-mod through <see cref="IModContext.Content"/>, the
/// definition carries its own id/kind/schema version, invalid or duplicate
/// registrations are refused, RegisterContent is enforced, the plugin-facing
/// <see cref="IModContentControl"/> aggregates every mod's entries, and the
/// registry keeps the instance the mod handed over.
/// </summary>
[Trait("Category", "Integration")]
public class ModContentTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestContentMod ContentMod(TestNode node) =>
		(TestContentMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestContentMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestEchoMod);

	[Fact]
	public void BindRegistersContent_ContextExposesIt()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		var mod = ContentMod(guest);
		Assert.True(mod.Registered);
		Assert.True(mod.Context!.Content.CanRegister);
		Assert.True(mod.Context.Content.IsRegistered("wooden.sword"));
		Assert.Contains("healing.recipe", mod.Context.Content.Definitions.Select(d => d.Id));
		Assert.Equal(2, mod.Context.Content.Count);

		var sword = mod.Context.Content.Definitions.Single(d => d.Id == "wooden.sword");
		Assert.Equal(ModContentKind.Item, sword.Kind);
		Assert.IsType<ModItemDefinition>(sword);
	}

	[Fact]
	public void SchemaVersion_IsStoredAndRead()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = ContentMod(host);
		var sword = mod.Context!.Content.Definitions.Single(d => d.Id == "wooden.sword");
		var recipe = mod.Context.Content.Definitions.Single(d => d.Id == "healing.recipe");

		Assert.Equal(2, sword.SchemaVersion);
		Assert.Equal(1, recipe.SchemaVersion);
	}

	[Fact]
	public void InvalidSchemaVersion_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;
		var originalCount = content.Count;

		Assert.False(content.TryRegister(new StubContentDefinition("bad.schema", schemaVersion: 0)));
		Assert.False(content.TryRegister(new StubContentDefinition("bad.schema", schemaVersion: -1)));
		Assert.False(content.IsRegistered("bad.schema"));
		Assert.Equal(originalCount, content.Count);
	}

	[Fact]
	public void HandshakeList_CarriesTheFingerprintOfEachModsOwnContent()
	{
		// The session's handshake list is what fills HandshakeMsg.Mods, and it is a leaf that
		// composes discovery with the content registry: the wiring the peer comparison rests
		// on is pinned HERE, over the real mod stack, rather than assumed from the two halves'
		// own tests — a list that never carried the fingerprint would leave every peer
		// comparison reading "no content" on both sides.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var fingerprints = host.Services.GetRequiredService<IModContentFingerprints>();

		var infos = host.Services.GetRequiredService<IModListProvider>().CurrentModInfos();
		var contentMod = infos.Single(info => info.Id == "test.content");

		Assert.Equal(fingerprints.ByMod["test.content"], contentMod.ContentFingerprint);
		Assert.NotNull(contentMod.ContentFingerprint);

		// A mod that registered nothing reports "none", never an unknown value: the host reads
		// it as the empty content set, which is what the mod actually materializes.
		Assert.Null(infos.Single(info => info.Id == "test.echo").ContentFingerprint);
	}

	[Fact]
	public void AModThatFailsToLoad_LeavesNoContentBehind()
	{
		// A mod registers its content BEFORE it can fail (a code registration happens inside
		// `Bind`, and the attribute scan runs even earlier), so the entries outlive the mod
		// unless the failed load withdraws them. They must not outlive it: the framework-wide
		// view is what the console, the ownership query and the content fingerprints read, and
		// a digest claiming content this process never materialized would make the handshake
		// refuse a peer over what is really a local load failure — one-sidedly, since only the
		// failing peer's set carries the ghost entry.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mods = host.Services.GetRequiredService<ModService>();

		Assert.DoesNotContain(mods.LoadedMods, mod => mod is TestBindFailingMod);
		Assert.DoesNotContain(
			host.Services.GetRequiredService<IModContentControl>().Entries,
			entry => entry.ModId == TestBindFailingMod.Id);
		Assert.DoesNotContain(
			TestBindFailingMod.Id,
			host.Services.GetRequiredService<IModContentFingerprints>().ByMod.Keys);
	}

	[Fact]
	public void ContentCatalog_ReadsRealModStack()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var catalog = host.Services.GetRequiredService<IModContentCatalog>();

		Assert.True(catalog.TryResolve("item", "wooden.sword", out var entry));
		Assert.NotNull(entry);
		Assert.Equal("test.content", entry!.ModId);
		Assert.Equal(2, entry.Definition.SchemaVersion);
		Assert.False(catalog.HasConflicts);
	}

	[Fact]
	public void ContentIds_MustBeCanonicalPaths()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		Assert.False(content.TryRegister(new StubContentDefinition("Bad.Id")), "upper-case ids cannot be canonicalised silently");
		Assert.False(content.TryRegister(new StubContentDefinition("ns:sword")), "the namespace separator belongs to the canonical id");
		Assert.False(content.TryRegister(new StubContentDefinition("bad id")), "whitespace is not a valid path");
		Assert.False(
			content.TryRegister(new StubContentDefinition(new string('a', ContentId.MaxPathLength + 1))),
			"over-length path refused");
		Assert.True(content.TryRegister(new StubContentDefinition("canonical.id_9")));
	}

	[Fact]
	public void ContentCatalog_ResolvesCanonicalIdAndExposesNamespace()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var catalog = host.Services.GetRequiredService<IModContentCatalog>();
		var control = host.Services.GetRequiredService<IModContentControl>();

		Assert.True(catalog.TryResolve("item", "testcontent:wooden.sword", out var entry));
		Assert.Equal("test.content", entry!.ModId);
		Assert.Equal("testcontent", entry.Namespace);
		Assert.Equal("testcontent", control.Entries.Single(e => e.Definition.Id == "wooden.sword").Namespace);
	}

	[Fact]
	public void ContentOwnerQuery_ReadsRealModStack()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = ContentMod(host);

		Assert.True(mod.Context!.ContentOwners.TryGetOwner("item", "wooden.sword", out var owner));
		Assert.Equal("test.content", owner);
		Assert.False(mod.Context.ContentOwners.TryGetOwner("item", "missing", out _));
	}

	[Fact]
	public void MissingRegisterContentPermission_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = EchoMod(host).Context!.Content;

		Assert.False(content.CanRegister, "RegisterContent is required: nothing is implicit.");
		Assert.False(content.TryRegister(new StubContentDefinition("x")));
		Assert.Equal(0, content.Count);
	}

	[Fact]
	public void InvalidRegistration_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;
		var originalCount = content.Count;

		Assert.False(content.TryRegister(null!));
		Assert.False(content.TryRegister(new StubContentDefinition("")));
		Assert.False(content.TryRegister(new StubContentDefinition("id", kind: "")));
		Assert.False(content.TryRegister(new StubContentDefinition("id", kind: "   ")));
		Assert.False(content.TryRegister(new StubContentDefinition(
			"id", kind: new string('k', ModContentPolicy.MaxKindLength + 1))));
		Assert.Equal(originalCount, content.Count);
	}

	[Fact]
	public void DuplicateRegistration_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		Assert.False(content.TryRegister(new StubContentDefinition("wooden.sword")));
		Assert.Equal(2, content.Count);
	}

	[Fact]
	public void Unregister_RemovesDefinition()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		Assert.True(content.TryUnregister("wooden.sword"));
		Assert.False(content.IsRegistered("wooden.sword"));
		Assert.Equal(1, content.Count);
		Assert.False(content.TryUnregister("wooden.sword"));
	}

	[Fact]
	public void RegisteredDefinition_IsStoredAsTheInstanceTheModHandedOver()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		var definition = new ModItemDefinition { Id = "kept.test", DisplayName = "Kept" };
		Assert.True(content.TryRegister(definition));

		Assert.Same(definition, content.Definitions.Single(d => d.Id == "kept.test"));
		Assert.Same(definition, content.Definitions.Single(d => d.Kind == ModContentKind.Item && d.Id == "kept.test"));
	}

	[Fact]
	public void ControlSurface_AggregatesEveryModsEntries()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var control = host.Services.GetRequiredService<IModContentControl>();

		var contentEntries = control.Entries.Where(e => e.ModId == "test.content").ToList();
		Assert.Equal(2, contentEntries.Count);
		Assert.Contains(contentEntries, e => e.Definition.Id == "wooden.sword" && e.Definition.Kind == ModContentKind.Item);
		Assert.Contains(contentEntries, e => e.Definition.Id == "healing.recipe" && e.Definition.Kind == ModContentKind.Recipe);
	}

	[Fact]
	public void PolicyCaps_AreExactAndNoSilentTruncation()
	{
		Assert.True(ModContentPolicy.IsValidId("a"));
		Assert.False(ModContentPolicy.IsValidId(""));
		Assert.False(ModContentPolicy.IsValidId("   "));

		Assert.True(ModContentPolicy.IsValidKind("recipe"));
		Assert.False(ModContentPolicy.IsValidKind(""));
		Assert.False(ModContentPolicy.IsValidKind(new string('k', ModContentPolicy.MaxKindLength + 1)));

		Assert.True(ModContentPolicy.IsValidSchemaVersion(1));
		Assert.False(ModContentPolicy.IsValidSchemaVersion(0));

		Assert.True(ModContentPolicy.CanAdd(ModContentPolicy.MaxDefinitionsPerMod - 1));
		Assert.False(ModContentPolicy.CanAdd(ModContentPolicy.MaxDefinitionsPerMod));
	}
}
