using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The Unity-independent input state machine for the in-game command console.
/// It owns open/close, the current line, history navigation, completion cycling
/// and submission; the Unity overlay only translates IMGUI events into these
/// methods and renders the state.
/// </summary>
public sealed class ConsoleInputSession(ICommandControl control, ICommandCompletionSource completion)
{
	private const string OpenPrefix = "/";

	private readonly ICommandControl _control = control;
	private readonly ICommandCompletionSource _completion = completion;
	private readonly List<string> _history = [];

	private bool _open;
	private string _input = "";
	private string _draft = "";
	private int _historyIndex = -1;
	private int _cursor;
	private IReadOnlyList<CommandSuggestion> _completionCandidates = [];
	private int _completionIndex = -1;

	public bool IsOpen => _open;

	public string Input => _input;

	public int Cursor => _cursor;

	public IReadOnlyList<CommandSuggestion> CompletionSuggestions => _completionCandidates;

	public IReadOnlyList<string> CompletionCandidates => [.. _completionCandidates.Select(c => c.Text)];

	public string? Hint => _open ? _completion.GetHint(_input) : null;

	public IReadOnlyList<string> History => _history;

	public void Open()
	{
		if (_open)
		{
			return;
		}

		_open = true;
		_input = OpenPrefix;
		_cursor = _input.Length;
		_historyIndex = -1;
		_draft = "";
		ResetCompletions();
	}

	public void Close()
	{
		if (!_open)
		{
			return;
		}

		_open = false;
		_input = "";
		_cursor = 0;
		_historyIndex = -1;
		_draft = "";
		ResetCompletions();
	}

	public void SetInput(string value, int? cursor = null)
	{
		var normalized = value ?? "";
		_input = normalized;
		_cursor = cursor.HasValue ? ClampCursor(cursor.Value) : normalized.Length;
		ResetCompletions();
	}

	public void SetCursor(int position)
	{
		if (_open)
		{
			_cursor = ClampCursor(position);
		}
	}

	public void InsertChar(char c)
	{
		if (!_open || char.IsControl(c))
		{
			return;
		}

		_input = _input.Substring(0, _cursor) + c + _input.Substring(_cursor);
		_cursor++;
		ResetCompletions();
	}

	public void InsertText(string text)
	{
		if (!_open || string.IsNullOrEmpty(text))
		{
			return;
		}

		_input = _input.Substring(0, _cursor) + text + _input.Substring(_cursor);
		_cursor += text.Length;
		ResetCompletions();
	}

	public bool Backspace()
	{
		if (!_open || _cursor <= 0)
		{
			return false;
		}

		_input = _input.Remove(_cursor - 1, 1);
		_cursor--;
		ResetCompletions();
		return true;
	}

	public bool Delete()
	{
		if (!_open || _cursor >= _input.Length)
		{
			return false;
		}

		_input = _input.Remove(_cursor, 1);
		ResetCompletions();
		return true;
	}

	public void MoveCursorLeft()
	{
		if (_open && _cursor > 0)
		{
			_cursor--;
		}
	}

	public void MoveCursorRight()
	{
		if (_open && _cursor < _input.Length)
		{
			_cursor++;
		}
	}

	public void MoveWordLeft()
	{
		if (!_open || _cursor <= 0)
		{
			return;
		}

		var start = _cursor;
		var i = start;
		while (i > 0 && char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		while (i > 0 && !char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		_cursor = i;
	}

	public void MoveWordRight()
	{
		if (!_open || _cursor >= _input.Length)
		{
			return;
		}

		var i = _cursor;
		while (i < _input.Length && char.IsWhiteSpace(_input[i]))
		{
			i++;
		}

		while (i < _input.Length && !char.IsWhiteSpace(_input[i]))
		{
			i++;
		}

		_cursor = i;
	}

	public bool BackspaceWord()
	{
		if (!_open || _cursor <= 0)
		{
			return false;
		}

		var start = _cursor;
		var i = start;
		while (i > 0 && char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		while (i > 0 && !char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		_input = _input.Remove(i, start - i);
		_cursor = i;
		ResetCompletions();
		return true;
	}

	public bool DeleteWord()
	{
		if (!_open || _cursor >= _input.Length)
		{
			return false;
		}

		var i = _cursor;
		while (i < _input.Length && char.IsWhiteSpace(_input[i]))
		{
			i++;
		}

		while (i < _input.Length && !char.IsWhiteSpace(_input[i]))
		{
			i++;
		}

		_input = _input.Remove(_cursor, i - _cursor);
		ResetCompletions();
		return true;
	}

	public void MoveHome()
	{
		if (_open)
		{
			_cursor = 0;
		}
	}

	public void MoveEnd()
	{
		if (_open)
		{
			_cursor = _input.Length;
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

		var text = _input.Trim();
		if (text.Length == 0)
		{
			return false;
		}

		_control.TryExecute(text);
		AddHistory(text);
		_input = "";
		_cursor = 0;
		_historyIndex = -1;
		_draft = "";
		ResetCompletions();
		return true;
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
			_draft = _input;
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

		_input = _history[_historyIndex];
		_cursor = _input.Length;
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
			_input = _draft;
			_draft = "";
		}
		else
		{
			_input = _history[_historyIndex];
		}

		_cursor = _input.Length;
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

		var candidates = _completion.Suggest(_input);
		if (candidates.Count == 0)
		{
			ResetCompletions();
			return false;
		}

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

		_input = ReplaceCurrentToken(_input, _completionCandidates[_completionIndex].Text);
		_cursor = _input.Length;
		return true;
	}

	/// <summary>Applies a specific suggestion chosen from the rendered list.</summary>
	public bool AcceptSuggestion(CommandSuggestion suggestion)
	{
		if (!_open)
		{
			return false;
		}

		_input = ReplaceCurrentToken(_input, suggestion.Text);
		_cursor = _input.Length;
		ResetCompletions();
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

	private int ClampCursor(int position) => Math.Max(0, Math.Min(_input.Length, position));

	private string ReplaceCurrentToken(string input, string candidate)
	{
		var token = CommandLineTokenizer.TokenAtCursor(input, _cursor);
		var isCommandToken = token.Start == 0
			&& input.Length > 0
			&& input[0] == '/'
			&& token.Text.StartsWith("/", StringComparison.Ordinal);
		var replacement = isCommandToken
			? candidate.StartsWith("/", StringComparison.Ordinal) ? candidate : "/" + candidate
			: CommandLineTokenizer.QuoteIfNeeded(candidate);

		if (token.Length == 0)
		{
			return input.Insert(token.Start, replacement);
		}

		return input.Substring(0, token.Start) + replacement + input.Substring(token.Start + token.Length);
	}
}
