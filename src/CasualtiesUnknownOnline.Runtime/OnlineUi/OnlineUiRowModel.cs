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
/// Layout is the adapter's business, exactly as the pixels are.
/// </para>
/// </summary>
public readonly record struct OnlineUiRowModel(IReadOnlyList<OnlineUiElementModel> Elements)
{
	/// <summary>One row holding <paramref name="elements"/>, in order.</summary>
	public static OnlineUiRowModel Of(params OnlineUiElementModel[] elements) => new(elements);
}
