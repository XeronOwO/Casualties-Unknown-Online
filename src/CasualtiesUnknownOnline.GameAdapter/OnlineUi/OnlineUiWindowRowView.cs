using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// One model row's controls on a CUO surface (ticket online-ui-art-and-controls-overhaul, S2b): the line
/// containers the row wrapped into, and the element views in model order — the two are crossed by the wrap,
/// which is why they are kept apart here rather than in the window.
///
/// <para>
/// The row owns nothing but its objects: which line an element lands on comes from the Runtime's wrapping
/// rule, what each element shows comes from the model, and the surface above reconciles both frame by
/// frame. Making the row have exactly <paramref name="count"/> lines reports whether that changed
/// anything, which is the surface's signal to put its lines back in model order (Unity appends a new
/// object to the end of its parent).
/// </para>
///
/// <para>
/// The page's own control COLUMN is here too (<see cref="ColumnWidthOf"/> and
/// <see cref="AlignToColumn"/>), because it is the same kind of fact as the row's lines: a shape the whole
/// page shares and the surface writes back onto each of its rows. The window and the two panels carry it
/// through this one place so a control cannot line up on one surface and not on its sibling.
/// </para>
/// </summary>
internal sealed class OnlineUiWindowRowView
{
	/// <summary>How much of a surface's width a name column may take.</summary>
	private const float ColumnShare = 0.8f;

	private readonly float _lineSpacing;

	internal OnlineUiWindowRowView(float lineSpacing)
	{
		_lineSpacing = lineSpacing;
	}

	/// <summary>The row's line containers, in order; one line holds the elements that fit side by side.</summary>
	internal List<GameObject> Lines { get; } = [];

	/// <summary>The row's element views, in model order.</summary>
	internal List<OnlineUiControlView> Elements { get; } = [];

	/// <summary>Which element OWNS each element's line, as the surface's last pass read it
	/// (<see cref="OnlineUiRowLayout.LineOwners"/>); empty before the first pass.</summary>
	internal IReadOnlyList<int> Owners { get; set; } = [];

	internal bool EnsureLines(Transform parent, int count)
	{
		var changed = false;
		while (Lines.Count > count)
		{
			Object.Destroy(Lines[Lines.Count - 1]);
			Lines.RemoveAt(Lines.Count - 1);
			changed = true;
		}

		while (Lines.Count < count)
		{
			var go = new GameObject("Line", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			go.transform.SetParent(parent, worldPositionStays: false);
			var group = go.GetComponent<HorizontalLayoutGroup>();
			group.spacing = _lineSpacing;
			group.childAlignment = TextAnchor.MiddleLeft;
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = false;
			group.childForceExpandHeight = false;
			Lines.Add(go);
			changed = true;
		}

		return changed;
	}

	/// <summary>Gives a line a height of its own (the page's breathing room) or clears it (-1 = the
	/// line's own height, which is its tallest element).</summary>
	internal void SetGap(int line, float height)
	{
		var element = Lines[line].GetComponent<LayoutElement>();
		if (!element.preferredHeight.Equals(height))
		{
			element.preferredHeight = height;
		}
	}

	/// <summary>
	/// The width a surface's control column takes (ticket online-ui-paired-controls-and-page-scroll, S3): the
	/// widest name any of its table rows carries, so every control below that column starts at the same place
	/// however long the names above it are. A PAIRED row's own name does not count — it is on a line of its
	/// own and takes the whole width — and a page with nothing to align answers 0, which leaves every row the
	/// flexible label it had.
	///
	/// <para>
	/// It is read from the element MODELS rather than from what the views drew, and every name is measured by
	/// the view that was built for that element (<paramref name="nameWidth"/> is handed the row, the element
	/// and its index) — never off a box a layout group has written, which is the feedback this surface removes
	/// everywhere else.
	/// </para>
	/// </summary>
	internal static float ColumnWidthOf(
		IReadOnlyList<OnlineUiRowModel> rows,
		Func<int, int, OnlineUiElementModel, float> nameWidth,
		float contentWidth)
	{
		var widest = 0f;
		for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
		{
			var elements = rows[rowIndex].Elements;
			for (var index = 0; index < elements.Count; index++)
			{
				if (elements[index].NewLine)
				{
					continue;
				}

				widest = Mathf.Max(widest, nameWidth(rowIndex, index, elements[index]));
			}
		}

		// The column never eats the room a control needs: names that want more than four fifths of the width
		// keep their controls whole and wrap instead, which is the worse-looking but readable end of the
		// trade.
		return Mathf.Min(widest, contentWidth * ColumnShare);
	}

	/// <summary>Gives every element of this row the page's own control column: the width its NAME keeps, so the
	/// controls of the page line up however long the names are (ticket
	/// online-ui-paired-controls-and-page-scroll, S3). It is a second pass over the elements — the surface
	/// measures the whole page's names first, because a row cannot know what the rows below it carry — with
	/// each element's width and model handed back in, so nothing is measured twice.
	///
	/// <para>
	/// The elements a paired row HOLDS keep none of that column: the column lines the page's own rows up, while
	/// what closes a paired row is its value, sitting at that row's trailing edge. Handing it the column would
	/// push it a name's width off the page's edge and leave the control it mirrors stranded at the far end of a
	/// hole — the reading its own pass already produced (<c>columnWidth: 0f</c>) is what stands.
	/// </para>
	/// </summary>
	internal void AlignToColumn(IReadOnlyList<OnlineUiElementModel> elements, float[] widths, float columnWidth)
	{
		if (columnWidth <= 0f)
		{
			return;
		}

		for (var index = 0; index < Elements.Count && index < elements.Count; index++)
		{
			if (index < Owners.Count && Owners[index] >= 0 && Owners[index] != index)
			{
				continue;
			}

			Elements[index].LayOutRow(elements[index], widths[index], columnWidth);
		}
	}

	/// <summary>
	/// What the element at <paramref name="index"/> is laid out UNDER: the line the wrap gave it, or the row
	/// that owns that line. An element the model sent to a line of its own owns it
	/// (<see cref="OnlineUiRowLayout.LineOwners"/>), and the elements the fit put beside it are laid out INSIDE
	/// its box — which is what makes that row the page's own width and puts the value field beside its slider
	/// at the row's trailing edge (ticket online-ui-paired-controls-and-page-scroll; the acceptance reading of
	/// batch `20261015-c` is a paired row 132 units wide inside a 984-unit page, its value field a row of its
	/// own drawn 268 units past that box).
	///
	/// <para>
	/// The owner lays its own box out FIRST — the surface walks a row's elements in model order — so the answer
	/// is this frame's rather than the last one's, and a row that cannot hold anything (a placeholder for a
	/// moved prefab) answers false and leaves its line-mates where they were.
	/// </para>
	/// </summary>
	internal Transform ParentOf(int index, int line, IReadOnlyList<int> owners)
	{
		var owner = index < owners.Count ? owners[index] : -1;
		if (owner >= 0 && owner != index && owner < Elements.Count && Elements[owner].OwnsItsLine)
		{
			return Elements[owner].Root.transform;
		}

		return Lines[line].transform;
	}

	internal bool DestroyElementsFrom(int keep)
	{
		if (Elements.Count <= keep)
		{
			return false;
		}

		for (var index = keep; index < Elements.Count; index++)
		{
			Elements[index].Destroy();
		}

		Elements.RemoveRange(keep, Elements.Count - keep);
		return true;
	}

	internal void Destroy()
	{
		foreach (var line in Lines)
		{
			Object.Destroy(line);
		}

		Lines.Clear();
		DestroyElementsFrom(0);
	}
}
