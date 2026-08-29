using CasualtiesUnknownOnline.Runtime.Protocol;

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

	/// <summary>
	/// The owner's LookTarget/CorpseScript override gaze target (null when no
	/// override is active). Drives the remote clone's head/eye direction so a
	/// peer visibly looks at the same world point the owner is looking at.
	/// </summary>
	public NetVector2? LookOverridePos { get; set; }

	/// <summary>The owner's remaining override-look time (Body.overrideLookTime).</summary>
	public float LookOverrideTime { get; set; }

	/// <summary>The owner's remaining scared-face time (Body.eyeScareTime).</summary>
	public float EyeScareTime { get; set; }

	/// <summary>The owner's remaining panic-face time (Body.eyePanicTime).</summary>
	public float EyePanicTime { get; set; }

	/// <summary>The owner's remaining eye-close time (Body.eyeCloseTime).</summary>
	public float EyeCloseTime { get; set; }

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

	/// <summary>
	/// Rolling per-swing sequence (0 = never swung): the render proxy replays
	/// the swing clip when this changes — every swing, even several inside one
	/// held IsAttacking window (rapid mining swings). An old-version sender
	/// never sets it; the receiver falls back to the flag's rising edge.
	/// </summary>
	public byte SwingSeq { get; set; }

	/// <summary>
	/// The owner's active workout/exercise type (0 = none, 1 = pushups, 2 =
	/// squats, 3 = plank). The render proxy replays the matching exercise
	/// animator clips when this changes; the 20 Hz stream refreshes the value
	/// while the workout runs and returns it to 0 when the workout ends.
	/// </summary>
	public byte WorkoutType { get; set; }

	/// <summary>The owner's nap variant (0 = standard lay-down, 1 = sick/alt lay-down).</summary>
	public byte NapVariant { get; set; }

	/// <summary>The owner's current dog-shake intensity (Body.dogShakeIntensity).</summary>
	public float DogShakeIntensity { get; set; }

	/// <summary>
	/// True while the owner is wall-sliding on the left wall (Body.slidingLeft,
	/// Body.cs:2601). Continuous presentation state: the render proxy replays
	/// the Wall clip and wall-side animator fields while the flag is set.
	/// </summary>
	public bool SlidingLeft { get; set; }

	/// <summary>True while the owner is wall-sliding on the right wall (Body.slidingRight, Body.cs:2600).</summary>
	public bool SlidingRight { get; set; }


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
}
