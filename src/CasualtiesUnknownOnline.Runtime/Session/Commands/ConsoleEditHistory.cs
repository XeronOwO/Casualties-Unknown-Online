using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Bounded undo/redo stack for the console input line. It stores only the input
/// text and cursor position; the owner decides what else to restore (selection,
/// completion state) when a snapshot is applied.
/// </summary>
public sealed class ConsoleEditHistory
{
	private const int MaxDepth = 64;

	private readonly List<InputSnapshot> _undo = [];
	private readonly List<InputSnapshot> _redo = [];

	public bool CanUndo => _undo.Count > 0;

	public bool CanRedo => _redo.Count > 0;

	public void Capture(string input, int cursor)
	{
		_undo.Add(new InputSnapshot(input, cursor));
		if (_undo.Count > MaxDepth)
		{
			_undo.RemoveAt(0);
		}

		_redo.Clear();
	}

	public bool TryUndo(string currentInput, int currentCursor, out string input, out int cursor)
	{
		if (_undo.Count == 0)
		{
			input = currentInput;
			cursor = currentCursor;
			return false;
		}

		_redo.Add(new InputSnapshot(currentInput, currentCursor));
		var snapshot = _undo[_undo.Count - 1];
		_undo.RemoveAt(_undo.Count - 1);
		input = snapshot.Input;
		cursor = snapshot.Cursor;
		return true;
	}

	public bool TryRedo(string currentInput, int currentCursor, out string input, out int cursor)
	{
		if (_redo.Count == 0)
		{
			input = currentInput;
			cursor = currentCursor;
			return false;
		}

		_undo.Add(new InputSnapshot(currentInput, currentCursor));
		var snapshot = _redo[_redo.Count - 1];
		_redo.RemoveAt(_redo.Count - 1);
		input = snapshot.Input;
		cursor = snapshot.Cursor;
		return true;
	}

	public void Clear()
	{
		_undo.Clear();
		_redo.Clear();
	}

	private readonly record struct InputSnapshot(string Input, int Cursor);
}
