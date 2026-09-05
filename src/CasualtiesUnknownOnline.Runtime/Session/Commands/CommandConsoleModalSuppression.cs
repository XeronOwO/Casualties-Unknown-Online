namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Tracks the one-frame modal suppression needed after the standalone command
/// console closes. The console overlay closes on an ESC in IMGUI OnGUI, while
/// the game's native pause handling runs in Update; depending on Unity event
/// ordering, <c>Plugin.Update</c> may observe the console as already closed and
/// clear the modal guard before <c>PlayerCamera.HandleInput</c> runs in the same
/// frame. Keeping the modal guard active for the first frame after a console
/// close swallows that closing ESC so the game's pause menu cannot also open.
/// </summary>
public sealed class CommandConsoleModalSuppression
{
	private bool _consoleWasOpen;

	/// <summary>
	/// Call once per frame with the current console-open state. Returns true
	/// when the native modal guard must remain active for this frame.
	/// </summary>
	public bool Update(bool consoleOpen)
	{
		var suppress = _consoleWasOpen && !consoleOpen;
		_consoleWasOpen = consoleOpen;
		return suppress;
	}
}
