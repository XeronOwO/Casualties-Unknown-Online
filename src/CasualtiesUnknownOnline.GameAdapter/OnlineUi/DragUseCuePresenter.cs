using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Draws the drag-use cue on the game's own typography: the row label's font, material and size, cloned from
/// the game's own prefab (the same route every Online UI label takes — a programmatic TMP in this build parses
/// its string into zero glyphs, measured 2026-10-25). The label is parented under the game's MAIN canvas, the
/// same parent every other Online UI surface uses, so it takes the canvas scaler's scale with it: a label of the
/// game's own font size drawn on a plain overlay canvas comes out noticeably larger (reported 2026-11-07).
/// The plugin decides WHEN and WHAT; this type only owns the surface.
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

		// The canvas' own scale: screen pixels are canvas units times the scale factor, so the point is divided
		// by it. Unity screen points have the origin at the BOTTOM-LEFT, the same convention as the canvas, so
		// the point needs no flip; the label sits up-right of the cursor and is clamped so a cursor at the edge
		// still leaves it on screen.
		var scale = ParentCanvasScale();
		var width = LabelWidth * scale;
		var height = LabelHeight * scale;
		var x = Mathf.Clamp(screenX + CursorOffset, 0f, Mathf.Max(0f, Screen.width - width));
		var y = Mathf.Clamp(screenY + CursorOffset, 0f, Mathf.Max(0f, Screen.height - height));
		_label.rectTransform.anchoredPosition = new Vector2(x / scale, y / scale);
	}

	public void Hide()
	{
		if (_label != null)
		{
			_label.gameObject.SetActive(false);
		}
	}

	private float ParentCanvasScale()
	{
		var canvas = _label != null ? _label.GetComponentInParent<Canvas>() : null;
		return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
	}

	private bool Ensure()
	{
		if (_label != null && _label.transform.parent != null)
		{
			return true;
		}

		// The game's own canvas, the parent every Online UI surface takes: it carries the CanvasScaler, the
		// layer and the sorting order the player's eye already knows. A label under it is the same size as the
		// game's own text by construction, not by copying numbers.
		var parent = OnlineUiNativeSurfaceHost.FindMainCanvasTransform();
		if (parent == null)
		{
			return false;
		}

		OnlineUiControlFactory.ReadRowTemplate(parent, out var typography, out _, out _, out _);
		var label = OnlineUiSurfaceParts.CreateLabel("CUO Drag Use Cue Label", parent, typography, OnlineUiTextStyle.Default);
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
