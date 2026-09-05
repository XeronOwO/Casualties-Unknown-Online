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
	public void RemoteInventoryTakeDisabled_HidesNonLocalTakeActions()
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
			getInventory: id => inventory.TryGetValue(id, out var inv) ? inv : null,
			allowRemoteInventoryTake: false);

		Assert.False(rows[1].CanTake);
		Assert.Empty(rows[1].TakeableItems);
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
	public void NoLineOfSight_HidesAllDirectInteractionActions()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = false, Conscious = false })!,
		};
		var inventory = new Dictionary<ulong, RemoteInventorySnapshot>
		{
			[Remote] = RemoteInventorySnapshot.From(new CharacterDataMsg
			{
				Items = { new CharacterItemMsg { InstanceId = 42, ItemId = "Bandage", SlotIndex = 2 } },
			})!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null,
			getInventory: id => inventory.TryGetValue(id, out var inv) ? inv : null,
			healItems: [new LocalHealItem(11, "Medkit")],
			hasLineOfSight: _ => false);

		Assert.False(rows[1].CanSee);
		Assert.False(rows[1].CanCarry);
		Assert.False(rows[1].CanTake);
		Assert.Empty(rows[1].TakeableItems);
		Assert.False(rows[1].CanRecruit);
		Assert.False(rows[1].CanHeal);
		Assert.False(rows[1].CanPush);
	}

	[Fact]
	public void NoLineOfSight_HidesConsciousSupportActions()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null,
			healItems: [new LocalHealItem(11, "Medkit")],
			hasLineOfSight: _ => false);

		Assert.False(rows[1].CanSee);
		Assert.False(rows[1].CanHeal);
		Assert.False(rows[1].CanPush);
		Assert.False(rows[1].CanPiggyback);
		Assert.False(rows[1].CanCarryOnBack);
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
	public void ConsciousAliveRemoteCanPiggybackWhenLocalInWorld()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.True(rows[1].CanPiggyback);
		Assert.False(rows[1].CanCarry);
	}

	[Fact]
	public void ConsciousAliveRemoteCanRideOnLocalBackWhenLocalInWorld()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.True(rows[1].CanCarryOnBack);
		Assert.True(rows[1].CanPiggyback);
	}

	[Fact]
	public void CarriedLocalCannotPutRemoteOnOwnBack()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction { CarrierOfLocal = Remote },
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].CanCarryOnBack);
		Assert.False(rows[1].CanPiggyback);
	}

	[Fact]
	public void CarriedLocalCannotPiggybackRemote()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction { CarrierOfLocal = Remote },
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].CanPiggyback);
	}

	[Fact]
	public void InWorldRemoteCanBePushedWhenLocalInWorld()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.True(rows[1].CanPush);
	}

	[Fact]
	public void CarriedLocalCannotPushRemote()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction { CarrierOfLocal = Remote },
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].CanPush);
	}

	[Fact]
	public void ConsciousRemoteCannotPiggybackWhenLocalIsNotInWorld()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: false,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].CanPiggyback);
	}

	[Fact]
	public void CarriedLocalCanRequestDropWhenInWorld()
	{
		var interaction = new FakeInteraction { CarrierOfLocal = Remote };
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			interaction,
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false);

		Assert.True(rows[0].CanRequestDrop);
		Assert.False(rows[1].CanRequestDrop);
	}

	[Fact]
	public void CarriedLocalShowsGetDownOnLocalRowAndOnCarrierRemoteRow()
	{
		var interaction = new FakeInteraction { CarrierOfLocal = Remote };
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			interaction,
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false);

		Assert.True(rows[0].CanRequestDrop);
		Assert.True(rows[1].CanRequestDropFromCarrier);
		Assert.False(rows[1].CanDrop);
	}

	[Fact]
	public void DeadMemberExposesDeadStatusFlag()
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
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.True(rows[1].IsDead);
		Assert.False(rows[1].IsUnconscious);
		Assert.False(rows[1].IsCarryingSomeone);
		Assert.False(rows[1].IsCarried);
	}

	[Fact]
	public void UnconsciousMemberExposesUnconsciousStatusFlag()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = false })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			new FakeInteraction(),
			canAdmin: false,
			localInWorld: true,
			hasHealItem: false,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(rows[1].IsDead);
		Assert.True(rows[1].IsUnconscious);
	}

	[Fact]
	public void CarryRelationExposesCarryingAndCarriedFlags()
	{
		var interaction = new FakeInteraction
		{
			CarriedByRemote = Other,
			CarrierOfRemote = Other,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			interaction,
			canAdmin: false,
			localInWorld: false,
			hasHealItem: false);

		Assert.True(rows[1].IsCarryingSomeone);
		Assert.True(rows[1].IsCarried);
		Assert.False(rows[1].IsDead);
		Assert.False(rows[1].IsUnconscious);
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

	[Fact]
	public void RowCarriesTheEffectiveConfiguredColor()
	{
		var selected = new PlayerColorValue(0.1f, 0.2f, 0.3f, 1f);
		var rows = Build(
			[Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			null,
			getColor: id => id == Remote ? selected : PlayerColorResolver.Resolve(id));

		Assert.Equal(selected.R, rows[0].Color.R);
		Assert.Equal(selected.G, rows[0].Color.G);
		Assert.Equal(selected.B, rows[0].Color.B);
	}

	[Fact]
	public void DuplicateCaseInsensitiveDisplayNames_PopulatePeerIdOnEveryCollidingRow()
	{
		var rows = Build(
			[Remote, Other],
			[],
			null,
			displayName: id => id == Remote ? "Alice" : "alice");

		Assert.Equal(Remote.ToString("X"), rows[0].PeerIdHex);
		Assert.Equal(Other.ToString("X"), rows[1].PeerIdHex);
	}

	[Fact]
	public void UniqueDisplayNames_DoNotPopulatePeerId()
	{
		var rows = Build(
			[Remote, Other],
			[],
			null,
			displayName: id => id == Remote ? "Alice" : "Bob");

		Assert.Null(rows[0].PeerIdHex);
		Assert.Null(rows[1].PeerIdHex);
	}

	[Fact]
	public void InWorldRemoteWithVitals_CanViewMedical()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			null,
			localInWorld: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.True(rows[1].CanViewMedical);
		Assert.False(rows[0].CanViewMedical);
	}

	[Fact]
	public void CanViewMedical_DoesNotRequireLineOfSight()
	{
		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			null,
			localInWorld: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null,
			hasLineOfSight: _ => false);

		Assert.True(rows[1].CanViewMedical);
		Assert.False(rows[1].CanSee);
	}

	[Fact]
	public void RemoteWithoutVitalsOrNotInWorld_CannotViewMedical()
	{
		var rows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: true)],
			null,
			localInWorld: true);

		Assert.False(rows[1].CanViewMedical);

		var vitals = new Dictionary<ulong, RemoteVitalsSnapshot>
		{
			[Remote] = RemoteVitalsSnapshot.From(new CharacterHealthMsg { Alive = true, Conscious = true })!,
		};

		var menuRows = Build(
			[Local, Remote],
			[Presence(Remote, handshaken: true, inWorld: false)],
			null,
			localInWorld: true,
			getVitals: id => vitals.TryGetValue(id, out var v) ? v : null);

		Assert.False(menuRows[1].CanViewMedical);
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
		IReadOnlyList<LocalHealItem>? healItems = null,
		bool allowRemoteInventoryTake = true,
		Func<ulong, bool>? hasLineOfSight = null,
		Func<ulong, string>? displayName = null,
		Func<ulong, PlayerColorValue>? getColor = null)
	{
		return OnlineUiMemberProjection.Build(
			Local,
			lobbyOwner: Local,
			lobbyMembers: lobbyMembers,
			members: members,
			displayName: displayName ?? (id => $"player-{id}"),
			getVitals: getVitals ?? (_ => null),
			getInventory: getInventory ?? (_ => null),
			playerInteraction: interaction,
			hostBan: hostBan,
			canAdmin: canAdmin,
			localInWorld: localInWorld,
			hasHealItem: hasHealItem,
			healItems: healItems ?? [],
			allowRemoteInventoryTake: allowRemoteInventoryTake,
			hasLineOfSight: hasLineOfSight,
			getColor: getColor);
	}

	private static MemberPresenceTable.MemberPresence Presence(ulong steamId, bool handshaken, bool inWorld) =>
		new() { SteamId = steamId, Handshaken = handshaken, InWorld = inWorld };

	private sealed class FakeInteraction : IPlayerInteractionControl
	{
		public ulong? CarriedByLocal;
		public ulong? CarrierOfLocal;
		public ulong? CarriedByRemote;
		public ulong? CarrierOfRemote;

		public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId)
		{
		}

		public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg)
		{
		}

		public void SendRemoteInventoryOperation(RemoteInventoryOperationRequestMsg msg)
		{
		}

		public void HandleRemoteInventoryOperation(ulong sender, RemoteInventoryOperationRequestMsg msg)
		{
		}

		public void FireRemoteInventoryApplyReceived(RemoteInventoryApplyMsg msg)
		{
		}

		public event Action<RemoteInventoryApplyMsg>? RemoteInventoryApplyReceived
		{
			add { }
			remove { }
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

		public void SendPiggybackRequest(ulong targetSteamId)
		{
		}

		public void SendCarryOnBackRequest(ulong targetSteamId)
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
			if (carriedSteamId == Local && CarrierOfLocal is { } carrier)
			{
				carrierSteamId = carrier;
				return true;
			}

			if (carriedSteamId == Remote && CarrierOfRemote is { } remoteCarrier)
			{
				carrierSteamId = remoteCarrier;
				return true;
			}

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

			if (carrierSteamId == Remote && CarriedByRemote is { } remoteCarried)
			{
				carriedSteamId = remoteCarried;
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

		public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0)
		{
		}

		public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg)
		{
		}

		public void FireUseReceived(PlayerItemUseResultMsg msg)
		{
		}

		public event Action<PlayerItemUseResultMsg>? UseReceived
		{
			add { }
			remove { }
		}

		public void SendPushRequest(ulong targetSteamId)
		{
		}

		public void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg)
		{
		}

		public void FirePushReceived(PlayerPushResultMsg msg)
		{
		}

		public event Action<PlayerPushResultMsg>? PushReceived
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
