using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;

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
	/// <summary>A new rect under <paramref name="parent"/>, carrying <paramref name="components"/> beside it.</summary>
	internal static RectTransform CreateRect(string name, Transform parent, params Type[] components)
	{
		var all = new Type[components.Length + 1];
		all[0] = typeof(RectTransform);
		Array.Copy(components, 0, all, 1, components.Length);
		var go = new GameObject(name, all);
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
		var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, worldPositionStays: false);
		var text = go.GetComponent<TextMeshProUGUI>();
		text.fontSize = typography.SizeOf(style);
		text.fontStyle = OnlineUiControlTypography.WeightOf(style);
		text.raycastTarget = false;
		if (typography.Font != null)
		{
			text.font = typography.Font;
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
