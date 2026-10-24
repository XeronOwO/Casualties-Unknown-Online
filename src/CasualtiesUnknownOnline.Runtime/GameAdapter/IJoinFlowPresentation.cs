using System;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The join flow as the host shell may state it: a launch that already knows
/// which lobby it is joining must reach a usable menu at once instead of waiting
/// for a player to click through the intro. The shell declares the intent and the
/// adapter decides how the game's intro is presented — the shell never writes
/// game state itself.
/// </summary>
public interface IJoinFlowPresentation
{
	/// <summary>
	/// Declares that this launch is a direct join: Steam starts the game with the
	/// lobby id on the command line when a friend's "Join Game" is clicked. The cover is
	/// STILL shown on this path (the user's 2026-10-24 ruling on
	/// <c>launch-intro-cover-policy.md</c>): the launch path decides the cover, and the
	/// preference governs ordinary launches only.
	/// </summary>
	void PrepareForDirectJoin();

	/// <summary>
	/// Hands the adapter the player's own local preference for the cover, read live: true = an ordinary launch
	/// skips it. The shell owns the configuration entry; the adapter owns when the cover is shown.
	/// </summary>
	void ConfigureIntroCoverPreference(Func<bool> skipsTheCover);
}
