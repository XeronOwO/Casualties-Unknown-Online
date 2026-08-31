using System;
using System.Collections.Generic;

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
	private IReadOnlyList<string> _completionCandidates = [];
	private int _completionIndex = -1;

	public bool IsOpen => _open;

	public string Input => _input;

	public IReadOnlyList<string> CompletionCandidates => _completionCandidates;

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
		_historyIndex = -1;
		_draft = "";
		ResetCompletions();
	}

	public void SetInput(string value)
	{
		if (string.Equals(_input, value ?? "", StringComparison.Ordinal))
		{
			return;
		}

		_input = value ?? "";
		ResetCompletions();
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

		_input = ReplaceCurrentToken(_input, _completionCandidates[_completionIndex]);
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

	private static string ReplaceCurrentToken(string input, string candidate)
	{
		var token = CommandLineTokenizer.CurrentToken(input);
		var isCommandToken = token.Start == 0
			&& input.Length > 0
			&& input[0] == '/'
			&& token.Text.StartsWith("/", StringComparison.Ordinal);
		var replacement = isCommandToken
			? candidate.StartsWith("/", StringComparison.Ordinal) ? candidate : "/" + candidate
			: CommandLineTokenizer.QuoteIfNeeded(candidate);

		if (token.Length == 0)
		{
			return input + replacement;
		}

		return input.Substring(0, token.Start) + replacement + input.Substring(token.Start + token.Length);
	}
}
