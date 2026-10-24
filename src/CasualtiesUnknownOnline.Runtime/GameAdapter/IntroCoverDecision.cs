using System;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one named decision about the game's intro cover (the content-warning screen), replacing the process-wide
/// "a join happened once" flag that used to keep the cover gone for the rest of the process.
///
/// <para>
/// Two facts decide it, and they answer different questions:
/// </para>
/// <list type="bullet">
/// <item>the player's own LOCAL preference (<c>Session/SkipIntroCover</c>, off by default): on = an ordinary
/// launch skips the cover, off = nothing skips it;</item>
/// <item>the launch path: a launch through Steam's "join a friend" action shows the cover even with the preference
/// on, because that launch is about getting into a session, not about skipping the game's own screen.</item>
/// </list>
///
/// <para>
/// Nothing here crosses the wire: it decides what THIS player sees on THIS machine. The preference is read live
/// (a <see cref="Func{TResult}"/> over the config entry), so flipping it takes effect on the next launch without a
/// second flag to keep in sync.
/// </para>
/// </summary>
public sealed class IntroCoverDecision
{
	private Func<bool>? _skipsTheCover;
	private bool _directJoinLaunch;

	/// <summary>True when the cover must not be shown on this launch.</summary>
	public bool SkipsTheCover => (_skipsTheCover?.Invoke() ?? false) && !_directJoinLaunch;

	/// <summary>True when this launch came from Steam's join-a-friend action.</summary>
	public bool IsDirectJoinLaunch => _directJoinLaunch;

	/// <summary>The player's own preference, read live; null until the shell wires it (then the cover shows).</summary>
	public void UsePreference(Func<bool> skipsTheCover) => _skipsTheCover = skipsTheCover ?? throw new ArgumentNullException(nameof(skipsTheCover));

	/// <summary>Records that this launch came from the join action. A join never skips the cover (the user's rule).</summary>
	public void NoteDirectJoinLaunch() => _directJoinLaunch = true;

	/// <summary>
	/// Why the cover is shown or skipped, as one line for the log and for a reader: every path answers, so a reading
	/// of this ticket's four launch cases can tell which rule fired instead of inferring it from a missing screen.
	/// </summary>
	public string Describe() => (_directJoinLaunch, _skipsTheCover?.Invoke() ?? false) switch
	{
		(true, _) => "this launch came from the join action, which never skips the cover",
		(false, true) => "the local preference is on and this is an ordinary launch",
		(false, false) => "the local preference (Session/SkipIntroCover) is off",
	};
}
