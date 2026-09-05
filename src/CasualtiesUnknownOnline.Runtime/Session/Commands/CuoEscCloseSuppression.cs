namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Tracks the one-frame modal suppression needed after any CUO surface that
/// closes on ESC. The surfaces close in IMGUI OnGUI, while the game's native
/// pause handling runs in Update; depending on Unity event ordering,
/// <c>Plugin.Update</c> may observe a surface as already closed and clear the
/// modal guard before <c>PlayerCamera.HandleInput</c> runs in the same frame.
/// Keeping the modal guard active for the first frame after an ESC-closing
/// surface closes swallows that closing ESC so the game's pause menu cannot
/// also open.
/// </summary>
public sealed class CuoEscCloseSuppression
{
	private bool _commandConsoleWasOpen;
	private bool _onlineWindowWasOpen;
	private bool _quickPanelWasOpen;

	/// <summary>
	/// Call once per frame with the current visibility states of the ESC-closing
	/// CUO surfaces. Returns true when the native modal guard must remain active
	/// for this frame because at least one such surface just closed.
	/// </summary>
	public bool Update(
		bool commandConsoleOpen,
		bool onlineWindowVisible,
		bool quickPanelVisible)
	{
		var suppress = (_commandConsoleWasOpen && !commandConsoleOpen)
			|| (_onlineWindowWasOpen && !onlineWindowVisible)
			|| (_quickPanelWasOpen && !quickPanelVisible);

		_commandConsoleWasOpen = commandConsoleOpen;
		_onlineWindowWasOpen = onlineWindowVisible;
		_quickPanelWasOpen = quickPanelVisible;

		return suppress;
	}
}
