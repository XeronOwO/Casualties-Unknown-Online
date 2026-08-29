namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// CUO wire message IDs. Frame format over SteamTransport: [msgId:1][payload],
/// payload length is implicit (rest of the frame). Payloads are protobuf-net
/// encoded (Protocol/Messages/).
/// </summary>
public enum NetMsg : byte
{
	// Diagnostics
	Ping = 1,
	Pong = 2,

	// Session control
	Handshake = 16,
	HandshakeAck = 17, // host → guest: acknowledges every handshake, even repeats (lazy Steam P2P sessions swallow early messages)
	HandshakeAckAck = 58, // guest → host: handshake end-to-end confirmation (the ack arrived — the host only marks the member Handshaken on this)
	SceneState = 18,
	WorldJoin = 20, // host → guest: start loading the world (sent at generation start; the run baseline rides KernelEnvelope/checkpoint)
	WorldReady = 21, // host → guest: everyone finished loading — start playing (start-gate release / late-joiner pass)

	// Entities
	PlayerJoin = 32, // host → guest: self-activation + roster announcement
	PlayerLeave = 33, // host → guest: a synced member left the session
	PlayerState = 35, // host → guest: authoritative entity batch (unreliable stream)
	PlayerStateReport = 36, // guest → host: local authoritative position (no host-side simulation)

	// Character data (guest → host reports, host → guest restore on reconnect)
	CharacterData = 37,
	HostCharacterData = 53, // host → guest: the host's own 1 Hz character snapshot (clone inventory rendering)

	// World mutations (local compute, remote verify/sync)
	BlockDamaged = 40, // guest → host: report (host arbitrates); host → guest: broadcast relay (source excluded)
	WorldBlockState = 41, // host → guest: full block-state snapshot (damage table) on world entry
	BlockPlaced = 42, // guest → host: report (host arbitrates); host → guest: broadcast relay (source excluded)

	// Item-refusal feedback (block-break drops still ride this one frame; item
	// kernel rejections use KernelEnvelope CommandRejected)
	ItemReject = 49, // host → guest: block-break drop refusal — the guest destroys its local copy

	// World entities (player-attacked building entities — plants, crates, creatures)
	BuildingEntityDamaged = 51, // guest → host: report (host applies + relays); host → guest: broadcast relay (source excluded)
	BuildingEntityOpened = 52, // guest → host: a crate/lock was opened (health = 0 write path); host → guest: broadcast relay (source excluded)

	// World events (host authority)
	EarthquakeStart = 55, // host → guest: an earthquake began (Duration seconds) — guests show the effect and suppress their own independent quake (four independent quakes would strip the terrain)
	KeypadCode = 57, // host → guest: the keypad codes (airdrop crates etc.) — generated host-side at world entry (the game lazy-generates them per side on first use, Openable.cs:19, which would give every side its own code)

	// Item id coordination (guest → host reports / host → guest grants)
	ItemIdWatermark = 64, // bidirectional: guest → host the counter it allocated up to; host → guest the grant it must resume from (a crashed-and-rejoined guest's counter restarts — the grant keeps its new ids from colliding with the old ones the host still holds)
	CarriedInventory = 65, // guest → host: the guest's carried inventory with self-assigned ids (generation finished) — the host registers it in the guest's transfer table

	// World entity events (traps/mechanisms — local compute → report → host applies → relay, replay on the receivers)
	EntityEvent = 66, // bidirectional: guest → host report of a triggered trap event; host → guest broadcast relay (source excluded — the host applies the event to its own world first)
	TrapStateSnapshot = 67, // host → guest: the one-shot trap consumptions so far (world entry, sent alongside the block-state snapshot)

	// World entity creation (runtime, outside generation — the spawn command)
	EntitySpawned = 68, // bidirectional: the creating side reports (keeps its local copy); the host creates its own and relays (source excluded) — items ride the item domain, entities ride this

	// World entity initial conditions (host authority)
	GeyserStateSnapshot = 69, // host → guest: the geysers' liquid types — rolled per-side at generation from the PUBLIC random stream (GeyserScript.cs:12, outside the isolated generation stream), so the host's roll is the authority; world entry + 60 s re-send, idempotent

	// World fluid grid (host authority — the host simulates the world fluid alone, the guests render the streamed regions)
	FluidRegion = 70, // host → guest (unreliable stream): an absolute RLE snapshot of a grid region around the member — 10 Hz changed-box diff + 1 Hz full-viewport fallback
	FluidInteraction = 71, // bidirectional: guest → host report of a consumed cell (drinking); host → guest broadcast relay (source excluded — the host executes on its own grid first)

	// Trade (host authority — the trader's state is host-computed, the acting side's local effects stay local)
	TraderState = 72, // host → guest: a trader's full authoritative state + stock — on every interaction, on world entry, and every 5 s (unreliable fallback); a full overwrite
	TraderAction = 73, // guest → host: a locally-executed trader interaction (purchase/give/haggle/threaten/hug/move/meet) — the host executes the trader-side change and broadcasts the state

	// Speech (the Talker domain — the bubble text is DATA: the speaking side applied localization + random + distortion, the receiver only displays)
	SpeechMsg = 74, // bidirectional: guest → host report of a spoken bubble; host → guest broadcast relay (the source excluded for players — a trader's bubble is host-broadcast)

	// Mod messages (Phase 4 Mod API — the shared mod-message frame: the payload
	// carries the sending mod's id + a raw payload; the receiving side routes by
	// id to the locally-loaded mod, unknown ids are dropped with a log. Report/
	// 定向 semantics, star topology, NO auto-relay — a guest's report reaches the
	// host's copy of the mod only; broadcasting is the host-side mod's explicit call)
	ModMessage = 75, // bidirectional: guest → host report; host → guest directed/broadcast

	// Crafting (the crafting domain — ONE crafting operation = ONE report carrying
	// its complete terminal state: the consumed/changed materials and the products.
	// The host applies per entry and relays the WHOLE report — never decomposed
	// into per-entry broadcasts, so one-operation-one-report holds end-to-end)
	CraftReport = 76, // bidirectional: guest → host report; host → guest broadcast relay (source excluded)
	RecipeUnlock = 77, // bidirectional: guest → host report of a blueprint unlock; host → guest broadcast relay (source excluded)

	// Opened lockable entities (the late-joiner snapshot — an open is a one-shot
	// write with no re-open, so a rejoin must learn the opens from the host)
	OpenedEntitiesSnapshot = 78, // host → guest: the opened entities' positions so far (world entry, sent alongside the block-state and trap-state snapshots)

	// The host's authoritative trap layout (the generated trap entities'
	// positions — the game's entity distribution runs physics queries the
	// random-stream isolation does not cover, so the sides' layouts diverge)
	TrapLayoutSnapshot = 79, // host → guest: the layout entries (world entry, sent alongside the block-state / trap-state / opened-entities snapshots)

	// Enemies/NPCs (host authority — the host simulates the AI + physics, the
	// guests render the frozen copies from the snapshot; same pattern as the
	// player entity stream)
	EnemyState = 80, // host → guest (unreliable): the authoritative enemy-state batch (20 Hz, seq-gated)
	EnemySnapshot = 81, // host → guest: the full enemy snapshot (world entry / late joiner — ids + spawn positions for binding + RuntimeSpawns for materializing runtime-created enemies)
	EnemyBite = 82, // bidirectional: guest → host report (the victim's local bite already applied); host → guest broadcast relay (source excluded) — an enemy bit a player, carrying the post-bite limb + body state
	EnemyAttack = 83, // host → guest: the host's enemy simulation decided an attack on a remote player (the victim applies it locally and reports the terminal state)
	EnemyLunge = 84, // bidirectional: guest → host report (the victim's local lunge already applied); host → guest broadcast relay (source excluded) — a crystal lunge hit a player, carrying the post-lunge limb + body state
	EnemyEffect = 85, // bidirectional: guest → host report (the victim's local proximity effect already applied); host → guest broadcast relay (source excluded) — ElderThornback/Xaloris/GrabberPlant proximity side effects, carrying the post-effect body state

	// Mod host commands (Phase 4b Mod API — command execution is host-authoritative:
	// the guest only sends the request, the host executes its own copy of the mod
	// and answers with a directed result)
	ModCommandRequest = 86, // guest → host: invoke a registered mod command
	ModCommandResult = 87, // host → guest: the command result (directed to the requester)

	// Damaged building entities (the late-joiner snapshot — live damage is a
	// position-keyed relay, but a late joiner regenerates every entity at full
	// health, so it must learn the host's current entity health)
	BuildingEntityHealthSnapshot = 88, // host → guest: current building-entity health records (world entry / 60 s resend)

	// Partially-damaged blocks (the late-joiner snapshot — the live BlockDamaged
	// relay is delta-based, but a late joiner regenerates every block with zero
	// accumulated BlockDamage, so it must learn the host's current damage; a
	// broken block rides WorldBlockState instead)
	BlockDamageSnapshot = 89, // host → guest: current partial block-damage records (world entry / 60 s resend)

	// World time (host authority — Time.timeScale is process-global world state:
	// guests request speed changes, the host applies the policy and broadcasts
	// the authoritative speed; the all-unconscious sleep acceleration is
	// host-computed, never per-side)
	WorldTimeRequest = 90, // guest → host: request Normal/Fast/SuperFast
	WorldTime = 91, // host → guest: the authoritative world-time speed (change / world entry / 5 s resend)

	// Limb presentation (local compute → report → apply → fan-out: a limb's
	// latch changed on its owner's local simulation — break/mend/dismember)
	LimbStateEvent = 93, // bidirectional: guest → host report of the owner's own limb latch; host → guest broadcast relay (source excluded) — carries the body's full post-event limb + health terminal state

	// Character action presentation (one-shot event: the owner's action already
	// played its sound locally, the peers replay it on the owner's clone; GunFire
	// also carries the recoil kick so the clone's weapon visibly kicks)
	CharacterSound = 94, // bidirectional: guest → host report of the owner's own action event; host → guest broadcast relay (source excluded)

	// Fluid presentation (host authority — the host simulates the world fluid
	// alone, so the transient water-push physics and waterflow sounds that the
	// host's SimulationStep produces are sent to the guests as dedicated events)
	FluidPresentation = 96, // host → guest (reliable): one water push / waterflow sound at a grid cell

	// Direct player interaction — take items from another player (host
	// authority: the host owns the cross-player inventory transfer; the two
	// participants apply the authoritative body mutation locally)
	PlayerInventoryTakeRequest = 97, // guest → host: take one carried item from another in-world player
	PlayerInventoryTransfer = 98, // host → participant: the authoritative transfer result (remove from FromSteamId, add to ToSteamId)

	// Direct player interaction — carry/release another player (host
	// authority: the host validates the carryable state, records the one
	// carrier/one carried relation and broadcasts the authoritative state;
	// the carried player's client drives its own body to follow the carrier)
	PlayerCarryStartRequest = 99, // guest → host: start carrying an unconscious/dead in-world player
	PlayerCarryStopRequest = 100, // guest → host: stop carrying the current carried player
	PlayerCarryState = 101, // host → all: authoritative carry relation changed (CarriedSteamId = 0 means released)

	// Direct player interaction — heal another player (host authority: the host
	// validates the healer/target snapshots, consumes the healer's medical item,
	// applies the healing effect to the target's authority and tells the two
	// participants the exact post-heal state)
	PlayerHealRequest = 102, // guest → host: use a carried medical item on another in-world player (ItemInstanceId 0 = host auto-select)
	PlayerHealResult = 103, // host → participants: authoritative heal result (item consumed/destroyed + target health/limbs)

	// Tutorial claw presentation (host authority — the claw's 20 Hz flow: the
	// host's TutorialHandler is the single live rig; a guest not running its own
	// course renders the same handPos/handPosCurrent from this absolute stream)
	TutorialClawState = 104, // host → guest (unreliable): the tutorial-claw presentation snapshot (20 Hz, seq-gated)

	// Player-item explosions (local compute → report → host applies + relays →
	// receiver replays the body/visual segment; the terrain/building/item facts
	// ride the existing block/building/world-item channels)
	DynamiteExplosion = 105, // bidirectional: guest → host report of a dynamite detonation; host → guest broadcast relay (source excluded)

	// World event state (host authority — the radiation line's active/timeGone
	// world state is host-owned; guests apply the absolute state and only run
	// the local per-frame presentation/effects between resends)
	RadiationLineState = 106, // host → guest: the authoritative radiation-line state (Active + TimeGone)

	// Trader recruit (host-authoritative co-op revive — the acting side only
	// sends the request; the host validates the trader/player gates and sends
	// the revived physiological state directly to the target)
	TraderRecruitRequest = 107, // guest → host: recruit a dead in-world player at a nearby trader
	TraderRecruitResult = 108, // host → target: the authoritative post-revive body state

	// Text chat (star relay — a guest reports its own line to the host, the
	// host validates and broadcasts to the other members; the same payload is
	// used both up and down)
	Chat = 109, // bidirectional: guest → host report, host → guest relay

	// World-entry snapshot group completion (host authority — the world-entry
	// fan-out sends many independent snapshot messages; this one is the explicit
	// end-of-group marker so a receiver never mistakes a partial best-effort
	// state for the full authoritative state)
	WorldSnapshotComplete = 110, // host → guest: the world-entry snapshot group is complete

	// Host administration (host authority — only the host may remove a guest
	// from the session; the target is told directly so it can tear down instead
	// of waiting for the host to disappear)
	Kicked = 111, // host → guest: this member was kicked by the host
	Banned = 112, // host → guest: this member was banned by the host (persisted on the host; future handshakes are rejected)

	// Player attack-animation presentation (one-shot: the owner's local
	// Body.Attack already instantiated the attackAnim prefab, the peers replay
	// it on the owner's clone with the same prefab/facing/direction)
	CharacterAttackAnim = 113, // bidirectional: guest → host report of the owner's own attack anim; host → guest broadcast relay (source excluded)

	// Player landing presentation (one-shot: the owner's local HandleGroundedState
	// already played the Grounded clip and spawned the landing dust; the peers
	// replay the same clip/dust on the owner's clone)
	CharacterLandingVisual = 114, // bidirectional: guest → host report of the owner's own landing visual; host → guest broadcast relay (source excluded)

	// Trader hostile swing presentation (one-shot: the acting side's local
	// trader already swung at that side's local player; the peers replay the
	// same attackAnimation on their same-position trader)
	TraderSwing = 115, // bidirectional: guest → host report of a local trader swing; host → guest broadcast relay (source excluded)

	// Direct player interaction — use a carried consumable on another player
	// (host authority: the host validates the user/target snapshots, consumes
	// or drains the item, applies the target-side body effect to the authority
	// and tells the two participants the exact post-use state)
	PlayerItemUseRequest = 116, // guest → host: use a carried drink/food on another in-world player (ItemInstanceId 0 = host auto-select)
	PlayerItemUseResult = 117, // host → participants: authoritative consumable-use result (item consumed/destroyed + target health/limbs)

	// Direct player interaction — push/shove another player (host authority:
	// the host validates distance/standing/cooldown, computes the force from
	// the authoritative entity positions and broadcasts one committed result;
	// the target's own client applies the native ragdoll/velocity locally)
	PlayerPushRequest = 118, // guest → host: push an in-world player
	PlayerPushResult = 119, // host → all: authoritative push result (force delta)

	// Player ragdoll presentation (one-shot: the owner's local Body.Ragdoll
	// transition already collapsed the body; the peers replay the lying pose on
	// the owner's clone immediately, with the 20 Hz standing state as fallback)
	CharacterRagdoll = 120, // bidirectional: guest → host report of the owner's own ragdoll toggle; host → guest broadcast relay (source excluded)

	// Player world-blood presentation (one-shot transient decal: the owner's
	// local BleedParticle already spawned the ground/wall blood object; the
	// peers replay the same decal at the same world position)
	WorldBloodSpawn = 121, // bidirectional: guest → host report of a local player's blood decal; host → guest broadcast relay (source excluded)

	// Phase C kernel envelope transport (the frame payload is a ProtocolFrame:
	// CommandEnvelope / CommittedBatchEnvelope / CheckpointEnvelope /
	// StateStreamEnvelope)
	KernelEnvelope = 122, // bidirectional: the four-envelope kernel protocol rides one message id
}
