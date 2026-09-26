namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What one element of the Online UI window's display list is (ticket
/// online-ui-art-and-controls-overhaul, S2b — the window family moves onto the game's own controls).
///
/// <para>
/// The Runtime names the SHAPE of the control and nothing else: which of the game's own prefabs draws
/// it is the adapter's decision (that is where the game's types live), and what an interaction MEANS is
/// the plugin's (it owns the actions). The set is exactly what the six pages need — a value that is not
/// here cannot be shown, which is what keeps the model a closed vocabulary instead of a UI toolkit.
/// </para>
/// </summary>
public enum OnlineUiElementKind
{
	/// <summary>A read-only line of text.</summary>
	Label,

	/// <summary>A push button: one caption, one click.</summary>
	Button,

	/// <summary>A single-line text field the player types into.</summary>
	TextField,

	/// <summary>A checkbox with a caption.</summary>
	Toggle,

	/// <summary>A dropdown: it opens on click, tracks the pointer, and closes on click-away, Escape and
	/// selection — the control the IMGUI shell never had.</summary>
	Dropdown,

	/// <summary>A slider over a continuous range, with its value shown beside it.</summary>
	Slider,

	/// <summary>A block of one colour (ticket online-ui-art-and-controls-overhaul, S3): the colour
	/// picker's swatch. With an id it is a control the player clicks to choose that colour; with no id it
	/// is the plain preview of the colour the player carries now, which reports nothing.</summary>
	ColorSwatch,
}
