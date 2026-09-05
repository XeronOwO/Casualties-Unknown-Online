using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The standalone in-game command console overlay. It is independent from the
/// modal Online UI window: pressing `/` opens it directly and it renders a
/// Minecraft-like translucent history panel plus a focused input line. All
/// interaction policy (history, completion, ESC) lives in
/// <see cref="ConsoleInputSession"/>; this class only translates IMGUI events.
/// </summary>
internal sealed class CommandConsoleOverlay
{
	private const float Width = 680f;
	private const float Height = 260f;
	private const float BottomMargin = 14f;
	private const float InputHeight = 30f;
	private const float SuggestionMaxHeight = 150f;
	private const int MaxNotificationLines = 5;
	private const float NotificationHoldSeconds = 8f;
	private const float NotificationFadeSeconds = 5f;

	private readonly ConsoleInputSession _session;
	private readonly ConsoleImeState _ime = new();
	private readonly CommandConsoleInputRenderer _inputRenderer = new();
	private Vector2 _scroll;
	private Vector2 _suggestionScroll;
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
			DrawClosedNotifications(ctx);
			return;
		}

		_ime.Update(Input.compositionString);
		HandleKeys();
		_focusPending = false;
		if (!_session.IsOpen)
		{
			return;
		}

		var width = Mathf.Min(Width, Screen.width - 24f);
		var height = Mathf.Min(Height, Screen.height * 0.4f);
		var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - BottomMargin, width, height);
		OnlineUiTheme.DrawOverlayBackground(rect);

		DrawHistory(ctx, rect);
		DrawSuggestions(rect);
		DrawInput(rect);
		DrawTooltip();
	}

	private void DrawClosedNotifications(OnlineUiContext ctx)
	{
		var lines = ctx.Commands.Lines;
		if (lines.Count == 0)
		{
			return;
		}

		var now = DateTime.UtcNow;
		var hold = TimeSpan.FromSeconds(NotificationHoldSeconds);
		var fade = TimeSpan.FromSeconds(NotificationFadeSeconds);
		var visible = new List<ConsoleLine>(MaxNotificationLines);
		for (var i = lines.Count - 1; i >= 0 && visible.Count < MaxNotificationLines; i--)
		{
			var line = lines[i];
			var alpha = ConsoleFadePolicy.ComputeAlpha(
				now - new DateTime(line.CreatedAtUtcTicks, DateTimeKind.Utc),
				hold,
				fade);
			if (alpha > 0.01f)
			{
				visible.Add(line);
			}
		}

		if (visible.Count == 0)
		{
			return;
		}

		var width = Mathf.Min(Width, Screen.width - 24f);
		var height = 24f + (visible.Count * 22f);
		var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - BottomMargin, width, height);
		OnlineUiTheme.DrawOverlayBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 4f));
		for (var i = visible.Count - 1; i >= 0; i--)
		{
			var line = visible[i];
			var alpha = ConsoleFadePolicy.ComputeAlpha(
				now - new DateTime(line.CreatedAtUtcTicks, DateTimeKind.Utc),
				hold,
				fade);
			DrawLine(line, alpha);
		}

		GUILayout.EndArea();
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

	private void DrawHistory(OnlineUiContext ctx, Rect panel)
	{
		var suggestions = _session.LiveSuggestions;
		var suggestionHeight = suggestions.Count == 0
			? 0f
			: Mathf.Min(SuggestionMaxHeight, 14f + (suggestions.Count * 22f));
		var inputRect = InputRect(panel);
		var historyHeight = inputRect.y - panel.y - 8f - (suggestionHeight > 0f ? suggestionHeight + 6f : 0f);
		if (historyHeight < 40f)
		{
			historyHeight = 40f;
		}

		var historyRect = new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, historyHeight);
		GUILayout.BeginArea(historyRect);
		if (ctx.Commands.Lines.Count == 0)
		{
			GUILayout.Label(ctx.T("console.overlay.empty"), OnlineUiTheme.MutedLabel());
		}

		if (ctx.Commands.Lines.Count != _lastLineCount)
		{
			_scroll.y = float.MaxValue;
		}

		_scroll = GUILayout.BeginScrollView(
			_scroll,
			GUILayout.ExpandWidth(true),
			GUILayout.ExpandHeight(true));
		foreach (var line in ctx.Commands.Lines)
		{
			DrawLine(line);
		}

		GUILayout.EndScrollView();
		GUILayout.EndArea();
		_lastLineCount = ctx.Commands.Lines.Count;
	}

	private static void DrawLine(ConsoleLine line) => DrawLine(line, 1f);

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

	private void DrawSuggestions(Rect panel)
	{
		var suggestions = _session.LiveSuggestions;
		if (suggestions.Count == 0)
		{
			return;
		}

		var inputRect = InputRect(panel);
		var height = Mathf.Min(SuggestionMaxHeight, 12f + (suggestions.Count * 22f));
		if (height < 22f)
		{
			height = 22f;
		}

		var rect = new Rect(panel.x + 8f, inputRect.y - height - 4f, panel.width - 16f, height);
		OnlineUiTheme.DrawOverlayBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f));
		_suggestionScroll = GUILayout.BeginScrollView(
			_suggestionScroll,
			GUILayout.ExpandWidth(true),
			GUILayout.ExpandHeight(true));
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
		GUILayout.EndArea();
	}

	private void DrawInput(Rect panel)
	{
		var rect = InputRect(panel);
		GUI.Label(new Rect(rect.x, rect.y, 18f, rect.height), ">", _inputRenderer.PromptStyle());
		_inputRenderer.Draw(new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height), _session, _ime);
	}

	private static Rect InputRect(Rect panel) =>
		new(panel.x + 10f, panel.y + panel.height - InputHeight - 8f, panel.width - 20f, InputHeight);

	private void DrawTooltip()
	{
		if (string.IsNullOrEmpty(GUI.tooltip))
		{
			return;
		}

		var mouse = Event.current.mousePosition;
		var width = Mathf.Min(360f, Screen.width - mouse.x - 24f);
		var rect = new Rect(mouse.x + 14f, mouse.y + 14f, width, 44f);
		OnlineUiTheme.DrawOverlayBackground(rect);
		GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), GUI.tooltip, OnlineUiTheme.MutedLabel());
	}
}
