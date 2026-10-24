using System;
using TMPro;
using UnityEngine;
using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The overlay's own construction helpers, lifted out of the view so the view stays a view: a marker's and
/// the readout's labels are built here, and the one colour conversion the overlay needs lives here too.
/// Nothing in this type holds state — the view's own two rules that DO read its state (the glyph fallback
/// and its once-per-mark warning) stay with the view.
/// </summary>
internal static class OnlineUiWorldOverlayParts
{
	/// <summary>The centre of a box given by its top-left corner, the way
	/// <see cref="NameplateLayout.AboveHead"/> hands it over.</summary>
	internal static Vector2 Centre(float x, float y, float width, float height) =>
		new(x + (width * 0.5f), y + (height * 0.5f));

	/// <summary>The readout's own labels: anchored to the top-left corner of the canvas, left-aligned, and
	/// never a raycast target — the overlay takes no input.</summary>
	internal static TextMeshProUGUI CreateHudLabel(
		string name,
		Transform parent,
		OnlineUiControlTypography typography,
		Vector2 anchoredPosition,
		Vector2 size)
	{
		var label = CreateLabel(name, parent, typography, TextAlignmentOptions.MidlineLeft);
		var rect = (RectTransform)label.transform;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;
		label.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted);
		label.gameObject.SetActive(false);
		return label;
	}

	/// <summary>
	/// A bottom-centre label: the spectator's strip about the player they are watching. The HUD labels anchor to
	/// the top-left because that is where the game's own readout sits; this one belongs on the bottom edge, so it
	/// anchors and pivots there instead of reusing the top-left position.
	/// </summary>
	internal static TextMeshProUGUI CreateBottomLabel(
		string name,
		Transform parent,
		OnlineUiControlTypography typography,
		float bottomMargin,
		Vector2 size)
	{
		var label = CreateLabel(name, parent, typography, TextAlignmentOptions.Center);
		var rect = (RectTransform)label.transform;
		rect.anchorMin = new Vector2(0.5f, 0f);
		rect.anchorMax = new Vector2(0.5f, 0f);
		rect.pivot = new Vector2(0.5f, 0f);
		rect.anchoredPosition = new Vector2(0f, bottomMargin);
		rect.sizeDelta = size;
		label.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted);
		label.gameObject.SetActive(false);
		return label;
	}


	/// <summary>A label on the game's own font, at the size the game's row prefab implies for the muted style —
	/// the same construction the window's and the panels' labels use (<see cref="OnlineUiSurfaceParts"/>) — with
	/// this overlay's own fit policy on top of it: one line, no auto-sizing, and overflow rather than a clip or
	/// an ellipsis (see the class doc), and a marker that never swallows a click meant for the world.</summary>
	internal static TextMeshProUGUI CreateLabel(
		string name,
		Transform parent,
		OnlineUiControlTypography typography,
		TextAlignmentOptions alignment)
	{
		var text = OnlineUiSurfaceParts.CreateLabel(name, parent, typography, OnlineUiTextStyle.Muted);
		text.alignment = alignment;
		text.enableWordWrapping = false;
		text.enableAutoSizing = false;
		text.overflowMode = TextOverflowModes.Overflow;
		return text;
	}

	internal static void SetText(TextMeshProUGUI label, string text)
	{
		if (!string.Equals(label.text, text, StringComparison.Ordinal))
		{
			label.text = text;
		}
	}

	internal static Color ToColor(OnlineUiNativeRgba color) => new(color.R, color.G, color.B, color.A);
}
