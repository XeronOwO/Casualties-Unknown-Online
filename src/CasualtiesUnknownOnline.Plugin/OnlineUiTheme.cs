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

	internal static readonly Color Border = new(0.72f, 0.58f, 0.24f, 0.9f);

	internal static readonly Color Accent = new(0.85f, 0.72f, 0.38f, 1f);

	internal static readonly Color Muted = new(0.62f, 0.67f, 0.72f, 1f);

	internal static readonly Color Text = new(0.92f, 0.93f, 0.94f, 1f);

	internal static readonly Color Positive = new(0.44f, 0.82f, 0.56f, 1f);

	internal static readonly Color Warning = new(0.9f, 0.66f, 0.32f, 1f);

	internal static readonly Color Error = new(0.9f, 0.38f, 0.34f, 1f);

	private static GUIStyle? _window;

	private static GUIStyle? _button;

	private static GUIStyle? _launcher;

	private static GUIStyle? _tabActive;

	private static GUIStyle? _tabInactive;

	private static GUIStyle? _label;

	private static GUIStyle? _mutedLabel;

	private static GUIStyle? _title;

	private static GUIStyle? _section;

	internal static GUIStyle Window() => _window ??= CreateWindow();

	internal static GUIStyle Button() => _button ??= CreateButton();

	internal static GUIStyle Launcher() => _launcher ??= CreateLauncher();

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

	internal static void DrawBackground(Rect rect)
	{
		GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Panel, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Border, 0f, 0f);
		GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Border, 0f, 0f);
	}

	private static GUIStyle CreateWindow()
	{
		var style = new GUIStyle(GUI.skin.window)
		{
			fontSize = 13,
			padding = new RectOffset(0, 0, 0, 0),
		};
		style.normal.background = null;
		style.normal.textColor = Text;
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
