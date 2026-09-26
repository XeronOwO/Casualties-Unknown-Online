using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The launcher control of the live Online UI surface (ticket online-ui-art-and-controls-overhaul, S2a):
/// the game's own button-row prefab stretched into the launcher's screen rect in the top-right corner,
/// captioned and faded by what the Runtime pushed this frame.
///
/// <para>
/// Reusing the game's prefab is what makes the control read as the game: the background sprite, its
/// <c>Image.type</c> and pixels-per-unit multiplier, the 9-slice and the caption's font asset are all
/// serialised in that prefab, so nothing here has to guess them (and nothing has to wait for the probe's
/// reading either). The rect is the one the IMGUI launcher occupied — a right margin of
/// <see cref="RightMargin"/>, a top margin of <see cref="TopMargin"/>, <see cref="Width"/> by
/// <see cref="Height"/> — expressed as a top-right anchor, so it stays where the player last saw it.
/// </para>
///
/// <para>
/// The pointer fact is POLLED from the rect and the current pointer rather than left to uGUI's
/// enter/exit callbacks: those fire on pointer movement, so a launcher that appears under a stationary
/// pointer would never report the hover that keeps it opaque. The click is the game's button itself, so
/// press-inside/release-inside and the game's own button visuals come with it; what a click MEANS is not
/// decided here — the surface queues the fact and the Runtime decides.
/// </para>
/// </summary>
internal sealed class OnlineUiLauncherView
{
	/// <summary>The game's own button row — the prefab its settings screen uses for a language row: a
	/// <c>Button</c> on the root and the caption on child 0 (SettingsMenu.cs, the language row).</summary>
	internal const string PrefabPath = "Special/GameSettingLanguage";

	internal const float RightMargin = 12f;
	internal const float TopMargin = 12f;
	internal const float Width = 158f;
	internal const float Height = 34f;

	private readonly RectTransform _rect;
	private readonly CanvasGroup _group;
	private readonly TextMeshProUGUI? _label;
	private readonly Canvas? _canvas;

	private bool _hovered;
	private string _text = "";
	private float _alpha = float.NaN;

	private OnlineUiLauncherView(RectTransform rect, CanvasGroup group, TextMeshProUGUI? label, Canvas? canvas, bool usesGamePrefab)
	{
		_rect = rect;
		_group = group;
		_label = label;
		_canvas = canvas;
		UsesGamePrefab = usesGamePrefab;
	}

	/// <summary>False when the game's own button prefab could not be loaded and the plain fallback was
	/// used — the caller logs the miss once instead of the style gap passing unnoticed.</summary>
	internal bool UsesGamePrefab { get; }

	/// <summary>
	/// Builds the launcher under <paramref name="parent"/> (CUO's canvas), or returns null when the
	/// game's own row prefab cannot be used at all. <paramref name="clicked"/> is invoked on every click
	/// of the control, after the game's own button decided it was a click.
	/// </summary>
	internal static OnlineUiLauncherView? TryCreate(Transform parent, Action clicked)
	{
		var prefab = Resources.Load<GameObject>(PrefabPath);
		var root = prefab != null ? Object.Instantiate(prefab, parent) : CreatePlainButton(parent);
		if (root.transform is not RectTransform rect)
		{
			Object.Destroy(root);
			return null;
		}

		root.name = "CUO Online UI Launcher";
		rect.anchorMin = new Vector2(1f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(1f, 1f);
		rect.anchoredPosition = new Vector2(-RightMargin, -TopMargin);
		rect.sizeDelta = new Vector2(Width, Height);
		rect.localScale = Vector3.one;

		// The whole launcher fades as one surface, caption included: the idle fade's alpha used to be
		// folded into the IMGUI frame's colours and its label style separately, which is exactly the
		// half-fade the first cut of that rule shipped; a group cannot fade one half and not the other.
		var group = root.AddComponent<CanvasGroup>();

		var graphic = root.GetComponent<Graphic>();
		if (graphic == null)
		{
			// A prefab the game changed may carry no raycast target of its own; an invisible image keeps
			// the button clickable (uGUI hit-tests the graphic, not its alpha).
			var image = root.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0f);
			graphic = image;
		}

		var button = root.GetComponent<Button>();
		if (button == null)
		{
			button = root.AddComponent<Button>();
		}

		button.targetGraphic = graphic;
		button.onClick.AddListener(() => clicked());

		var label = root.transform.childCount > 0 && root.transform.GetChild(0) != null
			? root.transform.GetChild(0).GetComponent<TextMeshProUGUI>()
			: null;
		if (label == null)
		{
			label = CreateCaption(root.transform);
		}

		// The ROOT canvas decides the render mode (a nested canvas reports its own default), and the
		// screen-point test needs the camera that canvas renders through — null for a screen-space
		// overlay, which is what the game's own UI is.
		var canvas = root.GetComponentInParent<Canvas>();
		return new OnlineUiLauncherView(rect, group, label, canvas != null ? canvas.rootCanvas : null, prefab != null);
	}

	/// <summary>Applies one frame: the caption (only when it changed) and the idle fade's opacity (only
	/// when it changed — a steady alpha lasts seconds, and every assignment dirties the canvas).</summary>
	internal void Apply(string caption, float alpha)
	{
		if (_label != null && _text != caption)
		{
			_text = caption;
			_label.text = caption;
		}

		var clamped = Mathf.Clamp01(alpha);
		if (!clamped.Equals(_alpha))
		{
			_alpha = clamped;
			_group.alpha = clamped;
		}
	}

	/// <summary>
	/// Polls the pointer against the launcher's rect and queues the hover fact when it flips. Alpha does
	/// not stop uGUI from hit-testing, so a translucent launcher stays hoverable and clickable — which is
	/// the whole point of the fade: the launcher must come back to full opacity when the player reaches
	/// for it.
	/// </summary>
	internal void PollHover(Queue<OnlineUiIntent> intents)
	{
		var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
		var hovered = RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera);
		if (hovered == _hovered)
		{
			return;
		}

		_hovered = hovered;
		intents.Enqueue(new OnlineUiIntent(
			hovered ? OnlineUiIntentKind.LauncherHoverEntered : OnlineUiIntentKind.LauncherHoverLeft));
	}

	private static GameObject CreatePlainButton(Transform parent)
	{
		// The game moved the row prefab: a plain uGUI button keeps the Online UI reachable (an
		// unreachable launcher means an unreachable window), and the surface's log line names the miss.
		var root = new GameObject("CUO Online UI Launcher", typeof(RectTransform), typeof(Image), typeof(Button));
		root.transform.SetParent(parent, worldPositionStays: false);
		return root;
	}

	private static TextMeshProUGUI CreateCaption(Transform parent)
	{
		var caption = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
		caption.transform.SetParent(parent, worldPositionStays: false);
		var rect = (RectTransform)caption.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		var text = caption.GetComponent<TextMeshProUGUI>();
		text.alignment = TextAlignmentOptions.Center;
		text.fontSize = 13f;
		text.raycastTarget = false;
		if (TMP_Settings.defaultFontAsset != null)
		{
			text.font = TMP_Settings.defaultFontAsset;
		}

		return text;
	}
}
