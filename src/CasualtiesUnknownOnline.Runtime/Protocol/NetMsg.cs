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
	WorldStartParams = 19,
	WorldJoin = 20, // host → guest: start loading the world (sent at generation start, after the params — the host owns the timing)
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

	// World items (runtime-generated item entities, local compute → host register → relay)
	ItemSpawn = 45, // guest → host: report; host → guest: broadcast relay (source excluded)
	ItemPickup = 46, // guest → host: report (host arbitrates — first-writer-wins); host → guest: broadcast of the winner
	ItemDrop = 47, // guest → host: report; host → guest: broadcast relay (source excluded)
	ItemDestroy = 48, // guest → host: report; host → guest: broadcast relay (source excluded)
	ItemReject = 49, // host → guest: arbitration refusal (e.g. pickup of an unknown item) — the guest rolls back
	ItemSnapshot = 50, // host → guest: full world-item snapshot on world entry (late joiner / reconnect)

	// World entities (player-attacked building entities — plants, crates, creatures)
	BuildingEntityDamaged = 51, // guest → host: report (host applies + relays); host → guest: broadcast relay (source excluded)
	BuildingEntityOpened = 52, // guest → host: a crate/lock was opened (health = 0 write path); host → guest: broadcast relay (source excluded)

	// World events (host authority)
	EarthquakeStart = 55, // host → guest: an earthquake began (Duration seconds) — guests show the effect and suppress their own independent quake (four independent quakes would strip the terrain)
	ItemMove = 56, // host → guest (unreliable): moving world-item positions — the host's physics is the position authority, guests follow (drops bounce to different spots otherwise)
	KeypadCode = 57, // host → guest: the keypad codes (airdrop crates etc.) — generated host-side at world entry (the game lazy-generates them per side on first use, Openable.cs:19, which would give every side its own code)

	// Item arbitration (accept-with-correction: the action always passes, the evidence decides whether a correction follows)
	ItemCorrection = 59, // host → guest: authoritative item state (the guest's last action-report evidence diverged) — applied via the restore machinery, no location fields
	ItemUse = 60, // guest → host: an item was used (digest evidence) — the host validates state and corrects
	ItemSlot = 61, // guest → host: an item moved slots (id + new slot) — the host updates its record and corrects

	// World items (generation-time — host authority: the host assigns the ids and distributes the full set)
	WorldItemsSnapshot = 62, // host → guest: the generation-time world items (ground + starting supplies) with host-assigned ids — the guests bind their local copies to the host's ids or materialize the host's version

	// Carried-item facts (host → guest events: use flipped state, slot move, pickup — the receiver updates the per-player fact table and re-renders the clone immediately; the 1 Hz character snapshot stays as the fallback)
	ItemCarriedSync = 63, // host → guest: one carried item's authoritative state (OwnerSteamId + full fact + SlotKnown) — a use/slot move/pickup broadcast; leaving an inventory travels ItemDrop

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
}
