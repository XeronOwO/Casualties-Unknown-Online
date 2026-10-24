namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// Who owns the keyboard right now, decided from the three facts the game publishes: its own console is open, a
/// control has focus, or that focus is CUO's own command box.
///
/// <para>
/// The user's 2026-10-24 rule (asked for while fixing the key-bind capture): a focused control that is NOT CUO's
/// command box means the game owns the keyboard, so the opener stands down and a key press cannot misfire; no focus
/// at all — the ordinary state while playing — lets the opener act; and a focused CUO command box is the console
/// itself, which handles the key as a command. The game's own console keeps its own arm: it executes Enter with no
/// focus test at all, so an open console owns the keyboard whatever is selected.
/// </para>
/// </summary>
public static class NativeKeyboardOwnership
{
	/// <summary>
	/// True when the game owns the keyboard — the opener must not claim a key. <paramref name="consoleOpen"/> is the
	/// game's own console being open, <paramref name="focusIsLiveControl"/> a focused control that is visible, and
	/// <paramref name="focusIsCuoCommandBox"/> that this focused control is CUO's own command box (which handles the
	/// key itself).
	/// </summary>
	public static bool GameOwnsTheKeyboard(bool consoleOpen, bool focusIsLiveControl, bool focusIsCuoCommandBox) =>
		consoleOpen || (focusIsLiveControl && !focusIsCuoCommandBox);
}
