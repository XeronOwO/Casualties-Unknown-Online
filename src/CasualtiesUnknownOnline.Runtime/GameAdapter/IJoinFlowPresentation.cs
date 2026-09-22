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
	/// lobby id on the command line when a friend's "Join Game" is clicked, so the
	/// content-warning/intro screen is skipped and the follow-host pump can start
	/// the run as soon as the world exists.
	/// </summary>
	void PrepareForDirectJoin();
}
