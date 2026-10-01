using System;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// How wide one control of a page is (ticket online-ui-layout-and-input-detail-pass, its acceptance pass
/// rework). The model's width is a FLOOR — a hint its drawer declared, which must never be undercut because
/// the wrap and the rows around this control were decided from it — and above that floor the answer is the
/// CONTENT: a caption TMP measured, an option Toggle's own box, an input field's declared width.
///
/// <para>
/// It exists because the engine cannot make that decision for this surface. uGUI measures a stretched child
/// by the rect it already has, so asking a row's own layout group to size one of the game's controls — whose
/// value text, caret and viewport are stretched inside it — reads a width that the group itself just wrote:
/// the measurement feeds back into itself and the control grows without bound (batch `20261001-k`: a
/// dropdown 690,000 units wide, a field 116,000, against a page 984 wide). The width is therefore declared
/// here, from things that do NOT depend on the box: the model's floor and the intrinsic width of the
/// content, which only ever shrink back to the floor.
/// </para>
///
/// <para>
/// It is pure and lives in the Runtime for the reason <see cref="OnlineUiRowLayout"/> does: the rule that
/// decides the geometry a player sees is settled and tested without a Unity runtime, and the adapter only
/// reads it.
/// </para>
/// </summary>
public static class OnlineUiControlBox
{
	/// <summary>The width a control takes on its line: the wider of what the model declared and what its own
	/// content needs, and never less than zero. A content the adapter could not measure (a control that is
	/// not one of the game's rows at all) reports 0 and leaves the floor as the whole answer.</summary>
	public static float EffectiveWidth(float modelFloor, float contentWidth) =>
		Math.Max(0f, Math.Max(modelFloor, contentWidth));

	/// <summary>The width of the box a control's own insides are stretched onto: the width the control takes
	/// minus the room its row keeps on both sides. A control narrower than that room reports 0 rather than a
	/// negative box — a stretched child with a negative size is a rect the player cannot see, which is the
	/// defect this rule exists to prevent.</summary>
	public static float InteriorWidth(float controlWidth, float padding)
	{
		var interior = controlWidth - (2f * padding);
		return interior > 0f ? interior : 0f;
	}
}
