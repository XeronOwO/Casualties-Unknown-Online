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
	HandshakeAck = 17,
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
}
