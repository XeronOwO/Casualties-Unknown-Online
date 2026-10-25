using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The ONE place a <see cref="SessionWaitState"/> becomes the line a player reads on the loading screen. Every named kind of wait is
/// worded here, as a CATALOGUE KEY plus the values its sentence places — the line is shown on the LOCAL player's screen, so it is
/// rendered in the local player's language by the client that draws it (`wait.*` in both tables). A host running the Chinese UI read
/// an English "Waiting for Kaguya to load… The group starts together." before this, which is the user's 2026-10-25 report.
/// <para>
/// The last arm THROWS rather than defaulting: C# requires an enum switch to cover values that are not named members, so a plain
/// fall-through would word a brand-new wait as if it were the last stage — a silent lie on the screen. Throwing means a
/// <see cref="SessionWaitKind"/> added without a line compiles and then fails the first time it is reached, naming itself.
/// </para>
/// <para>
/// The wording follows what the player can act on rather than what the code knows: the DIRECTION comes first (waiting for the host, or
/// for named members), then the countdown, because a reader stuck on a black window is asking "who is holding this up" before "how
/// long".
/// </para>
/// </summary>
internal static class SessionWaitText
{
	/// <summary>The key of the line for this wait, or a throw when the kind has no line yet.</summary>
	internal static string WaitLineKey(SessionWaitState state) => state.Kind switch
	{
		SessionWaitKind.HostLoading => "wait.host_loading",
		SessionWaitKind.MembersLoading => "wait.members_loading",
		SessionWaitKind.OthersLoading => "wait.others_loading",
		SessionWaitKind.Starting => "wait.starting",
		_ => throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "this wait kind has no line yet"),
	};

	/// <summary>The values the key's sentence places, in catalogue order.</summary>
	internal static IReadOnlyList<string> Args(SessionWaitState state) => state.Kind switch
	{
		SessionWaitKind.HostLoading => [],
		SessionWaitKind.MembersLoading or SessionWaitKind.OthersLoading =>
			[Named(state), Seconds(state).ToString(CultureInfo.InvariantCulture)],
		SessionWaitKind.Starting => [],
		_ => throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "this wait kind has no line yet"),
	};

	/// <summary>
	/// The line, rendered through the caller's catalogue: the loading screen is this client's own, so its language decides. The
	/// "nobody to name" word is rendered the same way, so a Chinese sentence never carries an English clause.
	/// </summary>
	internal static string Line(SessionWaitState state, Func<string, IReadOnlyList<string>, string> render)
	{
		var args = Args(state);
		if (state.Kind is SessionWaitKind.MembersLoading or SessionWaitKind.OthersLoading)
		{
			var names = state.WaitingNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
			args = [names.Count == 0 ? render(WaitNobodyKey(), []) : string.Join(", ", names), args[1]];
		}

		return render(WaitLineKey(state), args);
	}

	/// <summary>
	/// The named members, or the catalogue's word for "nobody to name" — the key is resolved by <see cref="Line"/> with the same
	/// renderer, so both languages own it.
	/// </summary>
	private static string Named(SessionWaitState state) =>
		string.Join(", ", state.WaitingNames.Where(name => !string.IsNullOrWhiteSpace(name)));

	/// <summary>The catalogue key the renderer substitutes for an empty name list — its own helper so the localization census can see the
	/// literal as a READ, which is what keeps a Chinese sentence from carrying an English clause.</summary>
	internal static string WaitNobodyKey() => "wait.names_nobody";

	private static int Seconds(SessionWaitState state) => state.RemainingSeconds < 0 ? 0 : state.RemainingSeconds;
}
