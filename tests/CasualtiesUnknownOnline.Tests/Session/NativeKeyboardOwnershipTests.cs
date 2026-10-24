using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Who owns the keyboard (the user's 2026-10-24 rule, asked for while fixing the key-bind capture): a focused control
/// that is not CUO's command box owns the keyboard, no focus lets the opener act, and the game's own console owns it
/// whenever it is open. The rule is pure so the three states are cases instead of a chain of guesses in the guard.
/// </summary>
[Trait("Category", "Unit")]
public class NativeKeyboardOwnershipTests
{
	[Fact]
	public void NothingFocused_TheOpenerMayAct() =>
		Assert.False(NativeKeyboardOwnership.GameOwnsTheKeyboard(consoleOpen: false, focusIsLiveControl: false, focusIsCuoCommandBox: false));

	[Fact]
	public void AFocusedControlOwnsTheKeyboard() =>
		Assert.True(NativeKeyboardOwnership.GameOwnsTheKeyboard(consoleOpen: false, focusIsLiveControl: true, focusIsCuoCommandBox: false));

	[Fact]
	public void TheGamesOwnConsoleOwnsItWhateverIsFocused()
	{
		Assert.True(NativeKeyboardOwnership.GameOwnsTheKeyboard(consoleOpen: true, focusIsLiveControl: false, focusIsCuoCommandBox: false));
		Assert.True(NativeKeyboardOwnership.GameOwnsTheKeyboard(consoleOpen: true, focusIsLiveControl: true, focusIsCuoCommandBox: false));
	}

	[Fact]
	public void AFocusedCuoCommandBoxIsTheConsoleItself() =>
		Assert.False(NativeKeyboardOwnership.GameOwnsTheKeyboard(consoleOpen: false, focusIsLiveControl: true, focusIsCuoCommandBox: true));
}
