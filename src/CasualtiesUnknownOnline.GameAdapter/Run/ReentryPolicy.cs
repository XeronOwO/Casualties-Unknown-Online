using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The world half of the re-entry ask, kept pure so every world-state combination has a case
/// (<c>ReentryPolicyTests</c>): whether THIS client may answer a member's ask with an entry
/// instruction. The member-side facts (handshaken, held out of the world, the per-sender
/// cooldown) are the Runtime channel's admission and are not repeated here; this rule reads the
/// adapter's own facts — a live world and a committed run baseline — because only the adapter can
/// establish them.
/// <para>
/// A member's ask is answered with the SAME instruction every other entry uses
/// (<c>IWorldControl.SendWorldJoinTo</c>: the targeted <c>WorldJoin</c> and, when this host holds
/// a run, the run-baseline checkpoint that follows it). Sending it with no live world would enter
/// the member into a layer this host has not generated, and sending it with no run baseline would
/// hold the member's generation forever on params that never arrive — which is why both refusals
/// are conditions of the invite rather than a documented limit.
/// </para>
/// </summary>
internal static class ReentryPolicy
{
	internal static ReentryDecision Decide(SessionRole role, bool sessionActive, bool hostHasLiveWorld, bool runBaselinePublished)
	{
		if (role != SessionRole.Host || !sessionActive)
		{
			return ReentryDecision.NotThisSide;
		}

		if (!hostHasLiveWorld)
		{
			return ReentryDecision.NoLiveWorld;
		}

		return runBaselinePublished ? ReentryDecision.Invite : ReentryDecision.NoRunBaseline;
	}
}
