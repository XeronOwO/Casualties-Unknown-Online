namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The network readout for one frame (ticket online-ui-art-and-controls-overhaul, S6): the live round-trip
/// time and, when there is one to show, the latest session event. It is the one Online UI surface that is
/// not attached to anything in the world — a readout in the canvas's top-left corner, where the game's own
/// hand-held item is not.
///
/// <para>
/// Both lines travel composed and with their colour, because both are the plugin's: the labels are
/// translated, the RTT is formatted there, and the status line only exists while the delayed-notification
/// window says so. "No status line" is a null OR an empty <see cref="StatusText"/> — the surface draws
/// nothing for either — because a translated line can legitimately come back empty, and an empty label
/// that still takes a slot in the readout would be a blank row. The surface places the two lines and
/// chooses nothing else.
/// </para>
/// </summary>
public readonly record struct OnlineUiNetworkHud(
	string RttText,
	OnlineUiNativeRgba RttColor,
	string? StatusText,
	OnlineUiNativeRgba StatusColor);
