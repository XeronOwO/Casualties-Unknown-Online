using CasualtiesUnknownOnline.Runtime.Session.Commands;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Renders the focused command-console input line: custom caret, selection,
/// syntax-highlighted tokens and IME composition. This is intentionally a
/// separate presenter from <see cref="CommandConsoleOverlay"/> so the overlay
/// only owns panel/history/suggestions layout while the input field keeps its
/// editing presentation concerns in one focused type.
/// </summary>
internal sealed class CommandConsoleInputRenderer
{
	private static GUIStyle? _inputStyle;
	private static GUIStyle? _promptStyle;

	internal void Draw(Rect rect, ConsoleInputSession session, ConsoleImeState ime)
	{
		var evt = Event.current;
		if (evt != null && evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
		{
			session.SetCursor(GetCursorAtMouse(rect, session, evt.mousePosition));
			evt.Use();
		}

		DrawInputBackground(rect);
		var style = InputStyle();
		if (session.HasSelection)
		{
			DrawSelectionBackground(rect, session, style);
		}

		DrawHighlightedInput(rect, session, style);
		DrawImeComposition(rect, session, ime, style);
		UpdateImeCursorPosition(rect, session, style);

		if (ShouldDrawCaret() && session.IsOpen)
		{
			var cursor = session.Cursor;
			var prefix = session.Input.Substring(0, cursor);
			var width = style.CalcSize(new GUIContent(prefix)).x;
			var caretRect = new Rect(
				rect.x + style.padding.left + width,
				rect.y + 4f,
				1f,
				rect.height - 8f);
			var previous = GUI.color;
			GUI.color = OnlineUiTheme.Accent;
			GUI.DrawTexture(caretRect, Texture2D.whiteTexture);
			GUI.color = previous;
		}
	}

	internal GUIStyle PromptStyle()
	{
		if (_promptStyle is null)
		{
			_promptStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleLeft,
			};
			_promptStyle.normal.textColor = OnlineUiTheme.Text;
		}

		return _promptStyle;
	}

	private static void DrawInputBackground(Rect rect)
	{
		var previous = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.25f);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);
		GUI.color = previous;
	}

	private static void DrawImeComposition(Rect rect, ConsoleInputSession session, ConsoleImeState ime, GUIStyle style)
	{
		if (!ime.IsComposing)
		{
			return;
		}

		var caretX = rect.x + style.padding.left + style.CalcSize(new GUIContent(session.Input.Substring(0, session.Cursor))).x;
		var previous = GUI.color;
		GUI.color = OnlineUiTheme.Muted;
		GUI.Label(new Rect(caretX, rect.y, style.CalcSize(new GUIContent(ime.Composition)).x, rect.height), ime.Composition, style);
		GUI.color = previous;
	}

	private static void UpdateImeCursorPosition(Rect rect, ConsoleInputSession session, GUIStyle style)
	{
		if (!session.IsOpen)
		{
			return;
		}

		var caretX = rect.x + style.padding.left + style.CalcSize(new GUIContent(session.Input.Substring(0, session.Cursor))).x;
		// GUI rects use a top-left origin; the legacy Input IME position uses
		// screen coordinates, so convert the caret's vertical center.
		Input.compositionCursorPos = new Vector2(caretX, Screen.height - (rect.y + (rect.height * 0.5f)));
	}

	private static void DrawSelectionBackground(Rect rect, ConsoleInputSession session, GUIStyle style)
	{
		var start = session.SelectionStart;
		var end = session.SelectionEnd;
		var startX = rect.x + style.padding.left + style.CalcSize(new GUIContent(session.Input.Substring(0, start))).x;
		var endX = rect.x + style.padding.left + style.CalcSize(new GUIContent(session.Input.Substring(0, end))).x;
		var previous = GUI.color;
		GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.35f);
		GUI.DrawTexture(new Rect(startX, rect.y + 3f, Mathf.Max(0f, endX - startX), rect.height - 6f), Texture2D.whiteTexture);
		GUI.color = previous;
	}

	private static void DrawHighlightedInput(Rect rect, ConsoleInputSession session, GUIStyle style)
	{
		var tokens = CommandLineTokenizer.Tokenize(session.Input);
		var x = rect.x + style.padding.left;
		var consumed = 0;
		foreach (var token in tokens)
		{
			if (token.Start > consumed)
			{
				DrawSegment(rect, x, style, session.Input.Substring(consumed, token.Start - consumed), OnlineUiTheme.Text, ref x);
			}

			DrawSegment(rect, x, style, token.Text, TokenColor(token), ref x);
			consumed = token.Start + token.Length;
		}

		if (consumed < session.Input.Length)
		{
			DrawSegment(rect, x, style, session.Input.Substring(consumed), OnlineUiTheme.Text, ref x);
		}
	}

	private static void DrawSegment(Rect rect, float x, GUIStyle style, string text, Color color, ref float nextX)
	{
		if (text.Length == 0)
		{
			return;
		}

		var previous = GUI.color;
		GUI.color = color;
		var width = style.CalcSize(new GUIContent(text)).x;
		GUI.Label(new Rect(x, rect.y, width, rect.height), text, style);
		GUI.color = previous;
		nextX = x + width;
	}

	private static Color TokenColor(CommandLineTokenizer.Token token)
	{
		if (token.Start == 0 && token.Text.StartsWith("/", System.StringComparison.Ordinal))
		{
			return OnlineUiTheme.Accent;
		}

		if (token.Quoted
			|| token.Text.StartsWith("{", System.StringComparison.Ordinal)
			|| token.Text.StartsWith("[", System.StringComparison.Ordinal))
		{
			return OnlineUiTheme.Muted;
		}

		return OnlineUiTheme.Text;
	}

	private static int GetCursorAtMouse(Rect rect, ConsoleInputSession session, Vector2 mouse)
	{
		var style = InputStyle();
		var text = session.Input;
		var best = 0;
		var bestDistance = float.MaxValue;
		for (var i = 0; i <= text.Length; i++)
		{
			var width = style.CalcSize(new GUIContent(text.Substring(0, i))).x;
			var x = rect.x + style.padding.left + width;
			var distance = Mathf.Abs(mouse.x - x);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = i;
			}
		}

		return best;
	}

	private static bool ShouldDrawCaret() => (int)(Time.realtimeSinceStartup * 2f) % 2 == 0;

	private static GUIStyle InputStyle()
	{
		if (_inputStyle is null)
		{
			_inputStyle = new GUIStyle(GUI.skin.textField)
			{
				alignment = TextAnchor.MiddleLeft,
				clipping = TextClipping.Clip,
				padding = new RectOffset(6, 6, 4, 4),
			};
			_inputStyle.normal.textColor = Color.white;
		}

		return _inputStyle;
	}
}
