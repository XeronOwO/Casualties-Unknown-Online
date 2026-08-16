namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure lying-pose decision for the render proxy (no Unity): the proxy lies
/// (ragdoll/dead/unconscious — the LayDown clips approximating the real
/// physics ragdoll, Body.cs:1713-1730, which a frozen proxy cannot replicate)
/// when it is not standing or not alive — unless it is sleeping, whose nap
/// clips (ExperimentLayDown/ArmsLayDown) take over. The SessionStatePump's
/// rule, extracted so the death/unconscious presentation is L0-locked.
/// </summary>
public static class LyingPose
{
	public static bool IsLying(bool standing, bool alive, bool sleeping)
		=> (!standing || !alive) && !sleeping;
}
