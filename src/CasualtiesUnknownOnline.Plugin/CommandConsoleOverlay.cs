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
	private const string InputControlName = "CUO.CommandConsole.Input";

	private readonly ConsoleInputSession _session;
	private Vector2 _scroll;
	private Vector2 _suggestionScroll;
	private bool _focusPending;

	internal CommandConsoleOverlay(ConsoleInputSession session)
	{
		_session = session;
	}

	internal bool IsOpen => _session.IsOpen;

	internal void Open()
	{
		_session.Open();
		_focusPending = true;
	}

	internal void Close() => _session.Close();

	internal void Draw(OnlineUiContext ctx)
	{
		if (!_session.IsOpen)
		{
			return;
		}

		HandleKeys();
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
		EnsureFocus();
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
		// newly focused text field does not receive a second '/'.
		if (_focusPending && evt.keyCode == KeyCode.Slash)
		{
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
		var value = _session.Input;
		GUI.SetNextControlName(InputControlName);
		value = GUILayout.TextField(value, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
		_session.SetInput(value);
		GUILayout.EndHorizontal();
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

	private void EnsureFocus()
	{
		if (_focusPending || GUI.GetNameOfFocusedControl() != InputControlName)
		{
			GUI.FocusControl(InputControlName);
			_focusPending = false;
		}
	}
}
