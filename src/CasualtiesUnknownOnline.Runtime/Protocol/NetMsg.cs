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
	WorldJoin = 20, // host → guest: start loading the world (sent at generation start; the run baseline rides KernelEnvelope/checkpoint, and the run IDENTITY the member validates checkpoint sets against rides this message's RunEpoch)
	WorldReady = 21, // host → guest: everyone finished loading — start playing (start-gate release / late-joiner pass)

	// Entities
	PlayerJoin = 32, // host → guest: self-activation + roster announcement
	PlayerLeave = 33, // host → guest: a synced member left the session

	// Character data (guest → host reports, host → guest restore on reconnect)
	CharacterData = 37,
	HostCharacterData = 53, // host → guest: the host's own 1 Hz character snapshot (clone inventory rendering)

	// World mutations (local compute, remote verify/sync)
	BlockDamaged = 40, // guest → host: report (host arbitrates); host → guest: broadcast relay (source excluded)
	WorldBlockState = 41, // host → guest: full block-state snapshot (damage table) on world entry
	BlockPlaced = 42, // guest → host: report (host arbitrates; a refused report is answered with the host's cell); host → guest: relay to every member, the reporter included (its echo is the acknowledgement)

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

	// World entity creation (runtime, outside generation — the spawn command)
	EntitySpawned = 68, // bidirectional: the creating side reports (keeps its local copy); the host creates its own and relays to every member, the source included (its echo acknowledges the report) — items ride the item domain, entities ride this

	// World entity initial conditions (host authority)
	GeyserStateSnapshot = 69, // host → guest: the geysers' liquid types — rolled per-side at generation from the PUBLIC random stream (GeyserScript.cs:12, outside the isolated generation stream), so the host's roll is the authority; world entry + 60 s re-send, idempotent

	// World fluid grid (host authority — the host simulates the world fluid alone, the guests render the streamed regions)
	FluidRegion = 70, // host → guest (unreliable stream): an absolute RLE snapshot of a grid region around the member — 10 Hz changed-box diff + 1 Hz full-viewport fallback
	FluidInteraction = 71, // bidirectional: guest → host report of a consumed cell (drinking); host → guest broadcast relay (source excluded — the host executes on its own grid first)

	// Trade (host authority — the trader's state is host-computed, the acting side's local effects stay local)
	TraderState = 72, // host → guest: a trader's full authoritative state + stock — on every interaction and on a reliable 5 s base fallback (the adaptive governor may stretch it to 30 s); a full overwrite
	TraderAction = 73, // guest → host: a locally-executed trader interaction (purchase/give/haggle/threaten/hug/move/meet) — the host executes the trader-side change and broadcasts the state

	// Speech (the Talker domain — the bubble text is DATA: the speaking side applied localization + random + distortion, the receiver only displays)
	SpeechMsg = 74, // bidirectional: guest → host report of a spoken bubble; host → guest broadcast relay (the source excluded for players — a trader's bubble is host-broadcast)

	// Mod messages (Phase 4 Mod API — the shared mod-message frame: the sending
	// mod's id + an optional declared packet id + a raw payload; the receiving
	// side routes by mod id to the locally-loaded mod, and by packet id to the
	// declaration that mod registered — unknown mod ids and undeclared packet
	// ids are dropped with a log). An EMPTY packet id is the anonymous tunnel
	// form: report/定向 semantics, star topology, NO auto-relay — a guest's
	// report reaches the host's copy of the mod only; broadcasting is the
	// host-side mod's explicit call. A DECLARED packet carries its own policy,
	// so the framework relays a member's report to the other members when the
	// declaration says the other members run it.)
	ModMessage = 75, // bidirectional: guest → host report; host → guest directed/broadcast/relay

	// Crafting (the crafting domain — ONE crafting operation = ONE report carrying
	// its complete terminal state: the consumed/changed materials and the products.
	// The host applies per entry and relays the WHOLE report — never decomposed
	// into per-entry broadcasts, so one-operation-one-report holds end-to-end)
	CraftReport = 76, // bidirectional: guest → host report; host → guest broadcast relay (source excluded)
	RecipeUnlock = 77, // bidirectional: guest → host report of a blueprint unlock; host → guest broadcast relay (source excluded)

	// The host's authoritative trap layout (the generated trap entities'
	// positions — the game's entity distribution runs physics queries the
	// random-stream isolation does not cover, so the sides' layouts diverge)
	TrapLayoutSnapshot = 79, // host → guest: the layout entries (world entry, sent alongside the block-state / trap-state / opened-entities snapshots)

	// Enemies/NPCs (host authority — the host simulates the AI + physics, the
	// guests render the frozen copies from the snapshot; same pattern as the
	// player entity stream)
	EnemySnapshot = 81, // host → guest: the absolute enemy roster (world entry / late joiner / the 60 s repair — one generation-stamped row per enemy: id, prefab, match hint, state; RuntimeSpawns carries the runtime rows' creation key and tint)
	EnemyAttack = 83, // host → guest: the host's enemy simulation ANNOUNCED an attack (broadcast to every in-world guest — which enemy, which kind, the per-enemy attack seq); each guest judges on its own view whether it was hit and reports the terminal state through kernel combat-result events

	// Mod host commands (Phase 4b Mod API — command execution is host-authoritative:
	// the guest only sends the request, the host executes its own copy of the mod
	// and answers with a directed result)
	ModCommandRequest = 86, // guest → host: invoke a registered mod command
	ModCommandResult = 87, // host → guest: the command result (directed to the requester)

	// Partially-damaged blocks (the late-joiner snapshot — the live BlockDamaged
	// relay is delta-based, but a late joiner regenerates every block with zero
	// accumulated BlockDamage, so it must learn the host's current damage; a
	// broken block rides WorldBlockState instead)
	BlockDamageSnapshot = 89, // host → guest: current partial block-damage records (world entry / 60 s resend)

	// World time (host authority — Time.timeScale is process-global world state:
	// a guest applies a manual speed locally and reports it, the host arbitrates
	// accept-first and broadcasts the authoritative speed; the all-unconscious
	// sleep acceleration is host-computed, never per-side)
	WorldTimeRequest = 90, // guest → host: a speed the guest already applied locally (hotkey / native movement reset)
	WorldTime = 91, // host → guest: the authoritative world-time speed (change / request answer / world entry / 5 s resend)

	// Limb presentation and owner injury (local compute → report → apply →
	// fan-out: a limb's latch changed on its owner's local simulation — break/
	// mend/dismember — or an injury raised a limb's bleed)
	LimbStateEvent = 93, // bidirectional: guest → host report of the owner's own limb latch OR injury; host → guest broadcast relay (source excluded) — carries the body's full post-event limb + health state

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

	// Direct player interaction — carry/release another player (host
	// authority: the host validates the carryable state and commits the one
	// carrier/one carried relation as a kernel carry fact; the committed batch
	// rides KernelEnvelope and the carried player's client drives its own body
	// to follow the carrier)
	PlayerCarryStartRequest = 99, // guest → host: start carrying an unconscious/dead in-world player
	PlayerCarryStopRequest = 100, // guest → host: stop carrying the current carried player

	// Direct player interaction — heal another player (host authority: the host
	// validates the healer/target snapshots, consumes the healer's medical item,
	// applies the healing effect to the target's authority and tells the two
	// participants the exact post-heal state)
	PlayerHealRequest = 102, // guest → host: use a carried medical item on another in-world player (ItemInstanceId 0 = host auto-select)

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

	// Player presentation (cosmetic identity — no gameplay state/authority: a
	// guest's change is accepted and relayed by the host, a host change is
	// broadcast; later joiner colors also ride Handshake/PlayerJoin)
	PlayerColorUpdate = 123, // bidirectional: guest → host report of a local color change; host → guest broadcast/relay

	// Co-op location ping (transient UI presentation — no authority, no
	// persistent state: the pinger's client already rendered the marker; the
	// host fires the received event on its own world and relays to the other
	// members, source excluded)
	LocationPing = 124, // bidirectional: guest → host report of a local middle-click marker; host → guest broadcast/relay

	// Remote inventory native-intent parity (host authority: the game's own drag
	// pipeline runs on the viewer and CUO captures the mutation call it made, the
	// host validates permission/membership/ownership/destination, the owner
	// replays that native call on the real objects)
	RemoteInventoryIntentRequest = 125, // guest → host: one native inventory intent captured from the local drag release

	RemoteInventoryIntent = 126, // host → owner: replay this validated native inventory intent on the real local body

	// Remote medical operation sessions (host authority: the host owns the
	// operation registry, item/limb reservations, incremental progress and the
	// single terminal commit; intermediate updates are not final).
	MedicalOperationStartRequest = 127, // guest → host: begin a medical operation
	MedicalOperationStartAck = 128, // host → operator: accepted/rejected with the operation id
	MedicalOperationUpdate = 129, // operator → host: incremental progress delta
	MedicalOperationState = 130, // host → clients: non-terminal authoritative progress
	MedicalOperationEndRequest = 131, // operator → host: native minigame ended with exact total ml
	MedicalOperationEndCommitted = 132, // host → clients: the single terminal result
	MedicalOperationCancel = 133, // operator → host: cancel an active session

	// Runtime world-entity creation backfill (host authority — the host's
	// accepted-creation table is the entity-registration truth; a runtime
	// creation whose live report or relay was swallowed is healed absolutely)
	RuntimeEntitySnapshot = 134, // host → guest: the absolute runtime-created entity table (world entry + the 60 s cycle)

	RuntimeEntityRejected = 135, // host → reporter: the reported creation is rejected (this host cannot materialize its prefab) — the report's answer, so its pending re-report stops and its local copy dies

	// Guest partial-damage report recovery (sync-coverage audit W2 — the live
	// BlockDamaged report is a DELTA the receiver accumulates, and the host's
	// authoritative table is the GAME's own list, so a swallowed report left the
	// host permanently short by exactly that hit and its absolute snapshot then
	// omitted the cell forever). The guest keeps its OWN cumulative contribution
	// per cell and re-reports the outstanding set until the host answers; the host
	// resolves every reported row against its per-sender ledger and applies the
	// difference (protocol 32), then answers with this host's own value for every
	// reported cell — a zero means "no damage here", and that answer, unlike the
	// periodic snapshot, is what clears the reporter's outstanding entries. One
	// new operation (the guest's report, sharing the snapshot's payload shape);
	// its answer is the pre-existing snapshot message.
	BlockDamageReport = 136, // guest → host: this sender's cumulative partial-damage contribution for the cells the host has not accounted for

	// Target-body verdict for a medical operation start (the target's own client
	// decides what its own body allows: the host validates what it owns, parks
	// the request and asks; the target runs the target half of the preconditions
	// against its LIVE body and answers). The pending table's liveness bound only
	// abandons an unanswered request — no latency value is ever a judgment input.
	MedicalOperationTargetCheckRequest = 137, // host → target: does your body allow this medical operation start?
	MedicalOperationTargetCheckAnswer = 138, // target → host: the target's own verdict + its reason (+ its live shrapnel count)

	// Recipe-unlock backfill (sync-coverage audit I6 — the blueprint unlock is a
	// one-shot report + relay while the recipe table is a per-process static, so
	// a swallowed send or a late join left a peer's crafting list short with
	// nothing that re-derived it). The unlock SET is the fact, and it is the same
	// fact in both directions: the host sends its live table's unlocked indices
	// on world entry and on the 60 s repair, and a guest reports its own set on
	// the shared fallback cadence until this host's set carries it. The set is
	// MONOTONIC (INT = 0 is what a blueprint's use writes and nothing writes it
	// back) and applying it is idempotent, so a repeat never re-locks a recipe
	// and an index this side already holds costs one suppressed write.
	RecipeUnlockSnapshot = 139, // bidirectional: guest → host report of this guest's unlocked recipe-index set; host → guest this host's authoritative set (world entry / 60 s repair / the answer that ends the guest's re-report)

	// The run/layer clocks the kernel baseline does not hold (the run clock is
	// game state no CUO domain owns: SaveSystem.savedRunTime is a per-process
	// static, 0 on a guest's fresh launch, so a member joining a run in progress
	// read only the time since it joined while the host read the run's total —
	// the end screen's death-stats clock). The layer's radiation-timer
	// accounting rides the same instant for the same reason. Both values are
	// read off the host's live world AT EACH SEND (the run clock is the host's
	// total at the instant the member is sent it; the receiver maps it back onto
	// its own world's epoch) and stamped with the kernel run baseline's
	// generation, the same stamp every layer-relative world report carries, so a
	// value from a previous layer is refused instead of being written onto a
	// freshly generated one. Sent in the world-entry group and the 60 s repair
	// group, right after the checkpoint that establishes the stamp it is
	// compared against.
	RunFacts = 140, // host → guest: the host's run total at the send point and the layer's timer accounting, stamped with the run-baseline generation

	// World-item impact presentation (one-shot: the side that SIMULATES the
	// landing plays the native collision presentation — the "drop" clip, the
	// landing block's step sound and the dust, or a plush's own squeak — while a
	// guest's non-authoritative world-item copies have theirs suppressed). The
	// authority reports, every guest replays; there is no guest → host report,
	// because a guest never simulates an authoritative landing.
	ItemImpact = 141, // host → guest: the authority played a world-item impact presentation at this position

	// The end-of-layer choice (member intent, host authority: the native
	// end-of-layer panel appears on the client whose own body is at the layer's
	// bottom, and its "Continue" is that member's judgment that the layer is
	// finished — but the layer's generation baseline is captured on the HOST's
	// boundary, so a member's own regeneration has nothing to generate from and
	// the session never moves). The member reports the choice instead, stamped
	// with its kernel generation — the same identity every layer-relative world
	// report carries — and the host drives its own advance when the stamp IS the
	// generation it is in. A request for any other layer, or from a peer that is
	// not a handshaken member, changes nothing; two members choosing at the same
	// moment produce one advance.
	LayerAdvanceRequest = 142, // guest → host: a member reached the end of the layer and chose to continue

	// The outcome report of every cross-player use whose effect runs on the
	// AFFECTED side's own client — the solid-food eat and the limb-tool use — because
	// only that side can say what the used item became: the action wrote that side's
	// copy of another member's carried item. The host admitted the use first — that
	// admission is the grant this report is matched against, one report per admitted
	// use — then commits the reported state into the owner's authoritative record and
	// publishes the ordinary use result, so the item stays its owner's fact and
	// reaches its owner's own item.
	PlayerItemActionOutcome = 143, // affected side → host: the item's own action ran on this client; the used item's post-action state

	// Host rules reach the members (the census answers which: HostRuleCensus). The
	// rules were local host configuration by decision, and a member's process held its
	// OWN values under the same names — so its member list decided the take affordance
	// from its own config, its console applied a write the host then refused (the value
	// appeared and went back), and a carrier's own client read its own piggyback
	// multiplier. The announcement carries the whole set ABSOLUTE: sent to a member the
	// moment it is admitted, again in the world-entry group and the 60 s in-session
	// repair group (a swallowed send converges on the repair), and broadcast to every
	// member the instant the host flips a rule — the config file, the Admin page and the
	// console all write through the same monitor, so one change is one announcement.
	// It is an interception layer, never a verdict: a member refuses locally what the
	// announced rules forbid, and the host checks the same thing behind it, because a
	// member's belief can be stale, lost or forged.
	HostRules = 144, // host → member: the host's host-rule values, absolute (admission, world entry, repair cycle, and every change while the session runs)

	// An inventory intent the host refused, answered to the client that performed the gesture. Without it the actor's
	// experience of every refused gesture is identical to a gesture that silently did nothing: the item stays put and
	// no explanation appears anywhere that player can see. One refusal per gesture, and never for a refusal the
	// actor's own client already made.
	InventoryIntentRefused = 145, // host → requester: the intent sent was refused, and the host's own reason

	// The host's cheat-permission roster, announced ABSOLUTE: who may run the cheat verbs (/data's write verbs,
	// /give, /summon and the /heal reset). The host's own standing is the online switch and is never on
	// this list. It rides the same three edges as HostRules — admission, world entry, the in-session repair group —
	// plus every grant/revoke while the session runs, because the list is what a member's own console refuses a
	// cheat verb against: a closed permission costs no round trip, and the operator reads the reason where they
	// typed the verb. Like the rules it is an interception layer, never the verdict: the host judges every request
	// that does arrive against its own list, and answers a refusal.
	CheatPermissions = 146, // host → member: the cheat-permission list, absolute (admission, world entry, repair cycle, and every grant/revoke)

	// The console /give verb (a cheat verb). The member cannot create an item — the
	// definition table, the prefab capture and the instance-id space are the host's —
	// so the member's verb is a REQUEST and the host's creation its answer. The host
	// judges the request against its own cheat switch and roster before it creates
	// anything, and the answer carries the host's own sentence, so a refusal lands
	// where the operator typed the verb. The item itself does not travel on this
	// pair: once created it rides the ordinary inventory-transfer fact below.
	PlayerInventoryGiveRequest = 147, // guest → host: create one item for a player, with the properties the operator named
	PlayerInventoryGiveResult = 148, // host → requester: the created item's identity and slot, or the refusal that answered it

	// The console /summon verb (a cheat verb): create one world object — a world entity, an animal, or (with the
	// "item" type word) a loose item entity — at a position. The member cannot create it: a creation the host
	// cannot materialize is rejected and the creating side's copy dies, the answer could not say what landed, and
	// only the host's own record is the one the world-entry group carries to a later joiner. So the member
	// resolves the position on its own scene (its pointer, its body) and REQUESTS; the host judges its own cheat
	// switch and roster, enforces the type word against its own tables, creates through the game's own call, and
	// answers. The created object itself does not ride this pair: a world entity reports EntitySpawned, an animal
	// rides the enemy domain's runtime-spawn facts, and a loose item entity reports ItemSpawn — the same row the
	// give ladder's drop makes.
	WorldSummonRequest = 149, // guest → host: summon one prefab (or item entity) at this point, with the operator's item state
	WorldSummonResult = 150, // host → requester: the created object's identity (items) and the answer, or the refusal that answered it

	// The console /heal verb (a cheat verb): an immediate, item-free FULL reset of the selected
	// players' state. A reset is a mutation of the TARGET's body, so by the judgment-ownership rule
	// the target's own client applies it — the host judges its own cheat switch and roster, resets its
	// own body where it is named, and instructs each member target. The result travels as two reports:
	// the ordinary character snapshot for the peers and the reconnect, and `HealResetApplied` for the
	// host's authority copy, because the plain snapshot may not state the three continuous values the
	// host owns. The answer carries the host's own sentence, so a refusal lands where the operator
	// typed the verb.
	HealResetRequest = 151, // guest → host: reset these players' state now (the member's selector expansion)
	HealResetInstruction = 152, // host → a member target: apply the full reset to your own body now (carries the host's instruction id)
	HealResetResult = 153, // host → requester: the answer (what was reset/revived/restored, or the refusal)

	// The applied half of the heal reset: the target's own client reports the body state the reset
	// produced, and the host merges it through the character data's authority-merge seam
	// (ApplyHealResetEvent), not the plain report path, because the ordinary character report may not
	// state the three continuous values the host owns. Bound to the instruction the host issued to
	// that member (its id is echoed), to the transport sender's own body, and to the reset table's
	// healthy constants for those three values.
	HealResetApplied = 155, // guest → host: the reset the host instructed was applied; here is the resulting full health/limb state

	// Host-computed continuous vitals (host authority — the host owns the continuous bleeding
	// values for every player and pushes the row down; the affected client still judges the
	// events and applies the row to its own body, so this is not a body write by the host)
	PlayerVitals = 154, // host → client: the host-computed bleeding row (blood volume + total speed + per-limb bleed, 10 Hz, unreliable; also answers a refused report by field name)

	// The world-entry instruction is an EDGE: the host sends it once per entry (at its own
	// world-entry transition and at the click), and the member's state at that instant can make
	// it vanish — one that arrives while the member is still in its old world waits at the entry
	// gate and is discarded when that world unloads, and one the member never received has no
	// second chance. A layer change is the case that exposes it: the host regenerates (its world
	// leaves the scene, every member is pulled to the menu), the members follow the host's
	// re-entry invite, and a member whose invite was lost is still in the menu a minute later
	// with no body (batch `20261007-a`'s unprotected member).
	//
	// So the member REPORTS the one fact the host cannot see — it is out of the world and its
	// exit was the host's own transition (a member that chose the menu must stay there) — and
	// the host answers with the SAME instruction every other entry uses: the targeted
	// `WorldJoin` and the run-baseline checkpoint that follows it. The host's own facts bound it
	// (handshaken member, held OUT of the world, a live world, a published run baseline), and a
	// per-sender cooldown keeps a spammer from making the host ship a checkpoint set per frame.
	ReentryRequest = 156, // guest → host: this member was pulled out of the world by the host's own exit and asks to be taken back in

	// The host's own layer advance is a SHARED EDGE, not an exit: while the host regenerates, its world
	// stops being live for a few frames (the round trip through the menu is what every member used to
	// follow) and then comes back one layer deeper. A member that instead follows the edge generates its
	// OWN new layer from the host's captured baseline at the same time as the host, so the group loads in
	// parallel and enters together through the ordinary start gate. The message is sent at the START of
	// the host's regeneration, before the transition reaches any member as a scene report, so a member can
	// never mistake it for the host leaving. It carries the native two-layer flag (the drill pod steps two
	// layers) and the host's generation stamp: a descent for any generation but this member's own is
	// refused rather than guessed at. A lost edge is not fatal — the host's ordinary world join at its new
	// entry still invites the member, which is the pre-edge recovery path.
	LayerDescent = 157, // host → guest: this host is advancing a layer now — generate your own new layer from its baseline and wait for the gate

	// The give gesture's placement answer. The host asks the RECEIVING client where its own rules would put
	// the item BEFORE any custody moves, and the receiver answers with the destination it will use or the
	// reason it refuses. The user's 2026-10-22 ruling — the item stays with the giver when the destination
	// cannot take it — needs that answer before the ledger row is written: the host then records the
	// receiver's RESOLVED destination, never the giver's request, and a refusal writes nothing at all.
	PlayerPlacementQuery = 158, // host → receiver: resolve this give's destination with your own rules (no custody moves yet)
	PlayerPlacementAnswer = 159, // receiver → host: the destination this client will use, or the reason it refuses

}
