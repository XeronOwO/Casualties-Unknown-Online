namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where an open dropdown's option list is placed (ticket online-ui-layout-and-input-detail-pass, row 4; the
/// review of the sixth pass found the layer had cancelled the game's own flip). The list is an overlay whose
/// top edge — or bottom edge, when it opens above its field — sits on the field it belongs to, at the field's
/// width; the one decision left is which side of the field it opens on, and the room the canvas has decides
/// that. The game's own <c>TMP_Dropdown</c> answers the same question from the list's world corners against
/// the root canvas; this rule is that answer, kept pure so it can be tested without a Unity runtime and so a
/// change to it cannot hide among the adapter's anchor writes.
/// </summary>
public static class OnlineUiDropdownPlacement
{
	/// <summary>The side of its field a list opens on.</summary>
	public enum OpenSide
	{
		Below,
		Above,
	}

	/// <summary>
	/// The side a list opens on. Below is the normal side; a list that does not fit between its field and the
	/// canvas's bottom edge flips above when it does fit between the field and the top, which is the one case a
	/// player notices. A list that fits on neither side — the field is boxed in — stays below: its first option
	/// is then still at the field, and a half-covered list is the lesser evil than one whose beginning is off
	/// the screen. The canvas's edges are the room's own bounds, never a guess at where the player is looking.
	/// </summary>
	public static OpenSide SideFor(float listHeight, float roomBelow, float roomAbove) =>
		roomBelow < listHeight && roomAbove >= listHeight ? OpenSide.Above : OpenSide.Below;

	/// <summary>The anchor x a list's rect is written to: the field's centre in the layer's own normalized
	/// coordinates. A window dragged partly off the canvas puts the anchor outside <c>0..1</c>, which the
	/// engine handles exactly as it handles any anchor.</summary>
	public static float AnchorX(float fieldCenter, float layerLeft, float layerWidth) =>
		layerWidth > 0f ? (fieldCenter - layerLeft) / layerWidth : 0.5f;

	/// <summary>The anchor y a list's rect is written to: the field's bottom edge when the list opens below
	/// it, its top edge when the list opens above.</summary>
	public static float AnchorY(float fieldEdge, float layerBottom, float layerHeight) =>
		layerHeight > 0f ? (fieldEdge - layerBottom) / layerHeight : 0.5f;
}
