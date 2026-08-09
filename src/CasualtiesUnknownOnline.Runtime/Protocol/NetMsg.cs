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
}
