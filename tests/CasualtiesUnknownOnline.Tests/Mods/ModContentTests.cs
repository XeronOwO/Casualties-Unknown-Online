using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The mod content registration surface over the real mod stack: definitions
/// are registered per-mod through <see cref="IModContext.Content"/], invalid
/// or duplicate registrations are refused, RegisterContent is enforced, the
/// plugin-facing <see cref="IModContentControl"/> aggregates every mod's
/// entries, and payloads are defensively copied on write and read.
/// </summary>
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
		Assert.Equal("item", sword.Kind);
		Assert.Equal([1, 2, 3], sword.Data);
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

		Assert.False(content.TryRegister("bad.schema", "item", [1], 0));
		Assert.False(content.TryRegister("bad.schema", "item", [1], -1));
		Assert.False(content.IsRegistered("bad.schema"));
		Assert.Equal(originalCount, content.Count);
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
		Assert.False(content.TryRegister("x", "item", [1]));
		Assert.Equal(0, content.Count);
	}

	[Fact]
	public void InvalidRegistration_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;
		var originalCount = content.Count;

		Assert.False(content.TryRegister("", "item", [1]));
		Assert.False(content.TryRegister("id", "", [1]));
		Assert.False(content.TryRegister("id", "item", null!));
		Assert.False(content.TryRegister("id", "item", new byte[ModContentPolicy.MaxDefinitionBytes + 1]));
		Assert.Equal(originalCount, content.Count);
	}

	[Fact]
	public void DuplicateRegistration_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		Assert.False(content.TryRegister("wooden.sword", "item", [9]));
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
	public void PayloadsAreDefensivelyCopied_OnWriteAndRead()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = ContentMod(host).Context!.Content;

		var original = new byte[] { 1, 2, 3 };
		Assert.True(content.TryRegister("copy.test", "item", original));
		original[0] = 9; // caller mutation must not leak into the registry

		var definition = content.Definitions.Single(d => d.Id == "copy.test");
		var firstRead = definition.Data;
		firstRead[1] = 8; // caller mutation of the returned copy must not leak either

		var secondRead = content.Definitions.Single(d => d.Id == "copy.test").Data;
		Assert.Equal([1, 2, 3], secondRead);
	}

	[Fact]
	public void ControlSurface_AggregatesEveryModsEntries()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var control = host.Services.GetRequiredService<IModContentControl>();

		var contentEntries = control.Entries.Where(e => e.ModId == "test.content").ToList();
		Assert.Equal(2, contentEntries.Count);
		Assert.Contains(contentEntries, e => e.Definition.Id == "wooden.sword" && e.Definition.Kind == "item");
		Assert.Contains(contentEntries, e => e.Definition.Id == "healing.recipe" && e.Definition.Kind == "recipe");
	}

	[Fact]
	public void PolicyCaps_AreExactAndNoSilentTruncation()
	{
		Assert.True(ModContentPolicy.IsValidId("a"));
		Assert.False(ModContentPolicy.IsValidId(""));
		Assert.False(ModContentPolicy.IsValidId("   "));

		Assert.True(ModContentPolicy.IsValidKind("recipe"));
		Assert.False(ModContentPolicy.IsValidKind(""));

		Assert.True(ModContentPolicy.IsValidData([]));
		Assert.False(ModContentPolicy.IsValidData(null));
		Assert.False(ModContentPolicy.IsValidData(new byte[ModContentPolicy.MaxDefinitionBytes + 1]));

		Assert.True(ModContentPolicy.CanAdd(ModContentPolicy.MaxDefinitionsPerMod - 1));
		Assert.False(ModContentPolicy.CanAdd(ModContentPolicy.MaxDefinitionsPerMod));
	}
}
