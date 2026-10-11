using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The by-id item read on the mod surface (<c>IModGameState.TryGetItem</c>): what a mod
/// that recorded an instance id can still learn about that item after it moves — including
/// onto a limb, which no inventory projection lists — and the two answers that have to stay
/// apart, "this id ended" and "this id never existed".
///
/// <para>
/// The read's source is the kernel, so the cases drive real commits through the production
/// composition root and read them back through the mod's own context: the host's kernel
/// answers on the host, and a guest answers from the mirror it receives the committed batch
/// into — which is the half that makes the read useful to a mod watching somebody else.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ModItemStateTests
{
	private const ulong HostId = 1001;
	private const ulong Guest1Id = 2001;
	private const ulong Guest2Id = 3001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void AnIdTheFrameworkNeverSaw_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var gameState = GameState(w.Host);

		Assert.True(gameState.CanRead);
		Assert.False(gameState.TryGetItem(0, out _), "0 means 'not yet allocated', not an item");
		Assert.False(gameState.TryGetItem(987654, out _));
	}

	[Fact]
	public void MissingReadGameStatePermission_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var item = Carried(w, HostId, 41, "splint");

		var gameState = EchoMod(w.Host).Context!.GameState;

		Assert.False(gameState.CanRead, "ReadGameState is required: nothing is implicit.");
		Assert.False(gameState.TryGetItem(item, out _));
	}

	[Fact]
	public void ACarriedItem_ReadsWithItsOwnerAndPayload()
	{
		using var w = ItemSimWorld.Create();
		Carried(w, HostId, 41, "splint", condition: 0.75f, favourited: true);

		Assert.True(GameState(w.Host).TryGetItem(41, out var item));
		Assert.Equal(41UL, item.InstanceId);
		Assert.Equal("splint", item.ItemId);
		Assert.Equal(ModItemLocation.Carried, item.Location);
		Assert.Equal(HostId, item.OwnerSteamId);
		Assert.Equal(0.75f, item.Condition);
		Assert.True(item.Favourited);

		// The members a carried location does not use carry their neutral value, so
		// Location is what tells a caller which of them mean something.
		Assert.Equal(-1, item.LimbIndex);
		Assert.Equal(0UL, item.ParentItemId);
		Assert.Equal(0f, item.X);
		Assert.Equal(0f, item.Y);
	}

	[Fact]
	public void AWorldItem_CarriesItsPosition()
	{
		using var w = ItemSimWorld.Create();
		Assert.True(Kernel(w).TrySpawn(HostId, new ItemIdentity(51, "bandage"), ItemLocation.World(12f, -7f), Msg(51, "bandage"), out _, out _));

		Assert.True(GameState(w.Host).TryGetItem(51, out var item));
		Assert.Equal(ModItemLocation.World, item.Location);
		Assert.Equal(12f, item.X);
		Assert.Equal(-7f, item.Y);
		Assert.Equal(0UL, item.OwnerSteamId);
		Assert.Equal(-1, item.LimbIndex);
	}

	[Fact]
	public void AContainedItem_NamesItsContainer()
	{
		using var w = ItemSimWorld.Create();
		Assert.True(Kernel(w).TrySpawnCarried(HostId, 61, "backpack", Msg(61, "backpack"), out _, out _));
		Assert.True(Kernel(w).TrySpawn(HostId, new ItemIdentity(62, "bandage"), ItemLocation.Contained(new ActorId(HostId), 61), Msg(62, "bandage"), out _, out _));

		Assert.True(GameState(w.Host).TryGetItem(62, out var item));
		Assert.Equal(ModItemLocation.Contained, item.Location);
		Assert.Equal(HostId, item.OwnerSteamId);
		Assert.Equal(61UL, item.ParentItemId);
		Assert.Equal(-1, item.LimbIndex);
	}

	[Fact]
	public void AnItemAppliedToALimb_ReadsOnThatLimb_AndCarriedAgainAfterTheRemoval()
	{
		using var w = ItemSimWorld.Create();
		Carried(w, HostId, 41, "splint");

		w.Host.Services.GetRequiredService<IItemControl>().SendItemAppliedToLimb(41, 3);
		w.Driver.Tick(20);

		Assert.True(GameState(w.Host).TryGetItem(41, out var applied));
		Assert.Equal(ModItemLocation.Applied, applied.Location);
		Assert.Equal(HostId, applied.OwnerSteamId);
		Assert.Equal(3, applied.LimbIndex);

		// ... and off again: one relocation each way, the same item and the same id.
		w.Host.Services.GetRequiredService<IItemControl>().SendItemRemovedFromLimb(41, 3);
		w.Driver.Tick(20);

		Assert.True(GameState(w.Host).TryGetItem(41, out var carried));
		Assert.Equal(ModItemLocation.Carried, carried.Location);
		Assert.Equal(HostId, carried.OwnerSteamId);
		Assert.Equal(-1, carried.LimbIndex);
		Assert.Equal("splint", carried.ItemId);
	}

	[Fact]
	public void AThirdMember_ReadsAnItemAnotherMemberApplied()
	{
		// The mirror half: the applying client reports, the host commits, and every OTHER
		// guest receives the committed batch — so a mod watching somebody else follows the
		// item onto their limb, which no character snapshot would tell it.
		using var w = ItemSimWorld.Create();
		Carried(w, Guest1Id, 42, "splint");

		w.G1.Services.GetRequiredService<IItemControl>().SendItemAppliedToLimb(42, 2);
		w.Driver.Tick(50);

		Assert.True(GameState(w.G2).TryGetItem(42, out var item), "the third member's mirror must hold the applied row");
		Assert.Equal(ModItemLocation.Applied, item.Location);
		Assert.Equal(Guest1Id, item.OwnerSteamId);
		Assert.Equal(2, item.LimbIndex);
		Assert.Equal("splint", item.ItemId);

		// ... and the client that performed the apply answers the same way from its own
		// kernel, so the read never depends on being a bystander.
		Assert.True(GameState(w.G1).TryGetItem(42, out var onTheApplier));
		Assert.Equal(ModItemLocation.Applied, onTheApplier.Location);
		Assert.Equal(2, onTheApplier.LimbIndex);
	}

	[Fact]
	public void AnItemThatEnded_ReadsAsTerminal_RatherThanAsUnknown()
	{
		using var w = ItemSimWorld.Create();
		Carried(w, HostId, 41, "splint");

		Assert.True(Kernel(w).TryDestroy(HostId, 41, TerminalKind.Consumed, out _, out _));

		Assert.True(GameState(w.Host).TryGetItem(41, out var item), "an ended id is a fact, not an absence");
		Assert.Equal(ModItemLocation.Terminal, item.Location);
		Assert.Equal(41UL, item.InstanceId);
		Assert.Equal("splint", item.ItemId);
		Assert.Equal(0UL, item.OwnerSteamId);
		Assert.Equal(-1, item.LimbIndex);

		// The distinction the whole read exists for: a different id that never existed is
		// still a false.
		Assert.False(GameState(w.Host).TryGetItem(43, out _));
	}

	/// <summary>One carried item of that owner, in the kernel and in the owner's own snapshot, so a later gesture has a row to move.</summary>
	private static ulong Carried(ItemSimWorld world, ulong owner, ulong instanceId, string definitionId, float condition = 0.75f, bool favourited = false)
	{
		var item = Msg(instanceId, definitionId, condition, favourited);
		if (owner == HostId)
		{
			Assert.True(Kernel(world).TrySpawnCarried(owner, instanceId, definitionId, item, out _, out _));
		}
		else
		{
			world.Host.Services.GetRequiredService<IItemControl>().AdoptTransferredItem(owner, instanceId, item);
		}

		world.Driver.Tick(20);
		return instanceId;
	}

	private static CharacterItemMsg Msg(ulong instanceId, string definitionId, float condition = 0.75f, bool favourited = false) =>
		new() { InstanceId = instanceId, ItemId = definitionId, Condition = condition, Favourited = favourited, SlotIndex = 0 };

	private static ItemKernelAuthority Kernel(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<ItemKernelAuthority>();

	private static IModGameState GameState(TestNode node) =>
		((TestReadGameStateMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestReadGameStateMod)).Context!.GameState;

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestEchoMod);
}
