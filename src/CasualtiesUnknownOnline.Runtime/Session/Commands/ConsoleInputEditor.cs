using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The pure text-editing state of one console input line: text, cursor and
/// selection. It is separate from <see cref="ConsoleInputSession"/> so history,
/// completion and open/close state do not accumulate into one oversized type.
/// </summary>
public sealed class ConsoleInputEditor
{
	private string _input = "";
	private int _cursor;
	private int _selectionStart = -1;
	private int _selectionEnd = -1;

	public string Input => _input;

	public int Cursor => _cursor;

	public int SelectionStart => _selectionStart;

	public int SelectionEnd => _selectionEnd;

	public bool HasSelection => _selectionStart >= 0
		&& _selectionEnd >= 0
		&& _selectionStart != _selectionEnd;

	public string SelectedText => HasSelection
		? _input.Substring(_selectionStart, _selectionEnd - _selectionStart)
		: "";

	public void SetInput(string value, int? cursor = null)
	{
		var normalized = value ?? "";
		_input = normalized;
		_cursor = cursor.HasValue ? ClampCursor(cursor.Value) : normalized.Length;
		ClearSelection();
	}

	public void SetCursor(int position)
	{
		_cursor = ClampCursor(position);
		ClearSelection();
	}

	public void SelectAll()
	{
		_selectionStart = 0;
		_selectionEnd = _input.Length;
		_cursor = _input.Length;
	}

	public void SetSelection(int start, int end)
	{
		var a = ClampCursor(start);
		var b = ClampCursor(end);
		_selectionStart = Math.Min(a, b);
		_selectionEnd = Math.Max(a, b);
		_cursor = _selectionEnd;
	}

	public bool DeleteSelection()
	{
		if (!HasSelection)
		{
			return false;
		}

		_input = _input.Remove(_selectionStart, _selectionEnd - _selectionStart);
		_cursor = _selectionStart;
		ClearSelection();
		return true;
	}

	public void InsertChar(char c)
	{
		if (char.IsControl(c))
		{
			return;
		}

		DeleteSelection();
		_input = _input.Substring(0, _cursor) + c + _input.Substring(_cursor);
		_cursor++;
	}

	public void InsertText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		DeleteSelection();
		_input = _input.Substring(0, _cursor) + text + _input.Substring(_cursor);
		_cursor += text.Length;
	}

	public bool Backspace()
	{
		if (DeleteSelection())
		{
			return true;
		}

		if (_cursor <= 0)
		{
			return false;
		}

		_input = _input.Remove(_cursor - 1, 1);
		_cursor--;
		return true;
	}

	public bool Delete()
	{
		if (DeleteSelection())
		{
			return true;
		}

		if (_cursor >= _input.Length)
		{
			return false;
		}

		_input = _input.Remove(_cursor, 1);
		return true;
	}

	public void MoveCursorLeft(bool extendSelection = false)
	{
		if (extendSelection)
		{
			var anchor = HasSelection ? _selectionStart : _cursor;
			var target = Math.Max(0, _cursor - 1);
			SetSelection(anchor, target);
			return;
		}

		if (_cursor > 0)
		{
			_cursor--;
		}

		ClearSelection();
	}

	public void MoveCursorRight(bool extendSelection = false)
	{
		if (extendSelection)
		{
			var anchor = HasSelection ? _selectionStart : _cursor;
			var target = Math.Min(_input.Length, _cursor + 1);
			SetSelection(anchor, target);
			return;
		}

		if (_cursor < _input.Length)
		{
			_cursor++;
		}

		ClearSelection();
	}

	public void MoveWordLeft()
	{
		if (_cursor <= 0)
		{
			return;
		}

		var i = _cursor;
		while (i > 0 && char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		while (i > 0 && !char.IsWhiteSpace(_input[i - 1]))
		{
			i--;
		}

		_cursor = i;
		ClearSelection();
	}

	public void MoveWordRight()
	{
		if (_cursor >= _input.Length)
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
		ClearSelection();
	}

	public bool BackspaceWord()
	{
		if (DeleteSelection())
		{
			return true;
		}

		if (_cursor <= 0)
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
		return true;
	}

	public bool DeleteWord()
	{
		if (DeleteSelection())
		{
			return true;
		}

		if (_cursor >= _input.Length)
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
		return true;
	}

	public void MoveHome(bool extendSelection = false)
	{
		if (extendSelection)
		{
			SetSelection(_cursor, 0);
			return;
		}

		_cursor = 0;
		ClearSelection();
	}

	public void MoveEnd(bool extendSelection = false)
	{
		if (extendSelection)
		{
			SetSelection(_cursor, _input.Length);
			return;
		}

		_cursor = _input.Length;
		ClearSelection();
	}

	public void RestoreState(string input, int cursor)
	{
		_input = input;
		_cursor = ClampCursor(cursor);
		ClearSelection();
	}

	private void ClearSelection()
	{
		_selectionStart = -1;
		_selectionEnd = -1;
	}

	private int ClampCursor(int position) => Math.Max(0, Math.Min(_input.Length, position));
}
