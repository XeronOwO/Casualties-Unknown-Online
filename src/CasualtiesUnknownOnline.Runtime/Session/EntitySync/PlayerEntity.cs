using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

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

	/// <summary>Time of the PREVIOUS snapshot (ms) — the render interpolation window adapts to the ACTUAL snapshot cadence (a low-frame-rate sender degrades the 20 Hz throttle to uneven intervals).</summary>
	public int PrevStateMs { get; set; } = int.MinValue;

	/// <summary>Domain → wire: the entity state snapshot; the reverse applies via
	/// <see cref="EntityStateMsg.ApplyTo"/>.</summary>
	public EntityStateMsg ToEntityStateMsg() => new()
	{
		Id = EntityId.ToNetworkEntityIdMsg(),
		Position = Position.ToNetVector2Msg(),
		LookPos = LookPos.ToNetVector2Msg(),
		Velocity = Velocity.ToNetVector2Msg(),
		Flags = (byte)(
			(IsRight ? 0x01 : 0) | (Standing ? 0x02 : 0) |
			(Alive ? 0x04 : 0) | (Conscious ? 0x08 : 0) | (Crouching ? 0x10 : 0) |
			(Sitting ? 0x20 : 0) | (Sleeping ? 0x40 : 0) | (Climbing ? 0x80 : 0)),
		ExtendedFlags = IsAttacking ? 0x01u : 0u,
	};
}
