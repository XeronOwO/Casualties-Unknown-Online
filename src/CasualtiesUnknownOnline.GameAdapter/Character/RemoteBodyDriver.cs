using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
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

	/// <summary>
	/// The shape of the exact limb poses currently rendered on this clone,
	/// captured relative to the body root when they were applied. It is a
	/// REFERENCE for the read-only check <see cref="CarriedLimbAnchor"/> documents
	/// — whether a carried rider clone's limbs actually travelled with the root
	/// the ride pose pinned — and never a placement source itself.
	/// </summary>
	public readonly CarriedLimbAnchor LimbAnchor = new();

	/// <summary>
	/// Largest limb-to-pinned-root separation read inside the current 1 Hz
	/// clone-diagnostic window. Zero is the expected reading (the transform
	/// hierarchy carries a clone's limbs with its root); a non-zero value is the
	/// runtime evidence that the carried clone's limbs were left behind.
	/// Read and reset by <see cref="RemotePlayerRenderer"/>'s diagnostics.
	/// </summary>
	public float LimbSeparationWindowMax;

	/// <summary>
	/// The SteamId of the carrier the stored reference below was written against,
	/// or 0 when no carry pin has been stored yet. It is a reference, not a live
	/// state: a relation that ends or a carrier that changes drops it
	/// (<see cref="CarryPresentationProbe.Clear"/>), and the drift reading is only
	/// taken while the live relation still names this same carrier AND the anchor
	/// this pin used still exists.
	/// </summary>
	public ulong PinnedCarrierSteamId;

	/// <summary>
	/// Whether the stored reference anchored on the LOCAL carrier body — the
	/// carrier's own view — rather than on that carrier's render clone (a
	/// third-party view). The drift reading must resolve the anchor the same way
	/// the pin did.
	/// </summary>
	public bool PinnedToLocalCarrier;

	/// <summary>World X of the rider-minus-anchor offset the last carry pin wrote.</summary>
	public float PinnedOffsetX;

	/// <summary>World Y of the rider-minus-anchor offset the last carry pin wrote.</summary>
	public float PinnedOffsetY;

	/// <summary>
	/// How many carry pins were written during the current 1 Hz clone-diagnostic
	/// window. It is the window's answer to "was this clone pinned at all": a
	/// drift reading is only meaningful in a window that has one, and a live carry
	/// relation with none is the anomaly, reported instead of a drift. Read and
	/// reset by <see cref="RemotePlayerRenderer"/>'s diagnostics.
	/// </summary>
	public int PinCountInWindow;

	/// <summary>
	/// Largest distance this clone was RENDERED away from the position its carry
	/// pin wrote for it (relative to its carrier) inside the current 1 Hz
	/// clone-diagnostic window. Zero is the expected reading — the frames that
	/// rendered showed the pair where the pin put them; a non-zero value is the
	/// rider having been moved after the pin, in world units. It survives the
	/// relation ending: the reading was taken while the pin was in force, and the
	/// window the diagnostic reports is the window it happened in. Read and reset
	/// by <see cref="RemotePlayerRenderer"/>'s diagnostics.
	/// </summary>
	public float PinDriftWindowMax;

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

	/// <summary>
	/// The owner's actual head/mouth sprite state from the 1 Hz character
	/// snapshot. <c>FacialExpressionHeadPatch</c> restores this on the clone
	/// after the game's own <c>FacialExpression.Update</c>, so clone-local
	/// mouth triggers (slot contents, head-limb dislocated, inherited eat-time)
	/// can never override the owner's visual truth.
	/// </summary>
	public HeadMouthState HeadMouth;

	/// <summary>
	/// The owner's computed leg-speed multiplier (0-1) from the 1 Hz character
	/// snapshot. It drives the weakness/slouch CrouchAmount input on the proxy
	/// (<see cref="BodyPosePresentation.ProxyCrouchInput"/>); default 1 means
	/// full strength/standing before the first snapshot arrives.
	/// </summary>
	public float LegSpeedMult = 1f;

	/// <summary>Last snapshot arrival (TickCount) — snapshot-change detection for the arrival-interval estimate.</summary>
	public long LastStateMs;

	/// <summary>Exponentially-averaged snapshot arrival interval (seconds) — the interpolation window. A raw per-snapshot interval jitters on an unreliable channel (pauses, then jumps); the average keeps the window stable so the proxy glides instead of stepping.</summary>
	public float AvgIntervalSec;
}
