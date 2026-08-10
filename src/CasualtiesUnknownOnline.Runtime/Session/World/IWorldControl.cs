using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world surface packet handlers operate on — implemented by WorldService.
/// Handlers depend on this narrow interface instead of the concrete service,
/// which keeps the constructor graph acyclic (abstract extraction, user rule).
/// </summary>
public interface IWorldControl
{
	WorldStartParams? WorldParams { get; set; }

	/// <summary>Host only: a run started (click moment) but the host is not in the world yet — mid-generation handshakes may follow immediately.</summary>
	bool HostRunPending { get; }

	void SetHostRunPending(bool pending);

	/// <summary>Host: a guest reported damage (sender = the reporter; drops ride the break — the host arbitrates). Guest: the host broadcast it.</summary>
	void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, IReadOnlyList<BlockDropEntryMsg>? drops);

	event Action<ulong, NetVector2, float, IReadOnlyList<BlockDropEntryMsg>?>? BlockDamagedReceived;

	/// <summary>Report a locally-performed block damage (drops = the break's drops, null/empty = damage only): guest → host report, host → broadcast to all synced members.</summary>
	void SendBlockDamaged(NetVector2 worldPos, float damage, IReadOnlyList<BlockDropEntryMsg>? drops);

	/// <summary>Host only: relay an ACCEPTED guest break report to the other members (source excluded).</summary>
	void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, IReadOnlyList<BlockDropEntryMsg>? drops);

	void FireWorldJoinReceived(bool isTutorial);

	event Action<bool>? WorldJoinReceived;

	/// <summary>
	/// Report a locally-performed player attack on a building entity (local
	/// compute): guest → host as a report (the host applies the damage to its
	/// own copy — which rolls the host-side entity drops — and relays), host →
	/// guest as a broadcast relay. The entity is identified by world position
	/// (world entities are generated deterministically on both sides).
	/// </summary>
	void SendBuildingEntityDamaged(NetVector2 pos, float damage);

	/// <summary>Guest: a block was placed locally — report it to the host (host arbitrates + relays).</summary>
	void SendBlockPlacedReport(int x, int y, ushort block);

	/// <summary>Host only: broadcast a placed block (source excluded — it already placed locally).</summary>
	void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block);

	void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block);

	event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage);

	/// <summary>A player's attack damaged a building entity — apply the damage to the entity at Pos.</summary>
	event Action<NetVector2, float>? BuildingEntityDamagedReceived;

	/// <summary>
	/// Report a locally-opened lockable entity (instant-open/lockpick/keypad —
	/// all write health = 0 directly): guest → host as a report (the host applies
	/// the open to its copy, which rolls the host-side drops, and relays), host →
	/// guest as a broadcast relay.
	/// </summary>
	void SendBuildingEntityOpened(NetVector2 pos);

	void FireBuildingEntityOpenedReceived(NetVector2 pos);

	/// <summary>A lockable entity was opened — apply the open (health = 0) to the entity at Pos.</summary>
	event Action<NetVector2>? BuildingEntityOpenedReceived;

	/// <summary>Host only: everyone enters the world together — arm the start gate (waits for every guest's InWorld, or 30 s). Returns whether anyone is being waited on.</summary>
	bool StartStartGate();

	/// <summary>Host only: a member finished loading (InWorld) — release the gate when all are in, or let a late joiner pass directly.</summary>
	void NotifyMemberInWorld(ulong steamId);

	/// <summary>Host only: the gate is armed (the host is waiting too) — driver pumps this for the 30 s fallback.</summary>
	void MaybeForceStartGate();

	/// <summary>Host only: true while the host itself must wait (frozen + overlay).</summary>
	bool StartGateActive { get; }

	/// <summary>Host only: seconds left until the gate force-releases (0 when not armed).</summary>
	int StartGateRemainingMs { get; }

	void FireWorldReadyReceived();

	event Action? WorldReadyReceived;

	/// <summary>Host only: a block changed after generation (mined/destroyed/built) — upsert it into the damage table.</summary>
	void ReportBlockState(int x, int y, ushort block);

	/// <summary>Host only: a block was restored to its generated baseline — drop it from the damage table.</summary>
	void RemoveBlockState(int x, int y);

	/// <summary>Host only: a new world layer is generating — the damage table starts empty again.</summary>
	void ResetDamagedBlocks();

	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	void SendBlockStateSnapshot(ulong targetSteamId);

	void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks);

	event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	/// <summary>Host only: an earthquake began — tell the guests to show the effect and re-align their quake timer (timing is synced to the host; every side still breaks its own region, the regions union via the air-write relay).</summary>
	void BroadcastEarthquakeStart(float duration, float nextDelay);

	/// <summary>Guest side: an earthquake began (host timing) — show the effect, re-align the local quake timer.</summary>
	void FireEarthquakeStartReceived(float duration, float nextDelay);

	event Action<float, float>? EarthquakeStartReceived;

	/// <summary>Host only: broadcast the keypad codes (position-keyed Openables) — the game lazy-generates per side otherwise (two codes).</summary>
	void SendKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes);

	/// <summary>Guest: the host's keypad codes arrived — write them onto the local Openables.</summary>
	void FireKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes);

	event Action<IReadOnlyList<KeypadEntryMsg>>? KeypadCodeReceived;

	/// <summary>
	/// Report a locally-triggered world entity event (a trap fired — local
	/// compute): guest → host as a report (the host applies the event to its own
	/// world and relays), host → broadcast to all synced members.
	/// </summary>
	void SendEntityEvent(EntityEventMsg msg);

	/// <summary>Host only: relay an accepted entity event to the other members (source excluded — it already applied locally).</summary>
	void BroadcastEntityEvent(ulong excludeSteamId, EntityEventMsg msg);

	void FireEntityEventReceived(ulong sender, EntityEventMsg msg);

	/// <summary>An entity event arrived — the receiver applies it (host: to its own world; guest: replay).</summary>
	event Action<ulong, EntityEventMsg>? EntityEventReceived;

	/// <summary>Host only: record a one-shot trap consumption (position-keyed; Extra rides along for progress-carrying events).</summary>
	void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra);

	/// <summary>Host only: send the one-shot trap consumptions to one member (on its world entry).</summary>
	void SendTrapStateSnapshot(ulong targetSteamId);

	void FireTrapStateReceived(IReadOnlyList<EntityEventMsg> consumed);

	/// <summary>Guest: the host's trap-consumption snapshot arrived — consume each entry (idempotent).</summary>
	event Action<IReadOnlyList<EntityEventMsg>>? TrapStateReceived;
}
