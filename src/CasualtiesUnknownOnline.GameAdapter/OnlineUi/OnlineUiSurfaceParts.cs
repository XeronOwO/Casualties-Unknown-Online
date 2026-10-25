using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The parts every CUO surface is built from: a bare rect carrying the components a band, a viewport or a
/// content area needs, a label set in the game's own typography, and a rect stretched inside its parent with
/// the room each edge keeps.
///
/// <para>
/// It exists because the window and the two panels each carried their OWN copy of these three — byte for byte
/// the same, which is exactly how a fix lands on one surface and not on its sibling (the cycle of 2026-10-15
/// found them). It is construction only: nothing here decides a size, an anchor policy, a colour or a fit
/// policy, and the game's own art still comes from <see cref="OnlineUiControlFactory"/>.
/// </para>
/// </summary>
internal static class OnlineUiSurfaceParts
{
	/// <summary>
	/// The layer the game's own UI lives on. It is not cosmetic: <c>UIUtil.IsPointerOverUIElement</c>
	/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/UIUtil.cs</c>) walks the EventSystem's raycast results and returns true only for a
	/// hit whose GameObject is on THIS layer — so a CUO panel drawn on the default layer is invisible to the game's own
	/// "is the pointer over UI" gate and a click on its blank area reaches the world (the user's 2026-10-25 report: the character
	/// mined through the context menu).
	/// </summary>
	internal static int UiLayer => LayerMask.NameToLayer("UI");

	/// <summary>A new rect under <paramref name="parent"/>, carrying <paramref name="components"/> beside it, on the game's UI layer so
	/// the game's pointer-over-UI gate sees it (<see cref="UiLayer"/>).</summary>
	internal static RectTransform CreateRect(string name, Transform parent, params Type[] components)
	{
		var all = new Type[components.Length + 1];
		all[0] = typeof(RectTransform);
		Array.Copy(components, 0, all, 1, components.Length);
		var go = new GameObject(name, all) { layer = UiLayer };
		go.transform.SetParent(parent, worldPositionStays: false);
		return (RectTransform)go.transform;
	}

	/// <summary>
	/// A label on the game's own font, at the size its style asks for. It refuses the raycast: a label that
	/// swallowed a click meant for the control beside it is a defect of its own, and every surface that labels
	/// something with it wants the same thing.
	/// </summary>
	internal static TextMeshProUGUI CreateLabel(
		string name,
		Transform parent,
		OnlineUiControlTypography typography,
		OnlineUiTextStyle style)
	{
		// The game's own label OBJECT when the surface has one: a TMP built with `new GameObject` parses its string into zero glyphs in this
		// build (see OnlineUiControlTypography's note), while the game's own label draws. The clone is stripped of the row's layout pieces so
		// the label only obeys the layout its new parent gives it.
		var go = typography.LabelTemplate != null
			? Object.Instantiate(typography.LabelTemplate, parent)
			: new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		go.name = name;
		go.layer = UiLayer;
		var layoutElement = go.GetComponent<LayoutElement>();
		if (layoutElement != null)
		{
			Object.Destroy(layoutElement);
		}

		var fitter = go.GetComponent<ContentSizeFitter>();
		if (fitter != null)
		{
			Object.Destroy(fitter);
		}

		go.SetActive(true);
		go.transform.SetParent(parent, worldPositionStays: false);
		var text = go.GetComponent<TextMeshProUGUI>();
		text.text = "";
		text.fontSize = typography.SizeOf(style);
		text.fontStyle = OnlineUiControlTypography.WeightOf(style);
		text.raycastTarget = false;
		if (typography.Font != null)
		{
			text.font = typography.Font;
		}

		// The game's own material, not the TMP default.
		if (typography.FontMaterial != null)
		{
			text.fontSharedMaterial = typography.FontMaterial;
		}

		return text;
	}

	/// <summary>Stretches a rect inside its parent, with the given insets from each edge.</summary>
	internal static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
	}
}
