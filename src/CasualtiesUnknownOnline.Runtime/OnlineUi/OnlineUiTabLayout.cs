using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// How the window's tab strip shares its own width among its tabs (ticket online-ui-layout-and-input-detail-pass:
/// the English strip the user found on 2026-10-14). A tab is as wide as its own caption plus the room its row
/// keeps around that caption, and the strip is a fixed band of the window — so when the captions together need
/// more than the band has, something has to give. Before this rule nothing did: the tab row is one
/// horizontal group with <c>childForceExpandWidth</c> false, so it neither shrinks nor wraps, and the six
/// English tabs measured 1038.7 units inside a 984-unit page with the last one running 55 units past the
/// window's right edge (the live reading in batch `20261015-a`).
///
/// <para>
/// The rule gives up the ROOM first and the caption last. A tab that is over its own minimum loses a share of
/// the excess in proportion to the room it still has above that minimum, so the excess comes off the tabs that
/// have room to spare and no caption is ever cut; a tab whose minimum its model declared a floor for never goes
/// under the floor. A strip whose minimums do not fit either keeps those minimums and stays wider than its band
/// — cutting a caption is the worse of the two — which no shipping catalogue reaches, because a caption's
/// minimum is its own measured text plus the room the control's box keeps around it.
/// </para>
///
/// <para>
/// It is pure and lives in the Runtime for the reason <see cref="OnlineUiRowLayout"/> does: the shape the player
/// sees is decided and tested here, without a Unity runtime, and the adapter only reads the answer.
/// </para>
/// </summary>
public static class OnlineUiTabLayout
{
	/// <summary>
	/// The width each tab takes, in the order the tabs are in. <paramref name="naturalWidths"/> is what each tab
	/// asks for — its caption plus the room a row keeps around a control — and <paramref name="minimumWidths"/>
	/// the least it can be given without cutting its caption, read in parallel with it. A minimum that is
	/// missing, or not positive, means the tab cannot shrink at all: its natural width stands whatever the strip
	/// is, which is what a control with no caption of its own answers.
	/// </summary>
	public static float[] Widths(
		IReadOnlyList<float> naturalWidths,
		IReadOnlyList<float> minimumWidths,
		float stripWidth,
		float spacing = 0f)
	{
		var widths = new float[naturalWidths.Count];
		var asked = 0f;
		for (var index = 0; index < widths.Length; index++)
		{
			widths[index] = naturalWidths[index] > 0f ? naturalWidths[index] : 0f;
			asked += widths[index];
		}

		if (widths.Length == 0)
		{
			return widths;
		}

		// The gaps between the tabs are the strip's to pay for as well: a strip of six tabs spends five of them,
		// and leaving them out would make a strip that exactly fills its band wrap in the layout while this rule
		// still calls it one line (the same reason OnlineUiRowLayout counts the gap).
		var excess = asked + (spacing * (widths.Length - 1)) - stripWidth;
		if (excess <= 0f)
		{
			return widths;
		}

		var slack = new float[widths.Length];
		var totalSlack = 0f;
		for (var index = 0; index < widths.Length; index++)
		{
			var floor = index < minimumWidths.Count && minimumWidths[index] > 0f
				? minimumWidths[index]
				: widths[index];
			slack[index] = widths[index] > floor ? widths[index] - floor : 0f;
			totalSlack += slack[index];
		}

		if (totalSlack <= 0f)
		{
			return widths;
		}

		// One pass, and it cannot go under a floor: every tab gives up a share of its own slack, so the most a
		// tab loses is exactly the room it had above its minimum.
		var given = excess < totalSlack ? excess : totalSlack;
		for (var index = 0; index < widths.Length; index++)
		{
			widths[index] -= given * slack[index] / totalSlack;
		}

		return widths;
	}
}
