using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world-entry backfill fan-out: owns the ordered snapshot group a member
/// receives on its first InWorld edge or on a reconnect while still InWorld.
/// Previously this method lived on <see cref="HandlerContext"/>, making that
/// class a god object that both handed handlers their control surfaces and
/// owned a concrete world-entry flow. This service is the single owner of that
/// fan-out and appends the explicit world-entry completion marker after the
/// snapshot group.
///
/// It also owns the IN-SESSION repair set (<see cref="SendInSessionRepair"/>):
/// the absolute in-world tables the host re-sends on its periodic cycle, so a
/// member that stays continuously in the world converges after a send the lazy
/// P2P session swallowed. Both flows are "what this member needs to be brought
/// to the host's state", so they live together — the trap layout was missing
/// from the periodic cycle for exactly as long as the two lists were owned in
/// different places.
///
/// Both groups also re-derive the trap layout from the host's LIVE scene before
/// they read it (<see cref="ILiveTrapLayoutSource"/>): the table is only as fresh
/// as its last scan, so a send that used it as-is could hand an entering or
/// reconnecting member an entity the world has since removed — or, on a layer
/// whose record the boundary reset cleared, nothing at all.
///
/// Both groups carry the kernel checkpoint FIRST: it is what establishes the
/// member's run baseline, and every generation-stamped absolute table
/// (block state, block damage, trap layout, runtime entities) is compared
/// against that baseline on arrival — sending one before the checkpoint makes
/// the receiver refuse the host's own current-generation table (review finding,
/// cycle `review/generation-identity-remaining-families.md`).
///
/// The recipe-unlock set (sync-coverage audit I6) rides both groups too, and is
/// deliberately NOT generation-stamped: the recipe table is a RUN fact (the game
/// rebuilds it in `WorldGeneration.Awake`), its keys are recipe indices rather
/// than layer-relative positions, and an unlock only ever adds — a set that
/// outlives the layer it was read in still asks for nothing but writes the
/// receiver already holds.
/// </summary>
public sealed class WorldEntryFanout(
	IWorldControl world,
	IItemControl items,
	IEnemySyncControl enemies,
	IEntitySyncControl entities,
	ICraftControl craft,
	IKernelProtocolControl kernelProtocol,
	ILogger<WorldEntryFanout> log,
	ILiveTrapLayoutSource? liveLayout = null)
{
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly IEnemySyncControl _enemies = enemies;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ICraftControl _craft = craft;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly ILogger<WorldEntryFanout> _log = log;
	private readonly ILiveTrapLayoutSource? _liveLayout = liveLayout;

	/// <summary>
	/// Host only: send the complete world-entry snapshot group to one member,
	/// then the explicit completion marker. Order-dependent by design — the
	/// receiver uses the marker to know the full backfill has arrived.
	/// </summary>
	public void Send(ulong steamId)
	{
		_log.LogInformation("Sending world-entry snapshot group to {Peer}.", steamId);

		RefreshTrapLayout();

		// The kernel checkpoint goes FIRST because it is what carries the run
		// baseline (RunEpoch + LayerIndex) to this member, and every absolute
		// table below that is stamped with the world/layer generation is
		// compared against that baseline on arrival: a stamped table sent before
		// it would be refused as stale by the very member it is meant to bring
		// up to date, with no second chance before the 60 s repair.
		_kernelProtocol.SendCheckpoint(steamId);
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_world.SendRadiationLineState(steamId);
		_items.SendItemSnapshot(steamId);
		_enemies.SendEnemySnapshot(steamId);
		_world.SendRuntimeEntitySnapshot(steamId);
		_craft.SendRecipeUnlockSnapshot(steamId); // the run's unlocked recipe set — a member that joined after an unlock learns it here, and nowhere else
		_world.SendWorldSnapshotComplete(steamId);
	}

	/// <summary>
	/// Host only: the in-session repair set for ONE member that stays in the
	/// world — the absolute, idempotent in-world tables, re-sent on the host's
	/// periodic cycle (the cadence and the in-world filter live in the adapter,
	/// which owns Unity time). Deliberately a SUBSET of <see cref="Send"/>: the
	/// item snapshot stays an entry-only one-shot (its rows are reconciled
	/// continuously by the item keyframe), the radiation line state is a scalar
	/// re-sent on its own generation/entry edges, and the completion marker is an
	/// entry-group contract.
	///
	/// The ENEMY snapshot rides here since the N1 recovery (2026-09-18): a
	/// repeat snapshot is idempotent because the guest pairs its frozen copies on
	/// the host's bind-time spawn anchor rather than on the live position, so a
	/// repair landing long after generation still binds (before the anchor it
	/// could not pair at all, and a failed pairing even cleared the guest's
	/// mapping flag).
	///
	/// Adding an absolute in-world table means adding it HERE as well: a
	/// swallowed entry send has no second chance until the next world-entry
	/// edge, which is the gap the trap-layout snapshot sat in (the sync-coverage
	/// audit's W6 row) and the enemy snapshot sat in (N1). The player roster
	/// rides here for the same reason (row R3): its entry-time writes live in
	/// <c>EntitySyncService.StartMemberSync</c>, and this group is its absolute
	/// re-send — the same entity ids, which the receiver absorbs by identity.
	/// </summary>
	public void SendInSessionRepair(ulong steamId)
	{
		_log.LogInformation("Sending the in-session repair group to {Peer}.", steamId);

		RefreshTrapLayout();

		// Same rule as the entry group: the run-baseline carrier first, then the
		// generation-stamped absolute tables it is compared against.
		_kernelProtocol.SendCheckpoint(steamId);
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_enemies.SendEnemySnapshot(steamId);
		_world.SendRuntimeEntitySnapshot(steamId);
		_craft.SendRecipeUnlockSnapshot(steamId);
		_entities.ResendRoster(steamId); // the roster is an absolute table too: a swallowed PlayerJoin (self-activation or a third party's row) converges here
	}

	/// <summary>
	/// The layout table is re-derived from the LIVE scene before a group reads it.
	/// The table is only as fresh as its last scan, so between two scans it can
	/// still list an entity the world has since removed (a self-destructed turret,
	/// a broken crystal) — and the member-side apply is an absolute align, so a
	/// member that enters inside that window materializes a phantom the host does
	/// not own. Both send points call it, so the freshness belongs to the SEND
	/// and not to one caller: the entry edge, the reconnect handshake, the
	/// entry-window repeat repair and the periodic wave are covered by construction.
	///
	/// The port is the adapter's live-scene scan (the Runtime cannot see the scene)
	/// and is OPTIONAL: a composition without one sends the table as last derived.
	/// It is safe to call per member — the adapter scans at most once per frame, so
	/// a wave costs ONE scan however many in-world members it heals.
	/// </summary>
	private void RefreshTrapLayout() => _liveLayout?.RefreshFromLiveScene();
}
