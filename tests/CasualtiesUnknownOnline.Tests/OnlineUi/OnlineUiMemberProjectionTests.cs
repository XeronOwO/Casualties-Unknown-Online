using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public sealed class OnlineUiMemberProjectionTests
{
	private const ulong Local = 100;
	private const ulong Remote = 200;
	private const ulong Other = 300;

	[Fact]
	public void BuildsOneRowPerLobbyMemberInLobbyOrder()
	{
		var rows = Build(
			[Remote, Local, Other],
			[],
			null,
			canAdmin: false,
			localInWorld: false,
			hasHealItem: false);

		Assert.Equal(3, rows.Count);
		Assert.Equal(Remote, rows[0].SteamId);
		Assert.Equal(Local, rows[1].SteamId);
		Assert.Equal(Other, rows[2].SteamId);
		Assert.True(rows[1].IsLocal);
		Assert.False(rows[2].IsLocal);
	}

	[Fact]
	public void HostCanKickAndBanNonLocalHandshakenMembers()
	{
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			null,
			canAdmin: true,
			localInWorld: false,
			hasHealItem: false);

		Assert.True(rows[1].CanKick);
		Assert.True(rows[1].CanBan);
		Assert.False(rows[0].CanKick);
		Assert.False(rows[0].CanBan);
	}

	[Fact]
	public void NonHostCannotKickOrBan()
	{
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			null,
			canAdmin: false,
			localInWorld: false,
			hasHealItem: false);

		Assert.False(rows[1].CanKick);
		Assert.False(rows[1].CanBan);
	}

	[Fact]
	public void DeadUnconsciousRemoteCanCarryAndTakeSlotItems()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = false, Conscious = false })!,
		};
		var inventory = new Dictionary<ulong, RemoteInventorySnapshot>
		{
			[Remote] = RemoteInventorySnapshot.From(new CharacterDataMsg
			{
				Items =
				{
					new CharacterItemMsg { InstanceId = 42, ItemId = "Bandage", SlotIndex = 2 },
					new CharacterItemMsg { InstanceId = 0, ItemId = "None", SlotIndex = 3 },
					new CharacterItemMsg { InstanceId = 7, ItemId = "Worn", SlotIndex = -2 },
				},
			})!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null,
			getInventory: id => inventory.TryGetValue(id, out var inv) ? inv : null);

		Assert.True(rows[1].CanCarry);
		Assert.True(rows[1].CanTake);
		Assert.Single(rows[1].TakeableItems);
		Assert.Equal(42UL, rows[1].TakeableItems[0].InstanceId);
		Assert.False(rows[1].CanHeal);
		Assert.True(rows[1].CanRecruit);
	}

	[Fact]
	public void AliveRemoteCanBeHealedWhenLocalHasMedicalItem()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};
		var healItems = new List<LocalHealItem> { new(11, "Medkit") };

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null,
			healItems: healItems);

		Assert.True(rows[1].CanHeal);
		Assert.Single(rows[1].HealItems);
		Assert.False(rows[1].CanCarry);
		Assert.False(rows[1].CanTake);
	}

	[Fact]
	public void DeadRemoteCanBeRecruitedButNotHealed()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = false, Conscious = false })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].CanHeal);
		Assert.True(rows[1].CanRecruit);
	}

	[Fact]
	public void LocalCarryingRemoteProducesDropInsteadOfCarry()
	{
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			new FakeInteraction { CarriedByLocal = Remote },
			canAdmin: false,
			localInWorld: false,
			hasHealItem: false);

		Assert.True(rows[1].CanDrop);
		Assert.False(rows[1].CanCarry);
	}

	[Fact]
	public void BannedMemberIsMarked()
	{
		var banList = new FakeBanService { Banned = [Remote] };
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			null,
			hostBan: banList,
			canAdmin: true,
			localInWorld: false,
			hasHealItem: false);

		Assert.True(rows[1].IsBanned);
	}

	private static IReadOnlyList<OnlineUiMemberRow> Build(
		IReadOnlyList<ulong> lobbyMembers,
		IReadOnlyList<MemberPresenceTable.MemberPresence> members,
		IPlayerInteractionControl? interaction,
		IHostBanService? hostBan = null,
		bool canAdmin = false,
		bool localInWorld = false,
		bool hasHealItem = false,
		Func<ulong, RemoteVitalsSnapshot?>? getVitals = null,
		Func<ulong, RemoteInventorySnapshot?>? getInventory = null,
		IReadOnlyList<LocalHealItem>? healItems = null)
	{
		return OnlineUiMemberProjection.Build(
			Local,
			lobbyOwner: Local,
			lobbyMembers: lobbyMembers,
			members: members,
			displayName: id => $"player-{id}",
			getVitals: getVitals ?? (_ => null),
			getInventory: getInventory ?? (_ => null),
			playerInteraction: interaction,
			hostBan: hostBan,
			canAdmin: canAdmin,
			localInWorld: localInWorld,
			hasHealItem: hasHealItem,
			healItems: healItems ?? []);
	}

	private static MemberPresenceTable.MemberPresence Presence(ulong steamId, bool handshaken, bool inWorld) =>
		new() { SteamId = steamId, Handshaken = handshaken, InWorld = inWorld };

	private sealed class FakeInteraction : IPlayerInteractionControl
	{
		public ulong? CarriedByLocal;

		public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId)
		{
		}

		public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg)
		{
		}

		public void FireTransferReceived(PlayerInventoryTransferMsg msg)
		{
		}

		public event Action<PlayerInventoryTransferMsg>? TransferReceived
		{
			add { }
			remove { }
		}

		public void SendCarryStartRequest(ulong targetSteamId)
		{
		}

		public void SendCarryStopRequest(ulong carriedSteamId)
		{
		}

		public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg)
		{
		}

		public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg)
		{
		}

		public void FireCarryStateReceived(PlayerCarryStateMsg msg)
		{
		}

		public event Action<PlayerCarryStateMsg>? CarryStateChanged
		{
			add { }
			remove { }
		}

		public bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId)
		{
			carrierSteamId = 0;
			return false;
		}

		public bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId)
		{
			carriedSteamId = 0;
			if (carrierSteamId == Local && CarriedByLocal is { } carried)
			{
				carriedSteamId = carried;
				return true;
			}

			return false;
		}

		public void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0)
		{
		}

		public void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg)
		{
		}

		public void FireHealReceived(PlayerHealResultMsg msg)
		{
		}

		public event Action<PlayerHealResultMsg>? HealReceived
		{
			add { }
			remove { }
		}
	}

	private sealed class FakeBanService : IHostBanService
	{
		public HashSet<ulong> Banned { get; set; } = [];

		public bool IsBanned(ulong steamId) => Banned.Contains(steamId);

		public IReadOnlyCollection<ulong> BannedSteamIds => Banned;

		public bool Ban(ulong steamId, string reason) => false;

		public bool Unban(ulong steamId) => false;
	}
}
