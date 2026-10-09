using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The attribute path over the real mod stack. The fixture mod registers NOTHING
/// in <see cref="ICuoMod.Bind"/>: its declarations are discovered, their kinds
/// come from the contracts they implement, and they land in the same registry,
/// catalog and owner query the code path feeds — one provider, two ways to feed
/// it. One declaration throws from a member getter and is refused while its
/// siblings bind, which the code path cannot promise: its per-entry catch wraps
/// the binder, not a provider's own loop.
/// </summary>
[Trait("Category", "Integration")]
public class ModContentDeclarationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestDeclaredContentMod DeclaredOf(TestNode node) =>
		(TestDeclaredContentMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestDeclaredContentMod);

	[Fact]
	public void DeclaredContent_IsRegisteredBeforeTheModsOwnBind()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		Assert.True(
			DeclaredOf(host).DeclarationsVisibleAtBind,
			"the scan runs before Bind, so a mod's own bind already sees what it declared next to itself");
	}

	[Fact]
	public void AttributeDeclarations_ReachTheRegistryAsTheModsOwnClasses()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = DeclaredOf(host).Context!.Content;

		Assert.Equal(2, content.Count);
		Assert.IsType<TestDeclaredContentMod.DeclaredItem>(
			content.Definitions.Single(definition => definition.Id == TestDeclaredContentMod.ItemId));
		Assert.IsType<TestDeclaredContentMod.DeclaredRecipe>(
			content.Definitions.Single(definition => definition.Id == TestDeclaredContentMod.RecipeId));
	}

	[Fact]
	public void AThrowingDeclaration_IsRefused_WhileItsSiblingsBind()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var content = DeclaredOf(host).Context!.Content;

		Assert.False(content.IsRegistered(TestDeclaredContentMod.ThrowingItemId));
		Assert.True(content.IsRegistered(TestDeclaredContentMod.ItemId));
		Assert.True(content.IsRegistered(TestDeclaredContentMod.RecipeId));
	}

	[Fact]
	public void DeclaredContent_IsAddressableThroughTheCatalogAndTheOwnerQuery()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var catalog = host.Services.GetRequiredService<IModContentCatalog>();

		Assert.True(catalog.TryResolve(ModContentKind.Item, "testdeclared:declared.item", out var entry));
		Assert.Equal("test.declared", entry!.ModId);
		Assert.False(catalog.HasConflicts);

		Assert.True(DeclaredOf(host).Context!.ContentOwners.TryGetOwner(ModContentKind.Item, TestDeclaredContentMod.ItemId, out var owner));
		Assert.Equal("test.declared", owner);
	}

	[Fact]
	public void BothPaths_FeedOneRegistry()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var control = host.Services.GetRequiredService<IModContentControl>();

		// The code path (TestContentMod's own TryRegister calls in Bind) and the
		// attribute path (this mod's declarations) are entries of ONE view.
		Assert.Contains(control.Entries, entry => entry.ModId == "test.content" && entry.Definition.Id == "wooden.sword");
		Assert.Contains(control.Entries, entry => entry.ModId == "test.declared" && entry.Definition.Id == TestDeclaredContentMod.ItemId);
	}
}
