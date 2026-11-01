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

	/// <summary>The marker column's width: fixed, so a selected row and an unselected one put their buttons at the same x.</summary>
	private const float MarkerWidth = 14f;

	/// <summary>The prompt's own width: the input control starts here, and so does the suggestion list (one number, because the two must line up).</summary>
	private const float PromptWidth = 18f;
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

	/// <summary>The tooltip the last pointer event reported, kept for the repaint that draws it (see <see cref="CaptureTooltip"/>).</summary>
	private string _tooltipText = string.Empty;

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
		CaptureTooltip();
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

		// Same style rule as the suggestion list below: an un-styled scroll view inherits the GAME's skin padding and lays the
		// history out inside it, which is a different left edge from the panel it is drawn in.
		_scroll = GUILayout.BeginScrollView(
			_scroll,
			false,
			true,
			GUI.skin.horizontalScrollbar,
			GUI.skin.verticalScrollbar,
			OnlineUiTheme.ScrollBackground(),
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
	// The usage line above the input is gone (user direction 2026-11-02): it sat exactly where the suggestion list opens, so the
	// list covered it, and it repeated one line of what the rows beside it already say. Each command row carries its own usage now.

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

		// The row's BOX starts exactly at the input's text edge, so the list reads as a continuation of the line being typed (user
		// direction 2026-11-02: aligned with the caret, not with the prompt and not with the window). The inner padding is the
		// button style's own, kept small so the text does not drift far inside the box.
		var inputTextX = inputRect.x + PromptWidth;
		var rect = new Rect(
			inputTextX,
			inputRect.y - height - 4f,
			panel.width - (inputTextX - panel.x) - 8f,
			height);
		LastSuggestionRect = rect;
		LastSuggestionInkRect = new Rect(rect.x + 2f + OnlineUiTheme.Button().padding.left, rect.y, rect.width - 4f, rect.height);
		OnlineUiTheme.DrawOverlayBackground(rect);
		GUILayout.BeginArea(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f));
		// The scroll view's own style decides where its content starts: the game's skin indents it, so the zero-padding style
		// (OnlineUiTheme.ScrollBackground) is what puts every row against the list's left edge, level with the prompt below it.
		_suggestionScroll = GUILayout.BeginScrollView(
			_suggestionScroll,
			false,
			true,
			GUI.skin.horizontalScrollbar,
			GUI.skin.verticalScrollbar,
			OnlineUiTheme.ScrollBackground(),
			GUILayout.ExpandWidth(true),
			GUILayout.ExpandHeight(true));
		var selected = _session.SelectedSuggestionIndex;
		for (var index = 0; index < suggestions.Count; index++)
		{
			var suggestion = suggestions[index];
			GUILayout.BeginHorizontal();
			var hint = CommandSuggestionHintText.Of(ctx, suggestion);
			// A command row carries its OWN usage (user direction 2026-11-02: the separate usage line sat where this list opens and was
			// covered by it). The usage is syntax, so it reads the same in every language and needs no catalogue key; when the catalogue has
			// no description for the command at all (a mod's own verb), the usage alone is the hint - an empty right side teaches nothing.
			if (!string.IsNullOrWhiteSpace(suggestion.Usage))
			{
				hint = string.IsNullOrWhiteSpace(hint) ? suggestion.Usage : hint + " · " + suggestion.Usage;
			}

			// The highlighted row is the one the arrows and Enter act on, so it must be visibly the current one. The marker is
			// its OWN column: padding the button's text with spaces pushed the label off-centre inside its box, which read as a
			// large left inset (measured 2026-11-02 — the row's text sat 6px in on one side and 0 on the other). ExpandHeight keeps
			// the glyph centred on the row's own height instead of sitting on its top edge.
			var marker = index == selected ? "▶" : "";
			GUILayout.Label(marker, OnlineUiTheme.Marker(), GUILayout.Width(MarkerWidth), GUILayout.ExpandHeight(true));
			if (GUILayout.Button(suggestion.Text, OnlineUiTheme.Button(), GUILayout.ExpandWidth(false)))
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
		GUI.Label(new Rect(rect.x, rect.y, PromptWidth, rect.height), ">", _inputRenderer.PromptStyle());
		LastPromptRect = new Rect(rect.x, rect.y, PromptWidth, rect.height);
		LastInputTextRect = new Rect(rect.x + PromptWidth, rect.y, rect.width - PromptWidth, rect.height);
		_inputRenderer.Draw(LastInputTextRect, _session, _ime);
	}

	/// <summary>
	/// The rects the overlay drew this frame. A reading compares these NUMBERS instead of eyeballing a capture (user direction
	/// 2026-11-02: align the rows with the caret, and prove it by measurement).
	/// </summary>
	internal static Rect LastPromptRect { get; private set; }

	internal static Rect LastInputTextRect { get; private set; }

	internal static Rect LastSuggestionRect { get; private set; }

	internal static Rect LastSuggestionInkRect { get; private set; }

	private static Rect InputRect(Rect panel) =>
		new(panel.x + 10f, panel.y + panel.height - InputHeight - 8f, panel.width - 20f, InputHeight);

	/// <summary>
	/// Draws the preview of the candidate the pointer is over — the one thing a CLICK cannot show, because the row is already printing its
	/// own hint and the preview is what a hover adds without accepting the row.
	/// </summary>
	/// <remarks>
	/// The text is the LAST captured value, never <c>GUI.tooltip</c> read here: IMGUI reports the hovered control's tooltip while it PROCESSES
	/// the pointer event and clears it before the repaint that could draw it, so reading it in the drawing pass found it empty on every frame —
	/// which is why the preview had never appeared.
	/// </remarks>
	private void DrawTooltip()
	{
		if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(_tooltipText))
		{
			return;
		}

		var mouse = Event.current.mousePosition;
		var width = Mathf.Max(120f, Mathf.Min(360f, Screen.width - mouse.x - 24f));
		var rect = new Rect(mouse.x + 14f, mouse.y + 14f, width, 44f);
		OnlineUiTheme.DrawOverlayBackground(rect);
		GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), _tooltipText, OnlineUiTheme.MutedLabel());
	}

	/// <summary>
	/// Keeps the hovered control's tooltip across the two events IMGUI splits it over: the pointer event that reports it and the repaint that
	/// draws it. An input event that reports none clears the stored text, so a preview cannot outlive the pointer leaving the row.
	/// </summary>
	private void CaptureTooltip()
	{
		if (!string.IsNullOrEmpty(GUI.tooltip))
		{
			_tooltipText = GUI.tooltip;
			return;
		}

		if (Event.current.type is EventType.MouseMove or EventType.MouseDrag or EventType.MouseDown or EventType.MouseUp)
		{
			_tooltipText = string.Empty;
		}
	}
}
