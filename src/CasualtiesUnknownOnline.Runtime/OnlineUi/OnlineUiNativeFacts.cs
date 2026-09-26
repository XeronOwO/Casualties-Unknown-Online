using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What one read-only probe of the game's own UI produced (the Online UI art/controls overhaul, S1):
/// the game's UI font, the styles of the game's settings rows and of its chrome, and the UI scale the
/// game's canvas runs at. Every part is a plain value the Runtime can hold and test; a part the probe
/// could not read stays missing (null/empty) instead of being guessed, and <see cref="IsComplete"/>
/// is the fact that tells the capture policy to stop retrying.
/// </summary>
public sealed record OnlineUiNativeFacts(
	string CanvasPath,
	IReadOnlyList<OnlineUiNativeImageStyle> RowStyles,
	IReadOnlyList<OnlineUiNativeImageStyle> ChromeStyles,
	OnlineUiNativeTextStyle? Text,
	float? UiScale,
	string? Note)
{
	/// <summary>The reading of an attempt that found no usable game canvas, or that hit a problem: what is
	/// missing is the surface or a fact, and the retry policy decides whether that is fatal.</summary>
	public static OnlineUiNativeFacts Unavailable(string note) => new(string.Empty, [], [], null, null, note);

	/// <summary>True when the probe found the game's canvas and built its host under it.</summary>
	public bool CanvasAttached => !string.IsNullOrEmpty(CanvasPath);

	/// <summary>
	/// The four unknowns of this stage, all read in one attempt — the font, a settings row's image style,
	/// the chrome census and the canvas scale. An empty chrome census counts as missing, not as "the game
	/// has no chrome": a sweep that returned nothing has not answered the question, and a reading that
	/// stopped there would let S2 treat an unread fact as a fact.
	/// </summary>
	public bool IsComplete =>
		CanvasAttached
		&& Text is not null
		&& RowStyles.Count > 0
		&& ChromeStyles.Count > 0
		&& UiScale is not null;
}
