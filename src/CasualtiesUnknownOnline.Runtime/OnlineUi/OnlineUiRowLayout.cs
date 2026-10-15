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
///
/// <para>
/// An element the model marks <see cref="OnlineUiElementModel.NewLine"/> starts the line BELOW the one
/// its predecessor is on whether or not it would have fitted, which is the shape a paired control is
/// written in: its name on the first line, its slider and its value together on the second (ticket
/// online-ui-paired-controls-and-page-scroll, S3). The mark is a floor on the wrap, never a replacement
/// for it — a marked element too wide for the line it was sent to wraps again — and a mark on the FIRST
/// element of a row changes nothing, because there is no line above it to leave and the answers have to
/// stay a run of line indices that never skips a number.
/// </para>
///
/// <para>
/// A marked element does not only start a line: it HOLDS it, and the elements the fit puts beside it on
/// that line are its own (<see cref="LineOwners"/>). That is what makes a paired control one row rather
/// than a control that happens to be preceded by its name.
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

	/// <summary>
	/// The same rule, with the model's own breaks: an element whose <paramref name="newLine"/> is set starts
	/// the line below the one before it. That is the whole difference — the fit decides every other line —
	/// and it lives here rather than in the adapter for the reason the rest of the rule does.
	///
	/// <para>
	/// A break on an element the fit ALREADY sent to a line of its own changes nothing (it is on the line
	/// below its predecessor either way), and every element after a break moves down with it: the answer has
	/// to stay a run of indices that never skips a number, so the lines the fit produced are shifted by the
	/// breaks that came before them rather than written one at a time.
	/// </para>
	/// </summary>
	public static int[] LineOf(IReadOnlyList<float> widths, IReadOnlyList<bool> newLine, float contentWidth, float spacing = 0f)
	{
		var fitted = LineOf(widths, contentWidth, spacing);
		var lines = new int[fitted.Length];
		var shift = 0;
		for (var index = 0; index < fitted.Length; index++)
		{
			var previous = index > 0 ? lines[index - 1] : -1;
			if (index > 0 && index < newLine.Count && newLine[index] && fitted[index] + shift <= previous)
			{
				shift++;
			}

			lines[index] = fitted[index] + shift;
		}

		return lines;
	}

	/// <summary>
	/// The element that OWNS each element's line, or <c>-1</c> for a line nothing owns: a control the model
	/// sent to a line of its own — the paired shape <see cref="OnlineUiElementModel.Paired"/> writes — owns
	/// that line, and every element the fit put beside it on the same line belongs to it. It is the second
	/// half of the break rule above: that one says where a paired control's line starts, this says what is on
	/// it.
	///
	/// <para>
	/// The adapter reads it to lay those elements out INSIDE the row they belong to rather than beside it,
	/// which is what gives the row the page's own width instead of the width its own content happens to need
	/// (ticket online-ui-paired-controls-and-page-scroll; the acceptance reading of batch `20261015-c` is a
	/// paired row 132 units wide inside a 984-unit page, with the value field bound to its slider — a row of
	/// its own, 390 units wide — drawn from 268 units past that box). An element that landed on a line the FIT
	/// produced owns nothing — such a line is the wrap's, not a control's — and a mark on the first element of a
	/// row owns that row's first line, exactly as the break rule lets it.
	/// </para>
	/// </summary>
	public static int[] LineOwners(IReadOnlyList<bool> newLine, IReadOnlyList<int> lines)
	{
		var owners = new int[lines.Count];
		var owner = -1;
		for (var index = 0; index < lines.Count; index++)
		{
			if (index > 0 && lines[index] != lines[index - 1])
			{
				owner = -1;
			}

			if (index < newLine.Count && newLine[index])
			{
				owner = index;
			}

			owners[index] = owner;
		}

		return owners;
	}
}
