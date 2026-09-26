using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// How a model row fits into the window's content width (ticket online-ui-art-and-controls-overhaul, S2b):
/// the greedy line-breaking the adapter lays rows out with. It is pure, so the rule that decides whether
/// a member's twelve action buttons form two lines or three is tested without a Unity runtime — the
/// adapter only consumes the answer.
///
/// <para>
/// A width hint of 0 (or less) means "this control takes its own natural width" and counts as nothing
/// towards the line: an ordinary text label therefore never forces a break, which is what keeps
/// <c>[label, button]</c> rows on one line. A hinted element is packed onto the current line while it
/// fits and starts a new line when it does not; an element wider than the whole content width still gets
/// a line of its own rather than looping.
/// </para>
/// </summary>
public static class OnlineUiRowLayout
{
	/// <summary>
	/// The visual line each element lands on, given the elements' width hints, the content width and the
	/// gap the consumer's layout puts between two elements of one line. The line indices start at 0 and
	/// never skip a number, so the adapter can group by them directly.
	/// </summary>
	public static int[] LineOf(IReadOnlyList<float> widths, float contentWidth, float spacing = 0f)
	{
		var lines = new int[widths.Count];
		var line = 0;
		var used = 0f;

		for (var index = 0; index < widths.Count; index++)
		{
			var hint = widths[index];
			var width = hint > 0f ? hint : 0f;

			// An element that is not first on its line also costs the gap before it, which is what the
			// consuming layout group will add: leaving that out makes a row that exactly fills the content
			// wrap in the layout while this rule still calls it one line.
			var candidate = used > 0f ? used + spacing + width : width;

			// `used > 0` keeps an over-wide element on a line of its own instead of advancing forever.
			if (used > 0f && contentWidth > 0f && candidate > contentWidth)
			{
				line++;
				used = width;
			}
			else
			{
				used = candidate;
			}

			lines[index] = line;
		}

		return lines;
	}
}
