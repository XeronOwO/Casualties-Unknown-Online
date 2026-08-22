using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The read-only game-state projection (Phase 4 Mod API remainder): the mod
/// surface is gated by ReadGameState and exposes the same session-scoped
/// remote character facts the Online UI already consumes, without exposing
/// Unity or game-assembly types.
/// </summary>
public class ModGameStateTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestReadGameStateMod GameStateMod(TestNode node) =>
		(TestReadGameStateMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestReadGameStateMod);

	private static TestEchoMod EchoMod(TestNode node) =>
		(TestEchoMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestEchoMod);

	private static CharacterDataMsg VitalsSnapshot(ulong owner, float brainHealth = 70f, bool conscious = true) => new()
	{
		OwnerSteamId = owner,
		Health = new CharacterHealthMsg
		{
			BrainHealth = brainHealth,
			Hunger = 40f,
			Thirst = 50f,
			Stamina = 80f,
			Energy = 60f,
			Temperature = 37f,
			Alive = brainHealth > 0f,
			Conscious = conscious && brainHealth > 0f,
		},
	};

	private static CharacterDataMsg InventorySnapshot(ulong owner, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		HandSlot = 1,
		Items = [.. items],
	};

	private static CharacterItemMsg Item(string itemId, int slotIndex, params CharacterItemMsg[] contents) => new()
	{
		InstanceId = 1234,
		ItemId = itemId,
		SlotIndex = slotIndex,
		Condition = 75.5f,
		Favourited = true,
		Contents = [.. contents],
	};

	[Fact]
	public void MissingReadGameStatePermission_IsRefused()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		var gameState = EchoMod(host).Context!.GameState;

		Assert.False(gameState.CanRead, "ReadGameState is required: nothing is implicit.");
		Assert.False(gameState.TryGetPlayer(GuestId, out _));
	}

	[Fact]
	public void WithPermission_ExposesRemotePlayerVitalsAndInventory()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		((ISessionControl)host.Session).FireRemoteSceneChanged(GuestId, true);
		host.Services.GetRequiredService<ICharacterDataControl>()
			.FireCharacterDataReceived(GuestId, VitalsSnapshot(0, brainHealth: 42f));
		host.Services.GetRequiredService<ICharacterDataControl>()
			.FireCharacterDataReceived(GuestId, InventorySnapshot(
				0,
				Item("medkit", 0),
				Item("backpack", 1, Item("inner", 1))));

		Assert.True(host.Services.GetRequiredService<RemoteVitalsService>().TryGet(GuestId, out _), "vitals cache should be populated");
		Assert.True(host.Services.GetRequiredService<RemoteInventoryService>().TryGet(GuestId, out _), "inventory cache should be populated");

		var gameState = GameStateMod(host).Context!.GameState;

		Assert.True(gameState.CanRead);
		Assert.True(gameState.TryGetPlayer(GuestId, out var player));
		Assert.Equal(GuestId, player.SteamId);
		Assert.NotNull(player.Vitals);
		Assert.Equal(42f, player.Vitals.BrainHealth);
		Assert.Equal(80f, player.Vitals.Stamina);
		Assert.NotNull(player.Inventory);
		Assert.Equal(2, player.Inventory.Count);
		Assert.Equal(1, player.Inventory.HandSlot);
		Assert.Equal("medkit", player.Inventory.Items[0].ItemId);
		Assert.Equal(75.5f, player.Inventory.Items[0].Condition);
		Assert.True(player.Inventory.Items[0].Favourited);
		Assert.Equal(1234UL, player.Inventory.Items[0].InstanceId);
		Assert.Equal("inner", player.Inventory.Items[1].Contents[0].ItemId);
	}

	[Fact]
	public void Guest_ExposesHostPlayerState()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			((ISessionControl)guest.Session).FireRemoteSceneChanged(HostId, true);
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(VitalsSnapshot(0, brainHealth: 88f));
			guest.Services.GetRequiredService<ICharacterDataControl>()
				.FireHostCharacterDataReceived(InventorySnapshot(0, Item("rifle", -2)));

			var gameState = GameStateMod(guest).Context!.GameState;

			Assert.True(gameState.CanRead);
			Assert.True(gameState.TryGetPlayer(HostId, out var player));
			Assert.Equal(HostId, player.SteamId);
			Assert.Equal(88f, player.Vitals!.BrainHealth);
			Assert.Equal("rifle", player.Inventory!.Items[0].ItemId);
			Assert.Equal(-2, player.Inventory.Items[0].SlotIndex);
		}
	}

	[Fact]
	public void TryGetPlayer_ReturnsFalse_WhenNoSnapshotHasArrived()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		var gameState = GameStateMod(host).Context!.GameState;

		Assert.False(gameState.TryGetPlayer(GuestId, out _));
	}

	[Fact]
	public void RemoteLeavingWorld_RemovesTheProjection()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using var hostScope = host;

		((ISessionControl)host.Session).FireRemoteSceneChanged(GuestId, true);
		host.Services.GetRequiredService<ICharacterDataControl>()
			.FireCharacterDataReceived(GuestId, VitalsSnapshot(0, brainHealth: 55f));

		var gameState = GameStateMod(host).Context!.GameState;
		Assert.True(gameState.TryGetPlayer(GuestId, out _));

		((ISessionControl)host.Session).FireRemoteSceneChanged(GuestId, false);

		Assert.False(gameState.TryGetPlayer(GuestId, out _));
	}
}
