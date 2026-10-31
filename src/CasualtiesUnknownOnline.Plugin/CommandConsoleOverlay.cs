using System;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;
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

	/// <summary>Which key opened the surface: the same key-down is still the current IMGUI event on the first frame, and it must be swallowed rather than typed (or, for the Return that opened it, submitted as an empty line).</summary>
	private KeyCode _focusOpener = KeyCode.Slash;
	private IMECompositionMode _previousImeMode = IMECompositionMode.Auto;

	internal CommandConsoleOverlay(ConsoleInputSession session, ILogger<CommandConsoleOverlay> log)
	{
		_session = session;
		_log = log;
	}

	/// <summary>The CUO logging pipeline (level options, rolling file and BepInEx sink) rather than BepInEx's bare
	/// source: one pipeline for the plugin and the runtime, so a reader finds every line in the same file.</summary>
	private readonly ILogger<CommandConsoleOverlay> _log;

	internal bool IsOpen => _session.IsOpen;

	/// <summary>
	/// Open the input. <paramref name="plainChat"/> opens it EMPTY as ordinary chat instead of pre-filling the
	/// command prefix; the opener is remembered so the very key-down that opened it is swallowed on the first
	/// OnGUI frame rather than typed (or, for Return, submitted as an empty line).
	/// </summary>
	internal void Open(bool plainChat = false)
	{
		_previousImeMode = Input.imeCompositionMode;
		Input.imeCompositionMode = IMECompositionMode.On;
		_session.Open(plainChat);
		_ime.Clear();
		_focusPending = true;
		_focusOpener = plainChat ? KeyCode.Return : KeyCode.Slash;
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
		DrawSuggestions(ctx, rect);
		DrawInput(rect);
		DrawTooltip();
	}

	private void DrawClosedNotifications(OnlineUiContext ctx)
	{
		// WHICH lines are notifications (the newest few, still fading, never a
		// report's history-only detail) is Runtime policy — this class owns placement,
		// colours and alpha.
		var now = DateTime.UtcNow;
		var hold = TimeSpan.FromSeconds(NotificationHoldSeconds);
		var fade = TimeSpan.FromSeconds(NotificationFadeSeconds);
		var visible = ConsoleNotificationPolicy.Recent(ctx.Commands.Lines, now, hold, fade, MaxNotificationLines);
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
			DrawLine(line, ConsoleLineText.Resolve(ctx.T, line), alpha);
		}

		GUILayout.EndArea();
	}

	private void HandleKeys()
	{
		// The IMGUI event is only valid inside OnGUI; the seam below takes it as a parameter so the SAME handler can be driven from a batch (a synthetic
		// `Event.KeyboardEvent`) and the keys that move the list are judgeable from a frame instead of by hand.
		if (Event.current is { } current)
		{
			HandleKey(current);
		}
	}

	/// <summary>
	/// One key event through the overlay's own switch. Returns true when the overlay consumed it. The parameter is the IMGUI event a player's key press
	/// produces; a batch hands in a synthetic one, which is the same code path minus the keyboard.
	/// </summary>
	internal bool HandleKey(Event evt)
	{
		if (evt == null || evt.type != EventType.KeyDown)
		{
			return false;
		}

		// The key that opened the console is still the current IMGUI event on this first OnGUI frame; swallow it so
		// the custom input does not receive a second '/' — and so the Return that opened plain chat does not also
		// SUBMIT the empty line it just created. KeypadEnter counts as the same opener.
		if (_focusPending
			&& (evt.keyCode == _focusOpener
				|| (_focusOpener == KeyCode.Return && evt.keyCode == KeyCode.KeypadEnter)))
		{
			_focusPending = false;
			_log.LogInformation($"Command console opened by {_focusOpener}; swallowing that key so it is not typed or submitted.");
			evt.Use();
			return true;
		}

		// While an OS IME composition is active, keystrokes belong to the IME,
		// not to the console editor (typing pinyin must not leak into the line).
		if (_ime.IsComposing)
		{
			evt.Use();
			return true;
		}

		if (evt.character != '\0' && !char.IsControl(evt.character))
		{
			_session.InsertChar(evt.character);
			evt.Use();
			return true;
		}

		switch (evt.keyCode)
		{
			case KeyCode.Return:
			case KeyCode.KeypadEnter:
				// Enter with text executes; Enter on an empty line closes the console (Minecraft's own rule — see SubmitOrClose).
				if (_session.SubmitOrClose())
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
			// The arrows belong to the LIST while one is drawn (user direction 2026-10-30): they move the highlight and preview the row, and they reach the
			// command history only when no list is on screen. Minecraft's console does exactly this, and a history jump under a visible list is how the player
			// loses the word they were looking at.
			case KeyCode.UpArrow:
				if (_session.DisplaySuggestions.Count > 0)
				{
					_session.MoveSelection(-1);
				}
				else
				{
					_session.PreviousHistory();
				}

				evt.Use();
				break;
			case KeyCode.DownArrow:
				if (_session.DisplaySuggestions.Count > 0)
				{
					_session.MoveSelection(1);
				}
				else
				{
					_session.NextHistory();
				}

				evt.Use();
				break;
			case KeyCode.Escape:
				_log.LogInformation("Command console ESC consumed; closing overlay.");
				Close();
				evt.Use();
				break;
		}

		return true;
	}

	private void DrawHistory(OnlineUiContext ctx, Rect panel)
	{
		var suggestions = _session.DisplaySuggestions;
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
			DrawLine(line, ConsoleLineText.Resolve(ctx.T, line));
		}

		GUILayout.EndScrollView();
		GUILayout.EndArea();
		_lastLineCount = ctx.Commands.Lines.Count;
	}

	private static void DrawLine(ConsoleLine line, string text) => DrawLine(line, text, 1f);

	private static void DrawLine(ConsoleLine line, string text, float alpha)
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
		GUILayout.Label(text, OnlineUiTheme.MutedLabel());
		GUI.color = previous;
	}

	/// <summary>
	/// The hint row of one completion, in the language the player's catalogue declares. A command's hint is looked up HERE, at the accessor, so the
	/// localisation census reads the key space (`console.cmd.`) instead of an opaque string the Runtime half happened to build; any other hint carries
	/// its own key. A caller-built description is the fallback only for a hint the catalogue cannot hold.
	/// </summary>
	private static string? HintText(OnlineUiContext ctx, CommandSuggestion suggestion) => suggestion.Hint switch
	{
		CommandSuggestionHint.None => null,
		CommandSuggestionHint.Command => CommandHint(ctx, suggestion.Args[0] as string ?? string.Empty),
		CommandSuggestionHint.MemberName => ctx.T("console.suggest.member_name"),
		CommandSuggestionHint.SteamId => ctx.T("console.suggest.steamid"),
		CommandSuggestionHint.SteamIdHex => ctx.T("console.suggest.steamid_hex"),
		CommandSuggestionHint.BannedSteamId => ctx.T("console.suggest.banned_steamid"),
		CommandSuggestionHint.BannedSteamIdHex => ctx.T("console.suggest.banned_steamid_hex"),
		CommandSuggestionHint.FilterBy => ctx.F("console.suggest.filter_by", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.FilterValue => ctx.F("console.suggest.value", suggestion.Args[0] ?? string.Empty, suggestion.Args[1] ?? string.Empty),
		CommandSuggestionHint.JsonEmpty => ctx.T("console.suggest.json_empty"),
		CommandSuggestionHint.JsonTemplate => ctx.T("console.suggest.json_template"),
		CommandSuggestionHint.SummonPosition => ctx.T("console.suggest.summon_position"),
		CommandSuggestionHint.BodySlot => ctx.T("console.suggest.body_slot"),
		CommandSuggestionHint.GamemodeValue => ctx.T("console.suggest.gamemode_value"),
		CommandSuggestionHint.HostRuleCurrent => ctx.F("console.suggest.rule_current", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.HostRuleValue => ctx.F("console.suggest.rule_value", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.KeyedText => KeyedHint(ctx, suggestion),
		_ => null,
	};

	/// <summary>
	/// A mod's own sentence: <c>Args[0]</c> is its catalogue key and the rest are the values that sentence places. The row is rendered by the client that draws
	/// it, in that client's language, exactly like a framework key.
	/// </summary>
	private static string? KeyedHint(OnlineUiContext ctx, CommandSuggestion suggestion)
	{
		if (suggestion.Args.Length == 0 || suggestion.Args[0] is not string key || key.Length == 0)
		{
			return null;
		}

		var arguments = new object?[suggestion.Args.Length - 1];
		for (var index = 1; index < suggestion.Args.Length; index++)
		{
			arguments[index - 1] = suggestion.Args[index];
		}

		return ctx.F(key, arguments);
	}

	/// <summary>
	/// One command's one-line description in the reader's language, or null when the catalogue has no entry (a hint never shows a key). Every key is a
	/// LITERAL handed straight to <c>ctx.T</c>, which is the shape the localisation census reads, so it can prove each catalogue entry has a reader; a
	/// command the table does not name answers null and the hint row shows nothing rather than English.
	/// </summary>
	private static string? CommandHint(OnlineUiContext ctx, string command) => command switch
	{
		"traderreport" => ctx.T("console.cmd.traderreport.desc"),
		"op" => ctx.T("console.cmd.op.desc"),
		"deop" => ctx.T("console.cmd.deop.desc"),
		"help" => ctx.T("console.cmd.help.desc"),
		"clear" => ctx.T("console.cmd.clear.desc"),
		"players" => ctx.T("console.cmd.players.desc"),
		"rtt" => ctx.T("console.cmd.rtt.desc"),
		"whoami" => ctx.T("console.cmd.whoami.desc"),
		"gamemode" => ctx.T("console.cmd.gamemode.desc"),
		"heal" => ctx.T("console.cmd.heal.desc"),
		"kick" => ctx.T("console.cmd.kick.desc"),
		"ban" => ctx.T("console.cmd.ban.desc"),
		"unban" => ctx.T("console.cmd.unban.desc"),
		"gamerule" => ctx.T("console.cmd.gamerule.desc"),
		"data" => ctx.T("console.cmd.data.desc"),
		"give" => ctx.T("console.cmd.give.desc"),
		"spectate" => ctx.T("console.cmd.spectate.desc"),
		"save" => ctx.T("console.cmd.save.desc"),
		"summon" => ctx.T("console.cmd.summon.desc"),
		_ => null,
	};

	private void DrawSuggestions(OnlineUiContext ctx, Rect panel)
	{
		var suggestions = _session.DisplaySuggestions;
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
		var selected = _session.SelectedSuggestionIndex;
		for (var index = 0; index < suggestions.Count; index++)
		{
			var suggestion = suggestions[index];
			GUILayout.BeginHorizontal();
			var hint = HintText(ctx, suggestion);
			// The highlighted row is the one ↑/↓ and Enter act on, so it must be visually the current one: the marker is part of the list, not a decoration.
			var marker = index == selected ? "▶ " : "   ";
			var content = new GUIContent(marker + suggestion.Text, hint);
			var style = OnlineUiTheme.Button();
			if (GUILayout.Button(content, style, GUILayout.ExpandWidth(false)))
			{
				_session.AcceptSuggestion(suggestion);
			}

			if (!string.IsNullOrWhiteSpace(hint))
			{
				GUILayout.Label(hint, OnlineUiTheme.MutedLabel(), GUILayout.ExpandWidth(true));
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
