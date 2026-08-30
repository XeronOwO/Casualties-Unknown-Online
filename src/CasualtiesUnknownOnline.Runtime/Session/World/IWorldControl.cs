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

	/// <summary>Host only: publish the world-start parameters as the generation baseline (captured at the host's click/boundary moment).</summary>
	void PublishWorldParams(WorldStartParams parameters);

	/// <summary>Host: tell every synced member to follow the run (the guest enters the world on this instruction).</summary>
	void SendWorldJoin(bool isTutorial);

	/// <summary>Host: send a targeted world-join instruction (a reconnecting member's re-entry).</summary>
	void SendWorldJoinTo(ulong steamId);

	/// <summary>Host only: a run started (click moment) but the host is not in the world yet — mid-generation handshakes may follow immediately.</summary>
	bool HostRunPending { get; }

	void SetHostRunPending(bool pending);

	/// <summary>Host: a guest reported damage (sender = the reporter; drops ride the break — the host arbitrates; MetalBonus preserves the ×10 metallic multiplier). Guest: the host broadcast it.</summary>
	void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops);

	event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?>? BlockDamagedReceived;

	/// <summary>Report a locally-performed block damage (drops = the break's drops, null/empty = damage only): guest → host report, host → broadcast to all synced members.</summary>
	void SendBlockDamaged(NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops);

	/// <summary>Host only: relay an ACCEPTED guest break report to the other members (source excluded).</summary>
	void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops);

	void FireWorldJoinReceived(bool isTutorial);

	event Action<bool>? WorldJoinReceived;

	/// <summary>
	/// Report a locally-performed player attack on a building entity (local
	/// compute): guest → host as a report (the host applies the damage to its
	/// own copy — which rolls the host-side entity drops — and relays), host →
	/// guest as a broadcast relay. The entity is identified by world position
	/// (world entities are generated deterministically on both sides).
	/// <paramref name="playHitSound"/> is true for attack damage (the receiver
	/// replays the entity's own hitSound) and false for silent damage sources
	/// such as cactus collision self-damage (the trigger side plays only the
	/// player-local gore sound, never the entity hitSound).
	/// </summary>
	void SendBuildingEntityDamaged(NetVector2 pos, float damage, bool playHitSound = true);

	/// <summary>Guest: a block was placed locally — report it to the host (host arbitrates + relays).</summary>
	void SendBlockPlacedReport(int x, int y, ushort block);

	/// <summary>Host only: broadcast a placed block (source excluded — it already placed locally).</summary>
	void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block);

	void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block);

	event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage, bool playHitSound);

	/// <summary>A building entity was damaged — apply the damage to the entity at Pos. If <c>playHitSound</c> is true the receiver also replays the entity's own hitSound (attack damage); silent damage sources pass false.</summary>
	event Action<NetVector2, float, bool>? BuildingEntityDamagedReceived;

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

	/// <summary>Host only: send the world-entry snapshot-complete marker to one member (sent after the full snapshot group).</summary>
	void SendWorldSnapshotComplete(ulong targetSteamId);

	/// <summary>Guest: the host's world-entry snapshot-complete marker arrived — the full snapshot group has been received.</summary>
	void FireWorldSnapshotCompleteReceived();

	event Action? WorldSnapshotCompleteReceived;

	/// <summary>Host only: a block changed after generation (mined/destroyed/built) — upsert it into the damage table.</summary>
	void ReportBlockState(int x, int y, ushort block);

	/// <summary>Host only: a block was restored to its generated baseline — drop it from the damage table.</summary>
	void RemoveBlockState(int x, int y);

	/// <summary>Host only: a new world layer is generating — the damage table starts empty again.</summary>
	void ResetDamagedBlocks();

	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	void SendBlockStateSnapshot(ulong targetSteamId);

	/// <summary>Host only: record the block's current accumulated damage at a block cell (the late-joiner snapshot's fact source).</summary>
	void ReportBlockDamage(int x, int y, float damage);

	/// <summary>Host only: the block broke or was air-written away — its partial damage is gone.</summary>
	void RemoveBlockDamage(int x, int y);

	/// <summary>Host only: send the partial block-damage records to one member (on its world entry).</summary>
	void SendBlockDamageSnapshot(ulong targetSteamId);

	/// <summary>Guest: the host's partial block-damage snapshot arrived — apply each entry absolutely (world entry / 60 s resend).</summary>
	void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries);

	event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

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

	/// <summary>Host only: broadcast the geyser liquid types (position-keyed GeyserScripts — rolled per-side at generation from the public random stream, the host's roll is the authority).</summary>
	void SendGeyserStateSnapshot(IReadOnlyList<GeyserStateEntryMsg> geysers);

	/// <summary>Guest: the host's geyser liquid types arrived — write them onto the local GeyserScripts.</summary>
	void FireGeyserStateReceived(IReadOnlyList<GeyserStateEntryMsg> geysers);

	event Action<IReadOnlyList<GeyserStateEntryMsg>>? GeyserStateReceived;

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

	/// <summary>
	/// Report a locally-detonated player-item explosion (dynamite): guest → host
	/// as a report (the host applies the explosion to its own world and relays),
	/// host → broadcast to all synced members. The trigger side's own world
	/// consequences already ran and ride the block/building/item channels; this
	/// event carries the one-shot item id and the detonation position for the
	/// host's apply, the peers' body/visual replay and duplicate suppression.
	/// </summary>
	void SendDynamiteExplosion(ulong itemInstanceId, NetVector2 position);

	/// <summary>Host only: relay an accepted player-item explosion to the other members (source excluded — it already applied locally).</summary>
	void BroadcastDynamiteExplosion(ulong excludeSteamId, ulong itemInstanceId, NetVector2 position);

	void FireDynamiteExplosionReceived(ulong sender, ulong itemInstanceId, NetVector2 position);

	/// <summary>A player-item explosion arrived — the receiver applies it (host: to its own world; guest: replay body/visual).</summary>
	event Action<ulong, ulong, NetVector2>? DynamiteExplosionReceived;

	/// <summary>
	/// Report a locally-spawned player world-blood decal: guest → host as a
	/// report (the host replays it on its own world and relays), host →
	/// broadcast to all synced members. The reporting player's own client
	/// already spawned the transient decal locally.
	/// </summary>
	void SendWorldBloodSpawn(WorldBloodSpawnMsg msg);

	/// <summary>Host only: relay an accepted world-blood decal to the other members (source excluded — it already spawned locally).</summary>
	void BroadcastWorldBloodSpawn(ulong excludeSteamId, WorldBloodSpawnMsg msg);

	void FireWorldBloodSpawnReceived(ulong sender, WorldBloodSpawnMsg msg);

	/// <summary>A world-blood decal arrived — the receiver replays it (host: after a guest report; guest: the host's relay).</summary>
	event Action<ulong, WorldBloodSpawnMsg>? WorldBloodSpawnReceived;

	/// <summary>Host only: record a one-shot trap consumption (position-keyed; Extra rides along for progress-carrying events).</summary>
	void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra);

	/// <summary>Host only: record a stateful trap edge into the kernel trap state machine.</summary>
	void ReportTrapState(EntityEventKind kind, float x, float y, byte extra);

	/// <summary>
	/// Report a runtime world-entity creation (outside generation — the spawn
	/// command): guest → host as a report (the host creates its own copy and
	/// relays), host → broadcast to all synced members.
	/// </summary>
	void SendEntitySpawned(EntitySpawnedMsg msg);

	/// <summary>Host only: relay an accepted entity creation to the other members (source excluded — it already created locally).</summary>
	void BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg);

	void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg);

	/// <summary>An entity-creation report arrived — the receiver creates its own copy (host: then relays; guest: remote apply).</summary>
	event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived;

	/// <summary>Host only: record an opened lockable entity at a world position (a kernel WorldEntities fact).</summary>
	void ReportOpenedEntity(float x, float y);

	/// <summary>Host only: record a damaged building entity's current health at a world position (a kernel WorldEntities fact).</summary>
	void ReportBuildingEntityHealth(float x, float y, float health);

	/// <summary>Host only: record one generated trap entity (the adapter's scanner reports it on the generation-finished edge).</summary>
	void ReportTrapLayout(EntityEventKind kind, float x, float y, string prefabName);

	/// <summary>Host only: send the trap layout to one member (on its world entry).</summary>
	void SendTrapLayoutSnapshot(ulong targetSteamId);

	/// <summary>Guest: the host's trap layout arrived — align the local world (materialize missing, destroy surplus).</summary>
	void FireTrapLayoutReceived(IReadOnlyList<TrapLayoutEntryMsg> entries);

	event Action<IReadOnlyList<TrapLayoutEntryMsg>>? TrapLayoutReceived;

	/// <summary>Host only: commit a coarse fluid-region summary (world chunk totals/types) into the kernel fluid checkpoint.</summary>
	void ReportFluidRegions(IReadOnlyList<FluidRegionSummary> regions);

	/// <summary>Host only: stream an absolute RLE fluid-grid region to one member (the host simulates the world fluid alone — the guests render the streamed regions).</summary>
	void SendFluidRegion(ulong targetSteamId, FluidRegionMsg msg);

	/// <summary>Guest: the host's fluid region arrived — apply it onto the local grid.</summary>
	void FireFluidRegionReceived(FluidRegionMsg msg);

	event Action<FluidRegionMsg>? FluidRegionReceived;

	/// <summary>
	/// Report a locally-performed fluid interaction (drinking — the cell was
	/// consumed): guest → host as a report (the host executes on its own grid
	/// and relays), host → broadcast to all synced members.
	/// </summary>
	void SendFluidInteraction(FluidInteractionMsg msg);

	/// <summary>Host only: relay an executed fluid interaction to the other members (source excluded — it already applied locally).</summary>
	void BroadcastFluidInteraction(ulong excludeSteamId, FluidInteractionMsg msg);

	void FireFluidInteractionReceived(ulong sender, FluidInteractionMsg msg);

	/// <summary>A fluid interaction arrived — the receiver applies it (host: to its own grid, then relays; guest: clear the cell).</summary>
	event Action<ulong, FluidInteractionMsg>? FluidInteractionReceived;

	/// <summary>Host only: send one transient fluid-presentation event (water push / waterflow sound) to one member.</summary>
	void SendFluidPresentation(ulong targetSteamId, FluidPresentationMsg msg);

	/// <summary>Guest: the host's fluid-presentation event arrived — replay the transient water push / waterflow sound.</summary>
	void FireFluidPresentationReceived(FluidPresentationMsg msg);

	event Action<FluidPresentationMsg>? FluidPresentationReceived;

	/// <summary>Host only: send one trader's authoritative state to one member (world entry, the 5 s fallback).</summary>
	void SendTraderState(ulong targetSteamId, TraderStateMsg msg);

	/// <summary>Host only: broadcast one trader's authoritative state to every member (an interaction just changed it — the acting side included, its local state was provisional).</summary>
	void BroadcastTraderState(TraderStateMsg msg);

	/// <summary>Guest: a trader's authoritative state arrived — apply the full overwrite.</summary>
	void FireTraderStateReceived(TraderStateMsg msg);

	event Action<TraderStateMsg>? TraderStateReceived;

	/// <summary>Report a locally-executed trader interaction (guest → host — the host executes the trader-side change and broadcasts the state).</summary>
	void SendTraderAction(TraderActionMsg msg);

	/// <summary>Host: a guest's trader interaction arrived — execute the trader-side change and broadcast the state.</summary>
	void FireTraderActionReceived(ulong sender, TraderActionMsg msg);

	event Action<ulong, TraderActionMsg>? TraderActionReceived;

	/// <summary>Guest: send a trader-recruit request (the acting side has already
	/// located its nearest trader; the host owns the trade gates + the revive).</summary>
	void SendTraderRecruitRequest(TraderRecruitRequestMsg msg);

	/// <summary>Host: a guest's trader-recruit request arrived.</summary>
	void FireTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg);

	event Action<ulong, TraderRecruitRequestMsg>? TraderRecruitRequestReceived;

	/// <summary>Host only: send the authoritative post-revive body state to the revived player.</summary>
	void SendTraderRecruitResult(ulong targetSteamId, TraderRecruitResultMsg msg);

	/// <summary>Guest: the host's trader-recruit result arrived — apply the revive to the local body.</summary>
	void FireTraderRecruitResultReceived(TraderRecruitResultMsg msg);

	event Action<TraderRecruitResultMsg>? TraderRecruitResultReceived;

	/// <summary>Report/broadcast a hostile trader swing presentation (guest → host report; host → all guests).</summary>
	void SendTraderSwing(TraderSwingMsg msg);

	/// <summary>A hostile trader swing arrived (report or relay) — the receiver replays the animation on its same-position trader.</summary>
	void FireTraderSwingReceived(ulong sender, TraderSwingMsg msg);

	event Action<ulong, TraderSwingMsg>? TraderSwingReceived;

	/// <summary>Guest: report a locally-spoken player bubble to the host.</summary>
	void SendSpeech(SpeechMsg msg);

	/// <summary>Host only: fan out a bubble (0 = every member — a trader bubble; else the source excluded — a player bubble).</summary>
	void BroadcastSpeech(ulong excludeSteamId, SpeechMsg msg);

	/// <summary>A bubble arrived: a player's report on the host, a relay on the guests.</summary>
	void FireSpeechReceived(ulong sender, SpeechMsg msg);

	event Action<ulong, SpeechMsg>? SpeechReceived;

	/// <summary>Host/solo: the current authoritative radiation-line state (the
	/// active/timeGone snapshot a world-entry/reconnect fan-out sends).</summary>
	RadiationLineStateMsg? RadiationLineState { get; }

	/// <summary>Host/solo: keep the current radiation-line state as the
	/// world-entry snapshot source. No wire send; a later lobby conversion
	/// reuses this snapshot before the host's first live broadcast.</summary>
	void SetRadiationLineState(RadiationLineStateMsg state);

	/// <summary>Host only: broadcast the authoritative radiation-line state —
	/// the world boundary is host-owned and every side must see the same line.</summary>
	void BroadcastRadiationLineState(RadiationLineStateMsg state);

	/// <summary>Host only: send the current radiation-line state to one member
	/// (world entry / reconnect backfill).</summary>
	void SendRadiationLineState(ulong targetSteamId);

	/// <summary>Guest: the host's radiation-line state arrived — apply it to
	/// the local line (and let the local Update continue per-frame between
	/// resends).</summary>
	void FireRadiationLineStateReceived(RadiationLineStateMsg state);

	event Action<RadiationLineStateMsg>? RadiationLineStateReceived;

	/// <summary>Guest: report a locally-authored text-chat line to the host.</summary>
	void SendChat(ChatMsg msg);

	/// <summary>Host only: fan a chat line out to every member except the author.</summary>
	void BroadcastChat(ulong excludeSteamId, ChatMsg msg);

	/// <summary>A chat line arrived: a guest's report on the host, a relay on the guests.</summary>
	void FireChatReceived(ulong sender, ChatMsg msg);

	event Action<ulong, ChatMsg>? ChatReceived;
}
