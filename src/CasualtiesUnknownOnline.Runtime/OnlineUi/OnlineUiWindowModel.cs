using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The Online UI window for one frame (ticket online-ui-art-and-controls-overhaul, S2b): the shell's own
/// facts (its title and its tab row) plus the current page as a list of rows. Null in
/// <see cref="OnlineUiFrame.Window"/> means the window is not shown, which is the state the launcher's
/// click toggles.
///
/// <para>
/// The tabs are ordinary <see cref="OnlineUiElementModel.Button"/> elements carrying
/// <see cref="OnlineUiElementModel.Selected"/>, so a tab click comes back as the same
/// <see cref="OnlineUiIntentKind.ControlInvoked"/> a page button produces and the plugin decides what it
/// means (the page switch stays where it is, next to the page enum and the drawer dispatch).
/// </para>
/// </summary>
public sealed record OnlineUiWindowModel(
	string Title,
	IReadOnlyList<OnlineUiElementModel> Tabs,
	IReadOnlyList<OnlineUiRowModel> Rows);
