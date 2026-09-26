using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// One model row's controls on the Online UI window's surface (ticket
/// online-ui-art-and-controls-overhaul, S2b): the line containers the row wrapped into, and the element
/// views in model order — the two are crossed by the wrap, which is why they are kept apart here rather
/// than in the window.
///
/// <para>
/// The row owns nothing but its objects: which line an element lands on comes from the Runtime's wrapping
/// rule, what each element shows comes from the model, and the window above reconciles both frame by
/// frame. Making the row have exactly <paramref name="count"/> lines reports whether that changed
/// anything, which is the window's signal to put its lines back in model order (Unity appends a new
/// object to the end of its parent).
/// </para>
/// </summary>
internal sealed class OnlineUiWindowRowView
{
	private readonly float _lineSpacing;

	internal OnlineUiWindowRowView(float lineSpacing)
	{
		_lineSpacing = lineSpacing;
	}

	/// <summary>The row's line containers, in order; one line holds the elements that fit side by side.</summary>
	internal List<GameObject> Lines { get; } = [];

	/// <summary>The row's element views, in model order.</summary>
	internal List<OnlineUiControlView> Elements { get; } = [];

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
