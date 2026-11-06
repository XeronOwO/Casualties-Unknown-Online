using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Draws the drag-use cue on the game's own typography: the row label's font, material and size, cloned from
/// the game's own prefab (the same route every Online UI label takes — a programmatic TMP in this build parses
/// its string into zero glyphs, measured 2026-10-25). The plugin decides WHEN and WHAT; this type only owns the
/// surface, so the label looks like the game's own text instead of an IMGUI box.
/// </summary>
internal sealed class DragUseCuePresenter : IDragUseCuePresenter
{
	private const float CursorOffset = 22f;
	private const float LabelWidth = 520f;
	private const float LabelHeight = 48f;

	private TextMeshProUGUI? _label;

	public void Show(string text, float screenX, float screenY)
	{
		if (string.IsNullOrEmpty(text))
		{
			Hide();
			return;
		}

		if (!Ensure())
		{
			return;
		}

		_label!.text = text;
		if (!_label.gameObject.activeSelf)
		{
			_label.gameObject.SetActive(true);
		}

		// ScreenSpaceOverlay canvas coordinates are screen pixels with the origin at the BOTTOM-LEFT, which is
		// Unity's own screen-point convention, so the point needs no flip here; the label sits up-right of the
		// cursor and is clamped so a cursor at the edge still leaves it on screen.
		var rect = _label.rectTransform;
		var x = Mathf.Clamp(screenX + CursorOffset, 0f, Mathf.Max(0f, Screen.width - LabelWidth));
		var y = Mathf.Clamp(screenY + CursorOffset, 0f, Mathf.Max(0f, Screen.height - LabelHeight));
		rect.anchoredPosition = new Vector2(x, y);
	}

	public void Hide()
	{
		if (_label != null)
		{
			_label.gameObject.SetActive(false);
		}
	}

	private bool Ensure()
	{
		if (_label != null)
		{
			return true;
		}

		var canvasObject = new GameObject("CUO Drag Use Cue", typeof(Canvas))
		{
			layer = LayerMask.NameToLayer("UI"),
		};
		var canvas = canvasObject.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 32000;
		Object.DontDestroyOnLoad(canvasObject);

		// The game's own typography: the row prefab's font, material and size. The clone of its label object is
		// what makes CJK actually render (see OnlineUiControlFactory's note).
		OnlineUiControlFactory.ReadRowTemplate(canvasObject.transform, out var typography, out _, out _, out _);
		var label = OnlineUiSurfaceParts.CreateLabel("CUO Drag Use Cue Label", canvasObject.transform, typography, OnlineUiTextStyle.Default);
		label.raycastTarget = false;
		label.alignment = TextAlignmentOptions.BottomLeft;
		var rect = label.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.zero;
		rect.pivot = Vector2.zero;
		rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
		_label = label;
		_label.gameObject.SetActive(false);
		return true;
	}
}
