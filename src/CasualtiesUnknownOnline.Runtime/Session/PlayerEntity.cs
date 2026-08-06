using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session-side abstraction of one player entity. Buffers hold the latest
/// received/simulated values; the Game Adapter reads them to drive game objects
/// and writes them from the authoritative simulation.
/// </summary>
public sealed class PlayerEntity(ulong steamId, NetworkEntityId entityId, bool isLocal)
{
	/// <summary>SteamID64 of the owning player.</summary>
	public ulong SteamId { get; set; } = steamId;

	/// <summary>Assigned by the host during handshake / join.</summary>
	public NetworkEntityId EntityId { get; set; } = entityId;

	/// <summary>True when this entity is the local player's own body.</summary>
	public bool IsLocal { get; } = isLocal;

	// ---- Latest authoritative state (host → all) ----
	public NetVector2 Position { get; set; }

	public NetVector2 LookPos { get; set; }

	public NetVector2 Velocity { get; set; }

	public bool IsRight { get; set; }

	public bool Standing { get; set; }

	public bool Alive { get; set; } = true;

	public bool Conscious { get; set; } = true;

	public bool Crouching { get; set; }

	// ---- Guest input (guest → host, consumed on the host's clone) ----
	public NetVector2 MoveDir { get; set; }

	public bool JumpQueued { get; set; }

	/// <summary>Local scene state, exchanged via SceneState messages.</summary>
	public bool InWorld { get; set; }
}
