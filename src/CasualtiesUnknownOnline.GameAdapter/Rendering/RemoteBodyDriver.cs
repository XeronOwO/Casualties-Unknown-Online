using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Rendering;

/// <summary>
/// Marks a Body as remote-managed: a frozen render proxy. All its physics and
/// game logic (FixedUpdate/Update) are skipped; only the session's reported
/// state is written onto it each frame.
/// </summary>
internal sealed class RemoteBodyDriver : MonoBehaviour
{
	/// <summary>Last applied sitting pose — sit clips play only on transitions.</summary>
	public bool PrevSitting;

	/// <summary>Last applied sleeping pose — lay-down clips play only on transitions.</summary>
	public bool PrevSleeping;

	/// <summary>Last applied lying pose (standing=false, not sleeping) — same transition rule.</summary>
	public bool PrevLying;

	/// <summary>Current climbing state — HandleVisuals overwrites the animator flag every frame.</summary>
	public bool Climbing;

	/// <summary>Last snapshot arrival (TickCount) — snapshot-change detection for the arrival-interval estimate.</summary>
	public long LastStateMs;

	/// <summary>Exponentially-averaged snapshot arrival interval (seconds) — the interpolation window. A raw per-snapshot interval jitters on an unreliable channel (pauses, then jumps); the average keeps the window stable so the proxy glides instead of stepping.</summary>
	public float AvgIntervalSec;
}
