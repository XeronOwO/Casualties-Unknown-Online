using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The CUO Online UI visual theme: a dark, translucent "operator console" look
/// with a muted amber accent and cyan status highlights. All styles are created
/// lazily from the current GUI skin so the theme works with repeated scene
/// loads and never needs asset bundles.
/// </summary>
internal static class OnlineUiTheme
{
	internal static readonly Color Panel = new(0.035f, 0.045f, 0.06f, 0.96f);

	internal static readonly Color PanelLight = new(0.07f, 0.09f, 0.12f, 0.98f);

	internal static readonly Color OverlayPanel = new(0.02f, 0.02f, 0.02f, 0.58f);

	internal static readonly Color Border = new(0.72f, 0.58f, 0.24f, 0.9f);

	internal static readonly Color Accent = new(0.85f, 0.72f, 0.38f, 1f);

	internal static readonly Color Muted = new(0.62f, 0.67f, 0.72f, 1f);

	internal static readonly Color Text = new(0.92f, 0.93f, 0.94f, 1f);

	internal static readonly Color Positive = new(0.44f, 0.82f, 0.56f, 1f);

	internal static readonly Color Warning = new(0.9f, 0.66f, 0.32f, 1f);

	internal static readonly Color Error = new(0.9f, 0.38f, 0.34f, 1f);

	private static GUIStyle? _window;

	private static GUIStyle? _button;

	private static GUIStyle? _closeButton;

	private static GUIStyle? _launcher;

	private static float _launcherAlpha = float.NaN;

	private static GUIStyle? _tabActive;

	private static GUIStyle? _tabInactive;

	private static GUIStyle? _label;

	private static GUIStyle? _mutedLabel;

	private static GUIStyle? _title;

	private static GUIStyle? _section;

	internal static GUIStyle Window() => _window ??= CreateWindow();

	internal static GUIStyle Button() => _button ??= CreateButton();

	internal static GUIStyle CloseButton() => _closeButton ??= CreateCloseButton();

	/// <summary>
	/// The launcher style at the given opacity (0..1). The launcher's idle fade re-derives its text
	/// colours from the theme palette instead of pushing the alpha through the ambient <c>GUI.color</c>
	/// tint, which the launcher's draw path never consumes. The colours are re-derived only when the
	/// alpha CHANGES: the launcher holds one alpha for seconds at a time (the idle window, the
	/// translucent floor, a hover), and every <c>GUIStyleState</c> access allocates a wrapper, so a
	/// steady alpha must not touch the style at all. One control uses this style, so re-deriving it is
	/// deterministic.
	/// </summary>
	internal static GUIStyle Launcher(float alpha)
	{
		var style = _launcher ??= CreateLauncher();
		if (alpha.Equals(_launcherAlpha))
		{
			return style;
		}

		_launcherAlpha = alpha;
		style.normal.textColor = WithAlpha(Accent, alpha);
		style.hover.textColor = WithAlpha(Text, alpha);
		style.active.textColor = WithAlpha(Accent, alpha);
		return style;
	}

	internal static GUIStyle Tab(bool active) => active
		? _tabActive ??= CreateTab(active: true)
		: _tabInactive ??= CreateTab(active: false);

	internal static GUIStyle Label() => _label ??= CreateLabel();

	internal static GUIStyle MutedLabel() => _mutedLabel ??= CreateMutedLabel();

	internal static GUIStyle Title() => _title ??= CreateTitle();

	internal static GUIStyle Section() => _section ??= CreateSection();

	internal static GUIStyle Status(Color color)
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			richText = true,
		};
		style.normal.textColor = color;
		return style;
	}

	/// <summary>
	/// The modal frame's panel and border, at the palette's own alphas, ALPHA-BLENDED. Every frame this
	/// theme draws is blended, and that flag is the whole point: the theme's colours carry an alpha of
	/// their own (<see cref="Panel"/> 0.96, <see cref="Border"/> 0.9, <see cref="OverlayPanel"/> 0.58),
	/// while this overload of <c>GUI.DrawTexture</c> hands the colour AND the flag to the draw verbatim —
	/// the ambient <c>GUI.color</c> tint never reaches it. A frame that does not ask for blending leaves
	/// those alphas with nothing to apply them, which is the opposite of the translucent "operator
	/// console" look this file documents.
	/// </summary>
	internal static void DrawBackground(Rect rect) => DrawFrame(rect, Panel, Border);

	/// <summary>
	/// The same panel and border at a SCALED alpha, blended the same way. The launcher's idle fade needs
	/// both halves of that sentence: the alpha is folded into the colours, and the frame is drawn
	/// blended — the one combination in which a translucent launcher is drawn translucent.
	/// </summary>
	internal static void DrawBackground(Rect rect, float alpha) =>
		DrawFrame(rect, WithAlpha(Panel, alpha), WithAlpha(Border, alpha));

	private static void DrawFrame(Rect rect, Color panel, Color border)
	{
		GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, panel, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border, 0f, 0f);
	}

	private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

	/// <summary>Full-screen-facing overlay background for transient/compact
	/// surfaces such as the Minecraft-like command console: the same blended
	/// frame draw, at the overlay palette's own 0.58 alpha, so the world stays
	/// visible behind the command history.</summary>
	internal static void DrawOverlayBackground(Rect rect) =>
		GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, OverlayPanel, 0f, 0f);

	private static GUIStyle CreateWindow()
	{
		var style = new GUIStyle(GUI.skin.window)
		{
			fontSize = 13,
			padding = new RectOffset(0, 0, 0, 0),
		};
		style.normal.background = null;
		style.normal.textColor = Text;
		// The default GUI window style has hover/active/focused backgrounds
		// that tint the whole window on click; keep the click visual neutral so
		// the Online UI background does not "switch color". Input blocking is
		// handled by OnlineMenuInputGuard, not by a visual active state.
		style.hover.background = null;
		style.hover.textColor = Text;
		style.active.background = null;
		style.active.textColor = Text;
		style.focused.background = null;
		style.focused.textColor = Text;
		return style;
	}

	private static GUIStyle CreateButton()
	{
		var style = new GUIStyle(GUI.skin.button)
		{
			fontSize = 13,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 5, 5),
		};
		style.normal.textColor = Text;
		style.hover.textColor = Accent;
		style.active.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateCloseButton()
	{
		var style = new GUIStyle(GUI.skin.button)
		{
			fontSize = 14,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(2, 2, 2, 2),
		};
		style.normal.textColor = Text;
		style.hover.textColor = Accent;
		style.active.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateLauncher()
	{
		var style = new GUIStyle(GUI.skin.button)
		{
			fontSize = 13,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 5, 5),
		};
		style.normal.background = null;
		style.hover.background = null;
		style.active.background = null;
		style.normal.textColor = Accent;
		style.hover.textColor = Text;
		style.active.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateTab(bool active)
	{
		var style = new GUIStyle(GUI.skin.button)
		{
			fontSize = 13,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 5, 5),
		};
		style.normal.textColor = active ? Accent : Muted;
		style.hover.textColor = Text;
		style.active.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateLabel()
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 13,
			richText = true,
		};
		style.normal.textColor = Text;
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

	private static GUIStyle CreateTitle()
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 16,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
		};
		style.normal.textColor = Accent;
		return style;
	}

	private static GUIStyle CreateSection()
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			fontStyle = FontStyle.Bold,
		};
		style.normal.textColor = Accent;
		return style;
	}
}
