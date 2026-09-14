namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Decides how a player should leave a live world — after a session teardown or
/// on the deliberate leave itself. The host is the save authority, so a host
/// leaving a live run must take the world-archive cut before returning to the
/// menu; solo play has no session role but is its own save authority and does the
/// same; a guest returns without writing anything (its state is only mirrored
/// through the host).
/// </summary>
public enum RunMenuReturnMode
{
	/// <summary>No menu return is needed (the player is already in the menu).</summary>
	None = 0,

	/// <summary>Return to the main menu without saving locally.</summary>
	MenuOnly = 1,

	/// <summary>Take the CUO world-archive cut, then return to the main menu.</summary>
	SaveAndMenu = 2,
}
