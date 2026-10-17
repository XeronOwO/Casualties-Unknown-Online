using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Which wait this client is in, decided from <see cref="SessionWaitInput"/> alone.
/// Pure, so the two failure shapes a review found are cases rather than accidents:
/// a wait may only say what the client holding the facts can SEE, and the roster it
/// prints must be the set the gate is actually waiting for.
/// <para>
/// The direction comes first because it changes everything else. The host owns the gate
/// and its countdown. A member does not: it sees the host's world entry arrive (or not)
/// and it sees the other members' rows (or not), and those two facts are weaker than
/// they look — a member's row arrives already in-world, so a member waiting on a third
/// player usually has nothing to name, and the host's flag being false covers the menu,
/// loading and "no report yet" alike. The wording downstream is built to say no more
/// than these facts support.
/// </para>
/// </summary>
internal static class SessionWaitPolicy
{
	internal static SessionWaitState Decide(SessionWaitInput input)
	{
		if (input.IsHost)
		{
			// The gate arms on `Handshaken && !InWorld`, so this roster uses the SAME predicate:
			// naming a member the gate is not waiting for would put a name on screen next to a
			// promise that is not true for them.
			var waiting = Names(input, member => member.SteamId != input.LocalSteamId && member.Handshaken && !member.InWorld);
			return new SessionWaitState(SessionWaitKind.MembersLoading, waiting, Math.Max(0, input.GateRemainingSeconds));
		}

		if (!input.HostInWorld)
		{
			// Nothing here says the host is GENERATING: the flag this reads is false while it is in
			// the menu, while it is loading and while this client simply has not seen its report.
			return new SessionWaitState(SessionWaitKind.HostLoading, [], 0);
		}

		var others = Names(input, member =>
			member.SteamId != input.LocalSteamId && member.SteamId != input.HostSteamId && member.Handshaken && !member.InWorld);
		if (others.Count > 0)
		{
			return new SessionWaitState(SessionWaitKind.OthersLoading, others, Math.Max(0, 30 - (input.MsSinceHostInWorld / 1000)));
		}

		// An empty `others` is the ORDINARY case on a member — a third player's row arrives already
		// in-world — so this arm must not read as "everyone is here". It is the last stage of this
		// client's own wait, and it says exactly that.
		return new SessionWaitState(SessionWaitKind.Starting, [], 0);
	}

	private static List<string> Names(SessionWaitInput input, Func<SessionWaitMember, bool> predicate) =>
		[.. input.Members.Where(predicate).Select(member => member.DisplayName)];
}
