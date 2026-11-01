using CasualtiesUnknownOnline.Runtime.OnlineUi;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The CUO Online UI visual theme: a dark, translucent "operator console" look
/// with a muted amber accent and cyan status highlights. All styles are created
/// lazily from the current GUI skin so the theme works with repeated scene
/// loads and never needs asset bundles.
///
/// <para>
/// What is left here after S6 is the palette and the two styles the command console overlay needs: it is
/// the only surface CUO still draws in IMGUI. The IMGUI panel frame, its close/tab/section styles and the
/// styles of the member card retired with the quick panel and the player context menu, which are controls
/// of the game's own surface now (S5), and the world-space overlays' label and status styles retired with
/// the overlays themselves, which are labels on that surface's canvas (S6) — so this file no longer paints
/// a panel of its own at all.
/// </para>
/// </summary>
internal static class OnlineUiTheme
{
	internal static readonly Color OverlayPanel = new(0.02f, 0.02f, 0.02f, 0.58f);

	internal static readonly Color Accent = new(0.85f, 0.72f, 0.38f, 1f);

	internal static readonly Color Muted = new(0.62f, 0.67f, 0.72f, 1f);

	internal static readonly Color Text = new(0.92f, 0.93f, 0.94f, 1f);

	internal static readonly Color Positive = new(0.44f, 0.82f, 0.56f, 1f);

	internal static readonly Color Warning = new(0.9f, 0.66f, 0.32f, 1f);

	internal static readonly Color Error = new(0.9f, 0.38f, 0.34f, 1f);

	private static GUIStyle? _button;

	private static GUIStyle? _mutedLabel;

	private static GUIStyle? _scrollBackground;

	private static GUIStyle? _label;

	private static GUIStyle? _textField;

	internal static GUIStyle Button() => _button ??= CreateButton();

	internal static GUIStyle MutedLabel() => _mutedLabel ??= CreateMutedLabel();

	/// <summary>
	/// The ordinary label this panel draws with. The game's <c>GUI.skin.label</c> brings its own font size and colour, which is
	/// not this window's text; the mod UI's labels go through this, so a mod's window looks like the rest of the panel.
	/// </summary>
	internal static GUIStyle Label()
	{
		if (_label is not null)
		{
			return _label;
		}

		_label = new GUIStyle(GUI.skin.label)
		{
			fontSize = 13,
			wordWrap = true,
		};
		_label.normal.textColor = Text;
		return _label;
	}

	/// <summary>The same argument for text fields: the game's skin decides the box, this panel decides its text.</summary>
	internal static GUIStyle TextField()
	{
		if (_textField is not null)
		{
			return _textField;
		}

		_textField = new GUIStyle(GUI.skin.textField)
		{
			fontSize = 13,
			padding = new RectOffset(6, 6, 4, 4),
		};
		_textField.normal.textColor = Text;
		return _textField;
	}

	/// <summary>
	/// The suggestion list's scroll view background. The GAME's own <c>GUI.skin.scrollView</c> carries its UI's padding, and using
	/// it indented every row by that padding (measured 2026-11-02: the list's buttons started a fifth of the panel's width to the
	/// right of the input prompt below them). A zero-padding style puts the rows against the list's own left edge.
	/// </summary>
	internal static GUIStyle ScrollBackground()
	{
		if (_scrollBackground is not null)
		{
			return _scrollBackground;
		}

		_scrollBackground = new GUIStyle(GUI.skin.scrollView)
		{
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			border = new RectOffset(0, 0, 0, 0),
		};
		_scrollBackground.normal.background = null;
		return _scrollBackground;
	}

	/// <summary>
	/// One theme colour as the plain value the window model and the game's own controls carry: the
	/// palette stays in this file (the one place that owns it), and the surface receives the channels
	/// instead of a palette of its own (ticket online-ui-art-and-controls-overhaul, S2b).
	/// </summary>
	internal static OnlineUiNativeRgba ToRgba(Color color) => new(color.r, color.g, color.b, color.a);

	/// <summary>
	/// The overlay background for the transient/compact surfaces CUO still draws itself — the command
	/// console, whose history panel, notification, suggestion list and tooltip all use it: the palette's
	/// own 0.58 alpha, ALPHA-BLENDED, so the world stays visible behind the history. The blend flag is the
	/// point of the overload: the explicit-colour <c>DrawTexture</c> hands the flag to the native draw
	/// verbatim, and an unblended draw leaves that alpha with nothing to apply it.
	/// </summary>
	internal static void DrawOverlayBackground(Rect rect) =>
		GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, OverlayPanel, 0f, 0f);

	private static GUIStyle CreateButton()
	{
		var style = new GUIStyle(GUI.skin.button)
		{
			fontSize = 13,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 5, 5),
			// The game's button style carries a MARGIN, and a margin offsets every button inside the layout that draws it: the
			// console's suggestion rows kept an indent after their scroll-view padding was removed, because this copy kept the
			// margin (measured 2026-11-02). Zero here means the layout decides the edge, not the skin.
			margin = new RectOffset(0, 0, 0, 0),
		};
		style.normal.textColor = Text;
		style.hover.textColor = Accent;
		style.active.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateMutedLabel()
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			richText = true,
		};
		style.normal.textColor = Muted;
		return style;
	}
}
