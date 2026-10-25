using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The Unity-independent input state machine for the in-game command console.
/// It owns open/close, history navigation, completion cycling, submission and
/// undo/redo; text/cursor/selection editing is delegated to
/// <see cref="ConsoleInputEditor"/>.
/// </summary>
public sealed class ConsoleInputSession(ICommandControl control, ICommandCompletionSource completion)
{
	private const string OpenPrefix = "/";

	private readonly ICommandControl _control = control;
	private readonly ICommandCompletionSource _completion = completion;
	private readonly ConsoleInputEditor _editor = new();
	private readonly ConsoleEditHistory _editHistory = new();
	private readonly List<string> _history = [];

	private bool _open;
	private string _draft = "";
	private int _historyIndex = -1;
	private IReadOnlyList<CommandSuggestion> _completionCandidates = [];
	private int _completionIndex = -1;

	public bool IsOpen => _open;

	public string Input => _editor.Input;

	public int Cursor => _editor.Cursor;

	public int SelectionStart => _editor.SelectionStart;

	public int SelectionEnd => _editor.SelectionEnd;

	public bool HasSelection => _editor.HasSelection;

	public string SelectedText => _editor.SelectedText;

	public IReadOnlyList<CommandSuggestion> CompletionSuggestions => _completionCandidates;

	/// <summary>The Minecraft-style live suggestion view: recomputed from the
	/// current input every access, independent of the Tab-cycling candidate
	/// list. The overlay shows this list while typing so pressing `/` or a
	/// command prefix immediately presents candidates without requiring Tab.</summary>
	public IReadOnlyList<CommandSuggestion> LiveSuggestions => _open
		? _completion.Suggest(_editor.Input)
		: [];

	public IReadOnlyList<string> CompletionCandidates => [.. _completionCandidates.Select(c => c.Text)];

	public string? Hint => _open ? _completion.GetHint(_editor.Input) : null;

	public IReadOnlyList<string> History => _history;

	public bool CanUndo => _editHistory.CanUndo;

	public bool CanRedo => _editHistory.CanRedo;

	/// <summary>
	/// Open the input. <paramref name="plainChat"/> opens it EMPTY for ordinary chat instead of pre-filling the
	/// command prefix, which is the only difference between the two openers: a line that begins with the prefix
	/// is still a command, so the mode decides what is typed FOR the player, never what the surface can do.
	/// </summary>
	public void Open(bool plainChat = false)
	{
		if (_open)
		{
			return;
		}

		_open = true;
		_editor.SetInput(plainChat ? string.Empty : OpenPrefix);
		_historyIndex = -1;
		_draft = "";
		ClearUndoRedo();
		ResetCompletions();
	}

	public void Close()
	{
		if (!_open)
		{
			return;
		}

		_open = false;
		_editor.SetInput("");
		_historyIndex = -1;
		_draft = "";
		ClearUndoRedo();
		ResetCompletions();
	}

	public void SetInput(string value, int? cursor = null)
	{
		_editor.SetInput(value, cursor);
		ResetCompletions();
	}

	public void SetCursor(int position)
	{
		if (_open)
		{
			_editor.SetCursor(position);
		}
	}

	public void SelectAll()
	{
		if (_open)
		{
			_editor.SelectAll();
		}
	}

	public void SetSelection(int start, int end)
	{
		if (_open)
		{
			_editor.SetSelection(start, end);
		}
	}

	public bool DeleteSelection()
	{
		if (!_open)
		{
			return false;
		}

		var deleted = _editor.DeleteSelection();
		if (deleted)
		{
			ResetCompletions();
		}

		return deleted;
	}

	public void InsertChar(char c)
	{
		if (!_open || char.IsControl(c))
		{
			return;
		}

		CaptureUndo();
		_editor.InsertChar(c);
		ResetCompletions();
	}

	public void InsertText(string text)
	{
		if (!_open || string.IsNullOrEmpty(text))
		{
			return;
		}

		CaptureUndo();
		_editor.InsertText(text);
		ResetCompletions();
	}

	public bool Backspace()
	{
		if (!_open)
		{
			return false;
		}

		CaptureUndo();
		var changed = _editor.Backspace();
		if (changed)
		{
			ResetCompletions();
		}

		return changed;
	}

	public bool Delete()
	{
		if (!_open)
		{
			return false;
		}

		CaptureUndo();
		var changed = _editor.Delete();
		if (changed)
		{
			ResetCompletions();
		}

		return changed;
	}

	public void MoveCursorLeft(bool extendSelection = false)
	{
		if (_open)
		{
			_editor.MoveCursorLeft(extendSelection);
		}
	}

	public void MoveCursorRight(bool extendSelection = false)
	{
		if (_open)
		{
			_editor.MoveCursorRight(extendSelection);
		}
	}

	public void MoveWordLeft()
	{
		if (_open)
		{
			_editor.MoveWordLeft();
		}
	}

	public void MoveWordRight()
	{
		if (_open)
		{
			_editor.MoveWordRight();
		}
	}

	public bool BackspaceWord()
	{
		if (!_open)
		{
			return false;
		}

		CaptureUndo();
		var changed = _editor.BackspaceWord();
		if (changed)
		{
			ResetCompletions();
		}

		return changed;
	}

	public bool DeleteWord()
	{
		if (!_open)
		{
			return false;
		}

		CaptureUndo();
		var changed = _editor.DeleteWord();
		if (changed)
		{
			ResetCompletions();
		}

		return changed;
	}

	public void MoveHome(bool extendSelection = false)
	{
		if (_open)
		{
			_editor.MoveHome(extendSelection);
		}
	}

	public void MoveEnd(bool extendSelection = false)
	{
		if (_open)
		{
			_editor.MoveEnd(extendSelection);
		}
	}

	/// <summary>
	/// Executes the current non-empty line and records it in history. The
	/// console stays open after submission so the player can issue another
	/// command; Escape closes it.
	/// </summary>
	public bool Submit()
	{
		if (!_open)
		{
			return false;
		}

		var text = _editor.Input.Trim();
		if (text.Length == 0)
		{
			return false;
		}

		_control.TryExecute(text);
		AddHistory(text);
		_editor.SetInput("");
		_historyIndex = -1;
		_draft = "";
		ClearUndoRedo();
		ResetCompletions();
		return true;
	}

	/// <summary>
	/// Enter's own rule, the one Minecraft's chat window follows: a line with text EXECUTES and the window stays open so another
	/// command can follow, while an empty line (or one holding only spaces) CLOSES the input — the player does not reach for the chat
	/// key again to dismiss an empty prompt (user report 2026-10-25: "命令框是空的,按回车键为什么不能关闭命令框啊?"). Returns whether
	/// the key was consumed.
	/// </summary>
	public bool SubmitOrClose()
	{
		if (!_open)
		{
			return false;
		}

		if (_editor.Input.Trim().Length == 0)
		{
			Close();
			return true;
		}

		return Submit();
	}

	public bool Escape()
	{
		if (!_open)
		{
			return false;
		}

		Close();
		return true;
	}

	public bool PreviousHistory()
	{
		if (!_open || _history.Count == 0)
		{
			return false;
		}

		if (_historyIndex == -1)
		{
			_draft = _editor.Input;
			_historyIndex = _history.Count - 1;
		}
		else if (_historyIndex > 0)
		{
			_historyIndex--;
		}
		else
		{
			return false;
		}

		_editor.SetInput(_history[_historyIndex]);
		ResetCompletions();
		return true;
	}

	public bool NextHistory()
	{
		if (!_open || _historyIndex < 0)
		{
			return false;
		}

		_historyIndex++;
		if (_historyIndex >= _history.Count)
		{
			_historyIndex = -1;
			_editor.SetInput(_draft);
			_draft = "";
		}
		else
		{
			_editor.SetInput(_history[_historyIndex]);
		}

		ResetCompletions();
		return true;
	}

	/// <summary>
	/// Cycles through the current completion candidates. The first Tab picks the
	/// first candidate; subsequent Tabs cycle through the same candidate list
	/// until the user types or changes the line.
	/// </summary>
	public bool CycleCompletion()
	{
		if (!_open)
		{
			return false;
		}

		var input = _editor.Input;
		var candidates = _completion.Suggest(input);
		if (candidates.Count == 0)
		{
			ResetCompletions();
			return false;
		}

		CaptureUndo();

		if (_completionCandidates.Count == 0)
		{
			_completionCandidates = candidates;
			_completionIndex = 0;
		}
		else
		{
			_completionIndex++;
			if (_completionIndex >= _completionCandidates.Count)
			{
				_completionIndex = 0;
			}
		}

		var completed = ReplaceCurrentToken(input, _editor.Cursor, _completionCandidates[_completionIndex].Text, _completionCandidates[_completionIndex].Verbatim);
		_editor.SetInput(completed, completed.Length);
		return true;
	}

	/// <summary>Applies a specific suggestion chosen from the rendered list.</summary>
	public bool AcceptSuggestion(CommandSuggestion suggestion)
	{
		if (!_open)
		{
			return false;
		}

		CaptureUndo();
		var input = _editor.Input;
		var completed = ReplaceCurrentToken(input, _editor.Cursor, suggestion.Text, suggestion.Verbatim);
		_editor.SetInput(completed, completed.Length);
		ResetCompletions();
		return true;
	}

	/// <summary>Reverts the most recent editing operation.</summary>
	public bool Undo()
	{
		if (!_open)
		{
			return false;
		}

		if (!_editHistory.TryUndo(_editor.Input, _editor.Cursor, out var input, out var cursor))
		{
			return false;
		}

		RestoreState(input, cursor);
		return true;
	}

	/// <summary>Reapplies the most recently undone editing operation.</summary>
	public bool Redo()
	{
		if (!_open)
		{
			return false;
		}

		if (!_editHistory.TryRedo(_editor.Input, _editor.Cursor, out var input, out var cursor))
		{
			return false;
		}

		RestoreState(input, cursor);
		return true;
	}

	private void AddHistory(string text)
	{
		_history.Add(text);
		if (_history.Count > 64)
		{
			_history.RemoveAt(0);
		}
	}

	private void ResetCompletions()
	{
		_completionCandidates = [];
		_completionIndex = -1;
	}

	private void CaptureUndo() => _editHistory.Capture(_editor.Input, _editor.Cursor);

	private void RestoreState(string input, int cursor)
	{
		_editor.RestoreState(input, cursor);
		ResetCompletions();
	}

	private void ClearUndoRedo() => _editHistory.Clear();

	private static string ReplaceCurrentToken(string input, int cursor, string candidate, bool verbatim)
	{
		var token = CommandLineTokenizer.TokenAtCursor(input, cursor);
		var isCommandToken = token.Start == 0
			&& input.Length > 0
			&& input[0] == '/'
			&& token.Text.StartsWith("/", StringComparison.Ordinal);
		var replacement = verbatim
			? candidate
			: isCommandToken
				? candidate.StartsWith("/", StringComparison.Ordinal) ? candidate : "/" + candidate
				: CommandLineTokenizer.QuoteIfNeeded(candidate);

		if (token.Length == 0)
		{
			return input.Insert(token.Start, replacement);
		}

		return input.Substring(0, token.Start) + replacement + input.Substring(token.Start + token.Length);
	}
}
