using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks a Body as remote-managed: a frozen render proxy. All its physics and
/// game logic (FixedUpdate/Update) are skipped; only the session's reported
/// state is written onto it each frame.
/// </summary>
internal sealed class RemoteBodyDriver : MonoBehaviour
{
	/// <summary>Last applied sitting pose — sit clips play only on transitions.</summary>
	public bool PrevSitting;

	/// <summary>
	/// True while this remote clone is the rider of a carry relation whose
	/// carrier is the local player. Set by <see cref="RemotePlayerRenderer"/>
	/// before applying state each frame; used by SessionStatePump to suppress
	/// the native sit replay and by BodyUpdatePatch to force an already-playing
	/// sit clip back to the ride/standing presentation.
	/// </summary>
	public bool IsCarriedRider;

	/// <summary>
	/// True while this remote clone is itself the carrier half of a carry
	/// relation. Set by <see cref="RemotePlayerRenderer"/> before applying
	/// state each frame; used by SessionStatePump and BodyUpdatePatch so a
	/// carrier never displays an idle-sit pose on any peer's view.
	/// </summary>
	public bool IsCarrier;

	/// <summary>Last applied sleeping pose — lay-down clips play only on transitions.</summary>
	public bool PrevSleeping;

	/// <summary>Last applied lying pose (standing=false, not sleeping) — same transition rule.</summary>
	public bool PrevLying;

	/// <summary>A reliable ragdoll-collapse event is waiting for the state stream's standing=false confirmation.</summary>
	public bool RagdollCollapsePending;

	/// <summary>The state stream has confirmed standing=false after the ragdoll event — later standing=true is a real stand-up.</summary>
	public bool RagdollCollapseConfirmed;

	/// <summary>Environment.TickCount when the ragdoll-collapse event was applied (the suppression window start).</summary>
	public long RagdollCollapseMs;

	/// <summary>True when the stream has delivered exact owner limb-pose facts; BodyPatches must let those transforms win over the animator skeleton.</summary>
	public bool RagdollPoseActive;

	/// <summary>Last applied attack-swing flag — the ArmsSwing clip plays only on the flag's rising edge.</summary>
	public bool PrevAttacking;

	/// <summary>Last applied swing sequence — the ArmsSwing clip replays when the sequence CHANGES (rapid swings inside one held flag window), the flag edge being the old-sender fallback.</summary>
	public byte PrevSwingSeq;

	/// <summary>The first snapshot seeded PrevSwingSeq — before that a sequence edge is NOT a new swing (the sender may have swung long before this clone existed).</summary>
	public bool SwingStateSeeded;

	/// <summary>Current climbing state — HandleVisuals overwrites the animator flag every frame.</summary>
	public bool Climbing;

	/// <summary>Current wall-slide-left state — BodyPatches re-asserts it on the clone's private Body.slidingLeft before HandleVisuals.</summary>
	public bool SlidingLeft;

	/// <summary>Current wall-slide-right state — BodyPatches re-asserts it on the clone's private Body.slidingRight before HandleVisuals.</summary>
	public bool SlidingRight;


	/// <summary>Last applied workout type — the exercise clips replay only when the wire type changes.</summary>
	public byte PrevWorkoutType;

	/// <summary>Last applied nap variant — the lay-down clip pair replays when the variant changes.</summary>
	public byte PrevNapVariant;

	/// <summary>Last snapshot arrival (TickCount) — snapshot-change detection for the arrival-interval estimate.</summary>
	public long LastStateMs;

	/// <summary>Exponentially-averaged snapshot arrival interval (seconds) — the interpolation window. A raw per-snapshot interval jitters on an unreliable channel (pauses, then jumps); the average keeps the window stable so the proxy glides instead of stepping.</summary>
	public float AvgIntervalSec;
}
