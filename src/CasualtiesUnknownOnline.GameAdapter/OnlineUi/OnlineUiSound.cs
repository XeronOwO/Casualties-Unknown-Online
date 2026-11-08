using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Which of the game's own UI sounds a control's report plays. The click-shaped intents are the game's
/// "click" (a button) and "miniClick" (a toggle or a dropdown pick, the pair `SettingsMenu` uses for its
/// small controls); typing, dragging and a committed number are not clicks and stay silent. Pure, so the
/// mapping is read by a test rather than by the ear.
/// </summary>
internal static class OnlineUiSound
{
	internal const string Click = "click";

	internal const string SmallClick = "miniClick";

	/// <summary>The sound the game plays for this report, or null when the report is not a click.</summary>
	internal static string? For(OnlineUiIntentKind kind) => kind switch
	{
		OnlineUiIntentKind.ControlInvoked => Click,
		OnlineUiIntentKind.ControlToggled => SmallClick,
		OnlineUiIntentKind.ControlSelected => SmallClick,
		_ => null,
	};
}
