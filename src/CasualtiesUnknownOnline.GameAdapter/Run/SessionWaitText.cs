using System;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The ONE place a <see cref="SessionWaitState"/> becomes the line a player reads on
/// the loading screen. Every named kind of wait is worded here, and the last arm
/// THROWS rather than defaulting: C# requires an enum switch to cover values that are
/// not named members, so a plain fall-through would word a brand-new wait as if it
/// were the last stage — a silent lie on the screen. Throwing means a
/// <see cref="SessionWaitKind"/> added without a line compiles and then fails the first
/// time it is reached, naming itself, which is the closest a switch expression gets to
/// the ticket's "a new waiting state cannot be added without a line".
/// <para>
/// The wording follows what the player can act on rather than what the code knows: the
/// DIRECTION comes first (waiting for the host, or for named members), then the
/// countdown, because a reader stuck on a black window is asking "who is holding this
/// up" before "how long". The countdown is the gate's own remaining time on the host
/// and an approximation on a member, which is why the member's line says who rather
/// than claiming a deadline it does not own.
/// </para>
/// </summary>
internal static class SessionWaitText
{
	internal static string Line(SessionWaitState state) => state.Kind switch
	{
		SessionWaitKind.HostLoading => "Waiting for the host to load… The host is not in the world yet.",
		SessionWaitKind.MembersLoading => $"Waiting for {Names(state)} to load… ({Seconds(state)}s). The group starts together.",
		SessionWaitKind.OthersLoading => $"Waiting for {Names(state)}… ({Seconds(state)}s). The host is already in the world.",
		SessionWaitKind.Starting => "Waiting for the host to start the run…",
		_ => throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "this wait kind has no line yet"),
	};

	/// <summary>
	/// The named members, or a word for "nobody to name". A wait whose list is empty is
	/// answered honestly rather than with an empty gap: on the host's own screen the
	/// missing member is always named, so an empty list means the wait is about nobody
	/// in particular.
	/// </summary>
	private static string Names(SessionWaitState state)
	{
		// A blank display name is the one thing the join list cannot render: `string.Join` would leave a
		// doubled comma or a gap in a sentence whose whole purpose is legibility. This projection is the
		// only reader of that field which does not fall back to a persona name, so it says something
		// generic rather than nothing.
		var named = state.WaitingNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
		return named.Count == 0 ? "the remaining players" : string.Join(", ", named);
	}

	private static int Seconds(SessionWaitState state) => state.RemainingSeconds < 0 ? 0 : state.RemainingSeconds;
}
