using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The reconnect-restore round: a guest that drops from the link while still
/// IN the world and reconnects must get the world snapshots back (trap layout,
/// block state, trap consumptions, opened entities, world items) — not just
/// the character restore. The handshake restores <c>member.InWorld</c> from the
/// peer's reported scene state, which bypasses <see cref="SceneStateHandler"/>'s
/// InWorld EDGE — the only place the world snapshots fan out — so a
/// still-in-world reconnect got the character save but no world snapshots
/// (observed live: the spent spike not shown, the shuttle door closed again,
/// the trashbag contents regressed).
/// </summary>
public class ReconnectWorldSnapshotTests
{
	private const ulong LobbyId = 9001;

	[Fact]
	public void GuestReconnects_WhileStillInWorld_ReceivesTheWorldSnapshotsAgain()
	{
		using var w = ItemSimWorld.Create();

		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");

		// g1 enters the world — the first fan-out sends the layout.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) == 1,
			$"the first world entry sends the layout, got {w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot)}");

		ReconnectGuestStillInWorld(w);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) >= 2,
			$"the reconnect must resend the world snapshots, got {w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot)}");
	}

	[Fact]
	public void GuestReconnects_WhileStillInWorld_ReceivesAllSevenWorldSnapshotsAgain()
	{
		using var w = ItemSimWorld.Create();

		// Populate every world-state table the snapshots carry.
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");
		hostWorld.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);
		hostWorld.ReportOpenedEntity(30f, 40f);
		hostWorld.ReportBuildingEntityHealth(50f, 60f, 25f);
		hostWorld.ReportBlockDamage(5, 6, 70f);
		hostWorld.ReportBlockState(5, 6, 7);
		w.Spawn(w.G1, 100, new CharacterItemMsg { ItemId = "ore", Condition = 1f });
		var itemSnapshots = new List<IReadOnlyList<WorldItem>>();
		w.G1.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => itemSnapshots.Add(items);

		// g1 enters the world — the first fan-out sends the snapshot group.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) == 1, "first entry: trap layout");
		Assert.Single(itemSnapshots);

		ReconnectGuestStillInWorld(w);

		// The reconnect must re-fan-out the WHOLE snapshot group — a missing one
		// is exactly the reconnect-restore regression this guard exists for.
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) >= 2, $"reconnect: trap layout, got {w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot)}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapStateSnapshot) >= 2, $"reconnect: trap state, got {w.ReceivedCount(w.G1, NetMsg.TrapStateSnapshot)}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.OpenedEntitiesSnapshot) >= 2, $"reconnect: opened entities, got {w.ReceivedCount(w.G1, NetMsg.OpenedEntitiesSnapshot)}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.BuildingEntityHealthSnapshot) >= 2, $"reconnect: building-entity health, got {w.ReceivedCount(w.G1, NetMsg.BuildingEntityHealthSnapshot)}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot) >= 2, $"reconnect: block damage, got {w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot)}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBlockState) >= 2, $"reconnect: block state, got {w.ReceivedCount(w.G1, NetMsg.WorldBlockState)}");
		Assert.True(itemSnapshots.Count >= 2, $"reconnect: world items, got {itemSnapshots.Count}");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete) >= 2,
			$"reconnect: snapshot-complete marker, got {w.ReceivedCount(w.G1, NetMsg.WorldSnapshotComplete)}");
	}

	[Fact]
	public void GuestReconnects_WhileStillInWorld_FiresRemoteSceneChangedForTheAdapter()
	{
		using var w = ItemSimWorld.Create();

		// g1 enters the world once.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);

		// Subscribe AFTER the first entry — the reconnect must fire the entry
		// signal again so the Game Adapter re-fans-out its world-entry state
		// (geyser liquid types, keypad codes) instead of waiting up to 60 s.
		var entered = new List<ulong>();
		w.Host.Session.RemoteSceneChanged += (id, inWorld) => { if (inWorld) { entered.Add(id); } };

		ReconnectGuestStillInWorld(w);

		Assert.Contains(w.G1.SteamId, entered);
	}

	private static void ReconnectGuestStillInWorld(ItemSimWorld w)
	{
		// g1 drops from the lobby — the host removes the member (presence check).
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G2.SteamId];
		w.Driver.TickUntil(() => !w.Host.Session.Members.Any(m => m.SteamId == w.G1.SteamId), maxMs: 5000);
		Assert.True(!w.Host.Session.Members.Any(m => m.SteamId == w.G1.SteamId), "g1 left the host's roster");

		// g1 returns, still IN the world (its LocalInWorld survived — only the
		// link dropped, the process never left the world). The handshake
		// restores member.InWorld = true from the peer's scene report.
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.FireLobbyEntered(LobbyId);
		w.Driver.TickUntil(
			() => w.Host.Session.Members.Any(m => m.SteamId == w.G1.SteamId && m.Handshaken),
			maxMs: 5000);
	}
}
