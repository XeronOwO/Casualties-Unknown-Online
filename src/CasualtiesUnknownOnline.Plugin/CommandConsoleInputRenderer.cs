using System;
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
	private static GUIStyle? _segmentStyle;
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
		var draw = SegmentStyle();

		// The row must CLIP: a line longer than the field is scrolled so the caret stays at its right edge, instead of running out of the panel and
		// over the world behind it (measured 2026-10-28: a 120-character line drew past the panel's edge with the caret following it).
		var clipLeft = rect.x + style.padding.left;
		var clipWidth = Mathf.Max(0f, rect.width - style.padding.left - style.padding.right);
		var caretX = clipLeft + draw.CalcSize(new GUIContent(session.Input.Substring(0, session.Cursor))).x;
		var scroll = Mathf.Max(0f, caretX - (clipLeft + clipWidth));
		var clip = new Rect(clipLeft, rect.y, clipWidth, rect.height);

		if (session.HasSelection)
		{
			DrawSelectionBackground(rect, session, style, scroll);
		}

		// Inside a clip the children are laid out in the CLIP's own space (origin 0,0), so the row is drawn at y=0 and x starts at -scroll: the
		// absolute x the rest of this file uses would be shifted by the clip's own offset.
		GUI.BeginClip(clip);
		DrawHighlightedInput(rect.height, session, style, scroll);
		DrawImeComposition(rect.height, session, ime, style, scroll);
		GUI.EndClip();

		UpdateImeCursorPosition(rect, session, style);

		if (ShouldDrawCaret() && session.IsOpen)
		{
			// Measured with the SAME style the glyphs are DRAWN with (zero padding, no background). The input style carries padding, and
			// `CalcSize` lays the content out the way the style would, padding included — so measuring the prefix with it put the caret one
			// padding PAST the text it follows (a constant offset the player reads as "the cursor is misaligned", reported twice).
			var caretRect = new Rect(
				caretX - scroll,
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

	private static void DrawImeComposition(float height, ConsoleInputSession session, ConsoleImeState ime, GUIStyle style, float scroll)
	{
		if (!ime.IsComposing)
		{
			return;
		}

		var draw = SegmentStyle();
		var caretX = draw.CalcSize(new GUIContent(session.Input.Substring(0, session.Cursor))).x - scroll;
		var previous = GUI.color;
		GUI.color = OnlineUiTheme.Muted;
		GUI.Label(new Rect(caretX, 0f, draw.CalcSize(new GUIContent(ime.Composition)).x, height), ime.Composition, draw);
		GUI.color = previous;
	}

	private static void UpdateImeCursorPosition(Rect rect, ConsoleInputSession session, GUIStyle style)
	{
		if (!session.IsOpen)
		{
			return;
		}

		var caretX = rect.x + style.padding.left + SegmentStyle().CalcSize(new GUIContent(session.Input.Substring(0, session.Cursor))).x;
		// GUI rects use a top-left origin; the legacy Input IME position uses
		// screen coordinates, so convert the caret's vertical center.
		Input.compositionCursorPos = new Vector2(caretX, Screen.height - (rect.y + (rect.height * 0.5f)));
	}

	private static void DrawSelectionBackground(Rect rect, ConsoleInputSession session, GUIStyle style, float scroll)
	{
		var draw = SegmentStyle();
		var start = session.SelectionStart;
		var end = session.SelectionEnd;
		var startX = rect.x + style.padding.left + draw.CalcSize(new GUIContent(session.Input.Substring(0, start))).x;
		var endX = rect.x + style.padding.left + draw.CalcSize(new GUIContent(session.Input.Substring(0, end))).x;
		var previous = GUI.color;
		GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.35f);
		GUI.DrawTexture(new Rect(startX, rect.y + 3f, Mathf.Max(0f, endX - startX), rect.height - 6f), Texture2D.whiteTexture);
		GUI.color = previous;
	}

	/// <summary>
	/// The style the SEGMENTS are drawn and measured with: the input style with its padding zeroed and its BACKGROUND removed.
	///
	/// <para>
	/// Two defects came from drawing text with a text-field style. Its padding insets the glyphs inside the rect it is given, so drawing every
	/// token into a rect that already started at `rect.x + padding.left` put each token one padding further right than the x the loop computed —
	/// the drift grew with every token and the caret, which measured with the padded style, fell further behind the longer the line got. And its
	/// background is the field's own rounded frame, so every TOKEN got a rounded rectangle of its own — which is exactly what the player
	/// reported ("you frame the characters with a rounded rectangle"). One zero-padding, transparent-background style for both measuring and
	/// drawing leaves the single input frame the row already draws, with the text and the caret sharing one origin.
	/// </para>
	/// </summary>
	private static GUIStyle SegmentStyle()
	{
		if (_segmentStyle is null)
		{
			_segmentStyle = new GUIStyle(InputStyle())
			{
				padding = new RectOffset(0, 0, 0, 0),
			};
			_segmentStyle.normal.background = null;
			_segmentStyle.hover.background = null;
			_segmentStyle.active.background = null;
			_segmentStyle.focused.background = null;
		}

		return _segmentStyle;
	}

	private static void DrawHighlightedInput(float height, ConsoleInputSession session, GUIStyle style, float scroll)
	{
		var draw = SegmentStyle();
		var tokens = CommandLineTokenizer.Tokenize(session.Input);
		var x = -scroll;
		var consumed = 0;
		foreach (var token in tokens)
		{
			if (token.Start > consumed)
			{
				DrawSegment(height, x, draw, session.Input.Substring(consumed, token.Start - consumed), OnlineUiTheme.Text, ref x);
			}

			DrawSegment(height, x, draw, token.Text, TokenColor(token), ref x);
			consumed = token.Start + token.Length;
		}

		if (consumed < session.Input.Length)
		{
			DrawSegment(height, x, draw, session.Input.Substring(consumed), OnlineUiTheme.Text, ref x);
		}
	}

	private static void DrawSegment(float height, float x, GUIStyle style, string text, Color color, ref float nextX)
	{
		if (text.Length == 0)
		{
			return;
		}

		var previous = GUI.color;
		GUI.color = color;
		var width = style.CalcSize(new GUIContent(text)).x;
		GUI.Label(new Rect(x, 0f, width, height), text, style);
		GUI.color = previous;
		nextX = x + width;
	}

	private static Color TokenColor(CommandLineTokenizer.Token token)
	{
		if (token.Start == 0 && token.Text.StartsWith("/", StringComparison.Ordinal))
		{
			return OnlineUiTheme.Accent;
		}

		if (token.Quoted
			|| token.Text.StartsWith("{", StringComparison.Ordinal)
			|| token.Text.StartsWith("[", StringComparison.Ordinal))
		{
			return OnlineUiTheme.Muted;
		}

		return OnlineUiTheme.Text;
	}

	private static int GetCursorAtMouse(Rect rect, ConsoleInputSession session, Vector2 mouse)
	{
		// The same style the glyphs are drawn with: a click must land where the character under the pointer IS, not one padding to its right.
		var style = InputStyle();
		var draw = SegmentStyle();
		var text = session.Input;
		var best = 0;
		var bestDistance = float.MaxValue;
		for (var i = 0; i <= text.Length; i++)
		{
			var width = draw.CalcSize(new GUIContent(text.Substring(0, i))).x;
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
