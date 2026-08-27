namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure gate for the character ragdoll one-shot versus the 20 Hz entity-state
/// stream. The reliable <c>CharacterRagdoll</c> event is allowed to win over a
/// stale/out-of-order <c>Standing=true</c> snapshot for a short window, but only
/// until a <c>Standing=false</c> snapshot confirms the collapse (or the window
/// expires). This prevents the common race where the reliable ragdoll event
/// arrives before the unicast/unreliable state stream has caught up, and an
/// older standing snapshot then makes the remote clone stand again.
/// </summary>
public static class RagdollPoseGate
{
	/// <summary>
	/// How long the dedicated ragdoll event may suppress a conflicting
	/// standing=true snapshot while waiting for the state stream's
	/// standing=false confirmation. 500 ms covers several 20 Hz ticks without
	/// making a genuinely-ended collapse look stuck.
	/// </summary>
	public const long SuppressWindowMs = 500;

	/// <summary>
	/// True when a received ragdoll collapse event should keep the render clone
	/// lying even though the current entity snapshot still says standing=true.
	/// The suppression ends when a standing=false snapshot has been observed
	/// (<paramref name="collapseConfirmed"/>), when the event is no longer
	/// pending, or when the suppression window expires.
	/// </summary>
	public static bool ShouldSuppressStanding(
		bool entityStanding,
		bool collapsePending,
		bool collapseConfirmed,
		long collapseMs,
		long nowMs)
	{
		if (!entityStanding || !collapsePending || collapseConfirmed)
		{
			return false;
		}

		return nowMs - collapseMs <= SuppressWindowMs;
	}
}
