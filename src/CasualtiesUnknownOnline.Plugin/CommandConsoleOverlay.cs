using System;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The standalone in-game command console overlay. It is independent from the
/// modal Online UI window: pressing `/` opens it directly, it owns the focused
/// IMGUI input field, and it renders the Runtime console buffer with an
/// age-based fade. All interaction policy (history, completion, ESC) lives in
/// <see cref="ConsoleInputSession"/>; this class only translates IMGUI events.
/// </summary>
internal sealed class CommandConsoleOverlay
{
	private const float Width = 760f;
	private const float Height = 340f;
	private const float BottomMargin = 24f;
	private const float FadeHoldSeconds = 20f;
	private const float FadeDurationSeconds = 10f;
	private const int MaxVisibleLines = 80;

	private static GUIStyle? _inputStyle;

	private readonly ConsoleInputSession _session;
	private readonly ConsoleImeState _ime = new();
	private Vector2 _scroll;
	private Vector2 _suggestionScroll;
	private Rect _inputRect;
	private int _lastLineCount = -1;
	private bool _focusPending;
	private IMECompositionMode _previousImeMode = IMECompositionMode.Auto;

	internal CommandConsoleOverlay(ConsoleInputSession session)
	{
		_session = session;
	}

	internal bool IsOpen => _session.IsOpen;

	internal void Open()
	{
		_previousImeMode = Input.imeCompositionMode;
		Input.imeCompositionMode = IMECompositionMode.On;
		_session.Open();
		_ime.Clear();
		_focusPending = true;
	}

	internal void Close()
	{
		if (_session.IsOpen)
		{
			Input.imeCompositionMode = _previousImeMode;
			_ime.Clear();
		}

		_session.Close();
	}

	internal void Draw(OnlineUiContext ctx)
	{
		if (!_session.IsOpen)
		{
			return;
		}

		_ime.Update(Input.compositionString);
		HandleKeys();
		_focusPending = false;
		if (!_session.IsOpen)
		{
			return;
		}

		var rect = new Rect((Screen.width - Width) * 0.5f, Screen.height - Height - BottomMargin, Width, Height);
		OnlineUiTheme.DrawBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("console.title"), OnlineUiTheme.Section());
		GUILayout.FlexibleSpace();
		GUILayout.Label(ctx.T("console.overlay.controls"), OnlineUiTheme.MutedLabel());
		GUILayout.EndHorizontal();

		DrawTextArea(ctx);
		DrawHints(ctx);
		DrawInput(ctx);

		GUILayout.EndArea();
		DrawTooltip();
	}

	private void HandleKeys()
	{
		var evt = Event.current;
		if (evt == null || evt.type != EventType.KeyDown)
		{
			return;
		}

		// The slash that opened the console in Plugin.Update is still the
		// current IMGUI event on this first OnGUI frame; swallow it so the
		// custom input does not receive a second '/'.
		if (_focusPending && evt.keyCode == KeyCode.Slash)
		{
			_focusPending = false;
			evt.Use();
			return;
		}

		// While an OS IME composition is active, keystrokes belong to the IME,
		// not to the console editor (typing pinyin must not leak into the line).
		if (_ime.IsComposing)
		{
			evt.Use();
			return;
		}

		if (evt.character != '\0' && !char.IsControl(evt.character))
		{
			_session.InsertChar(evt.character);
			evt.Use();
			return;
		}

		switch (evt.keyCode)
		{
			case KeyCode.Return:
			case KeyCode.KeypadEnter:
				if (_session.Submit())
				{
					evt.Use();
				}

				break;
			case KeyCode.Tab:
				_session.CycleCompletion();
				evt.Use();
				break;
			case KeyCode.Backspace:
				if (evt.control ? _session.BackspaceWord() : _session.Backspace())
				{
					evt.Use();
				}

				break;
			case KeyCode.Delete:
				if (evt.control ? _session.DeleteWord() : _session.Delete())
				{
					evt.Use();
				}

				break;
			case KeyCode.A:
				if (evt.control)
				{
					_session.SelectAll();
					evt.Use();
				}

				break;
			case KeyCode.C:
				if (evt.control)
				{
					if (_session.HasSelection)
					{
						GUIUtility.systemCopyBuffer = _session.SelectedText;
					}

					evt.Use();
				}

				break;
			case KeyCode.X:
				if (evt.control)
				{
					if (_session.HasSelection)
					{
						GUIUtility.systemCopyBuffer = _session.SelectedText;
						_session.DeleteSelection();
					}

					evt.Use();
				}

				break;
			case KeyCode.V:
				if (evt.control)
				{
					_session.InsertText(GUIUtility.systemCopyBuffer);
					evt.Use();
				}

				break;
			case KeyCode.Z:
				if (evt.control)
				{
					if (_session.Undo())
					{
						evt.Use();
					}
				}

				break;
			case KeyCode.Y:
				if (evt.control)
				{
					if (_session.Redo())
					{
						evt.Use();
					}
				}

				break;
			case KeyCode.LeftArrow:
				if (evt.control)
				{
					_session.MoveWordLeft();
				}
				else
				{
					_session.MoveCursorLeft(evt.shift);
				}

				evt.Use();
				break;
			case KeyCode.RightArrow:
				if (evt.control)
				{
					_session.MoveWordRight();
				}
				else
				{
					_session.MoveCursorRight(evt.shift);
				}

				evt.Use();
				break;
			case KeyCode.Home:
				_session.MoveHome(evt.shift);
				evt.Use();
				break;
			case KeyCode.End:
				_session.MoveEnd(evt.shift);
				evt.Use();
				break;
			case KeyCode.UpArrow:
				_session.PreviousHistory();
				evt.Use();
				break;
			case KeyCode.DownArrow:
				_session.NextHistory();
				evt.Use();
				break;
			case KeyCode.Escape:
				Close();
				evt.Use();
				break;
		}
	}

	private void DrawTextArea(OnlineUiContext ctx)
	{
		var lines = ctx.Commands.Lines;
		var start = Math.Max(0, lines.Count - MaxVisibleLines);
		var now = DateTime.UtcNow;
		var hold = TimeSpan.FromSeconds(FadeHoldSeconds);
		var fade = TimeSpan.FromSeconds(FadeDurationSeconds);

		if (lines.Count != _lastLineCount)
		{
			_scroll.y = float.MaxValue;
		}

		_scroll = GUILayout.BeginScrollView(
			_scroll,
			GUILayout.ExpandWidth(true),
			GUILayout.ExpandHeight(true),
			GUILayout.Height(Height * 0.52f));
		if (lines.Count == 0)
		{
			GUILayout.Label(ctx.T("console.overlay.empty"), OnlineUiTheme.MutedLabel());
		}

		for (var i = start; i < lines.Count; i++)
		{
			var line = lines[i];
			var age = now - new DateTime(line.CreatedAtUtcTicks, DateTimeKind.Utc);
			var alpha = ConsoleFadePolicy.ComputeAlpha(age, hold, fade);
			if (alpha <= 0.01f)
			{
				continue;
			}

			DrawLine(line, alpha);
		}

		GUILayout.EndScrollView();
		_lastLineCount = lines.Count;
	}

	private static void DrawLine(ConsoleLine line, float alpha)
	{
		var previous = GUI.color;
		var color = line.Kind switch
		{
			ConsoleLineKind.Success => OnlineUiTheme.Positive,
			ConsoleLineKind.Error => OnlineUiTheme.Error,
			_ => OnlineUiTheme.Text,
		};
		color.a *= alpha;
		GUI.color = color;
		GUILayout.Label(line.Text, OnlineUiTheme.MutedLabel());
		GUI.color = previous;
	}

	private void DrawHints(OnlineUiContext ctx)
	{
		var hint = _session.Hint;
		if (!string.IsNullOrWhiteSpace(hint))
		{
			GUILayout.Label(hint, OnlineUiTheme.Status(OnlineUiTheme.Accent));
		}

		DrawSuggestions();
	}

	private void DrawSuggestions()
	{
		var suggestions = _session.CompletionSuggestions;
		if (suggestions.Count == 0)
		{
			return;
		}

		_suggestionScroll = GUILayout.BeginScrollView(
			_suggestionScroll,
			GUILayout.Height(90f),
			GUILayout.ExpandWidth(true));
		foreach (var suggestion in suggestions)
		{
			GUILayout.BeginHorizontal();
			var content = new GUIContent(suggestion.Text, suggestion.Description);
			if (GUILayout.Button(content, OnlineUiTheme.Button(), GUILayout.ExpandWidth(false)))
			{
				_session.AcceptSuggestion(suggestion);
			}

			if (!string.IsNullOrWhiteSpace(suggestion.Description))
			{
				GUILayout.Label(suggestion.Description, OnlineUiTheme.MutedLabel(), GUILayout.ExpandWidth(true));
			}

			GUILayout.EndHorizontal();
		}

		GUILayout.EndScrollView();
	}

	private void DrawInput(OnlineUiContext ctx)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(">", OnlineUiTheme.MutedLabel());
		_inputRect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.textField, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
		GUILayout.EndHorizontal();
		DrawCustomInput(_inputRect);
	}

	private void DrawCustomInput(Rect rect)
	{
		var evt = Event.current;
		if (evt != null && evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
		{
			_session.SetCursor(GetCursorAtMouse(rect, evt.mousePosition));
			evt.Use();
		}

		OnlineUiTheme.DrawBackground(rect);
		var style = InputStyle();
		if (_session.HasSelection)
		{
			DrawSelectionBackground(rect, style);
		}

		DrawHighlightedInput(rect, style);
		DrawImeComposition(rect, style);
		UpdateImeCursorPosition(rect, style);

		if (ShouldDrawCaret() && _session.IsOpen)
		{
			var cursor = _session.Cursor;
			var prefix = _session.Input.Substring(0, cursor);
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

	private void DrawImeComposition(Rect rect, GUIStyle style)
	{
		if (!_ime.IsComposing)
		{
			return;
		}

		var caretX = rect.x + style.padding.left + style.CalcSize(new GUIContent(_session.Input.Substring(0, _session.Cursor))).x;
		var previous = GUI.color;
		GUI.color = OnlineUiTheme.Muted;
		GUI.Label(new Rect(caretX, rect.y, style.CalcSize(new GUIContent(_ime.Composition)).x, rect.height), _ime.Composition, style);
		GUI.color = previous;
	}

	private void UpdateImeCursorPosition(Rect rect, GUIStyle style)
	{
		if (!_session.IsOpen)
		{
			return;
		}

		var caretX = rect.x + style.padding.left + style.CalcSize(new GUIContent(_session.Input.Substring(0, _session.Cursor))).x;
		// GUI rects use a top-left origin; the legacy Input IME position uses
		// screen coordinates, so convert the caret's vertical center.
		Input.compositionCursorPos = new Vector2(caretX, Screen.height - (rect.y + (rect.height * 0.5f)));
	}

	private void DrawSelectionBackground(Rect rect, GUIStyle style)
	{
		var start = _session.SelectionStart;
		var end = _session.SelectionEnd;
		var startX = rect.x + style.padding.left + style.CalcSize(new GUIContent(_session.Input.Substring(0, start))).x;
		var endX = rect.x + style.padding.left + style.CalcSize(new GUIContent(_session.Input.Substring(0, end))).x;
		var previous = GUI.color;
		GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.35f);
		GUI.DrawTexture(new Rect(startX, rect.y + 3f, Mathf.Max(0f, endX - startX), rect.height - 6f), Texture2D.whiteTexture);
		GUI.color = previous;
	}

	private void DrawHighlightedInput(Rect rect, GUIStyle style)
	{
		var tokens = CommandLineTokenizer.Tokenize(_session.Input);
		var x = rect.x + style.padding.left;
		var consumed = 0;
		foreach (var token in tokens)
		{
			if (token.Start > consumed)
			{
				DrawSegment(rect, x, style, _session.Input.Substring(consumed, token.Start - consumed), OnlineUiTheme.Text, ref x);
			}

			DrawSegment(rect, x, style, token.Text, TokenColor(token), ref x);
			consumed = token.Start + token.Length;
		}

		if (consumed < _session.Input.Length)
		{
			DrawSegment(rect, x, style, _session.Input.Substring(consumed), OnlineUiTheme.Text, ref x);
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

	private int GetCursorAtMouse(Rect rect, Vector2 mouse)
	{
		var style = InputStyle();
		var text = _session.Input;
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

	private void DrawTooltip()
	{
		if (string.IsNullOrEmpty(GUI.tooltip))
		{
			return;
		}

		var mouse = Event.current.mousePosition;
		var width = Mathf.Min(360f, Screen.width - mouse.x - 24f);
		var rect = new Rect(mouse.x + 14f, mouse.y + 14f, width, 44f);
		OnlineUiTheme.DrawBackground(rect);
		GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), GUI.tooltip, OnlineUiTheme.MutedLabel());
	}

}
