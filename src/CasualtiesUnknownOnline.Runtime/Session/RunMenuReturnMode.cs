namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Decides how a player should leave the world after a session teardown.
/// The host is the save authority, so a host leaving a live run must persist
/// the native run save before returning to the menu; a guest returns without
/// writing its own save (guest state is only mirrored through the host).
/// </summary>
public enum RunMenuReturnMode
{
	/// <summary>No menu return is needed (the player is already in the menu).</summary>
	None = 0,

	/// <summary>Return to the main menu without saving locally.</summary>
	MenuOnly = 1,

	/// <summary>Persist the native run save, then return to the main menu.</summary>
	SaveAndMenu = 2,
}
