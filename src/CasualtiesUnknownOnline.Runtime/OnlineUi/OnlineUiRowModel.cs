using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One row of the Online UI window's page (ticket online-ui-art-and-controls-overhaul, S2b): the elements
/// that belong on one line of a page, in reading order. A page is a list of rows, and a row holding one
/// element is the ordinary full-width line — which is how most of the six pages read.
///
/// <para>
/// The row does not decide how it is laid out: it carries the elements' width hints and the adapter
/// breaks a row that does not fit (<see cref="OnlineUiRowLayout.LineOf(IReadOnlyList{float}, float)"/>).
/// Layout is the adapter's business, exactly as the pixels are. The one measurement a row carries is
/// <see cref="Gap"/>: a row with no elements IS the room between two blocks, because a uGUI layout group
/// spaces its children evenly and has no margin on a single one — which is how a heading gets the room it
/// owns (<see cref="OnlineUiWindowLayout.SectionGap"/> and its neighbours).
/// </para>
/// </summary>
public readonly record struct OnlineUiRowModel(IReadOnlyList<OnlineUiElementModel> Elements, float Gap = 0f, float Indent = 0f)
{
	/// <summary>One row holding <paramref name="elements"/>, in order.</summary>
	public static OnlineUiRowModel Of(params OnlineUiElementModel[] elements) => new(elements);

	/// <summary>
	/// The same row stepped in from its line's left edge by <paramref name="indent"/> canvas units —
	/// a block's rows sit under the block's own heading, and the step is the row's, not a run of
	/// spaces inside a label (a label takes the row's remaining width and would push its neighbours
	/// to the right edge, measured 2026-11-08).
	/// </summary>
	public OnlineUiRowModel WithIndent(float indent) => this with { Indent = indent };

	/// <summary>A row holding nothing but room: <paramref name="gap"/> canvas units of it. The page's vertical
	/// rhythm is written with these, from the layout's own scale rather than from a drawer's taste.</summary>
	public static OnlineUiRowModel Space(float gap) => new([], gap);

	/// <summary>Whether this row is room rather than content.</summary>
	public bool IsSpace => Elements.Count == 0;
}
