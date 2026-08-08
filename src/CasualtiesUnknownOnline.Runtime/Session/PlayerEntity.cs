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

	/// <summary>True when the player is sitting (idle &gt; 12s, Body.cs:3162).</summary>
	public bool Sitting { get; set; }

	/// <summary>True when the player is lying down (sleeping / nap, Body.cs:2514).</summary>
	public bool Sleeping { get; set; }

	/// <summary>True when the player is climbing (currentClimbable, Body.cs:470).</summary>
	public bool Climbing { get; set; }

	/// <summary>
	/// True while the player performs an attack swing (ExtendedFlags 0x01).
	/// Wire-reserved in the star-network refactor; consumed when attack-animation
	/// sync lands (the clone drives armsAnimator.Play("ArmsSwing")).
	/// </summary>
	public bool IsAttacking { get; set; }

	/// <summary>Host side: position the guest reported when entering the world — the clone's spawn anchor.</summary>
	public NetVector2 ReportedSpawnPos { get; set; }

	// ---- Render interpolation buffer (guest side only) ----
	/// <summary>Previous authoritative values, for lerping between snapshots.</summary>
	public NetVector2 PrevPosition { get; set; }

	public NetVector2 PrevLookPos { get; set; }

	public NetVector2 PrevVelocity { get; set; }

	/// <summary>
	/// Environment.TickCount when the current snapshot arrived. Stays MinValue
	/// until the first snapshot: while negative, SessionStatePump keeps the
	/// clone at its spawn anchor, and ReadEntityState applies the first
	/// snapshot directly (Prev = current) instead of interpolating from the
	/// buffer's (0,0) defaults.
	/// </summary>
	public int StateReceivedMs { get; set; } = int.MinValue;

	/// <summary>Local scene state, exchanged via SceneState messages.</summary>
	public bool InWorld { get; set; }
}
