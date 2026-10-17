using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// Reads and writes the path text. A path is one command token: <c>Keys[0].Field</c>,
/// <c>Components.Type.field</c>, <c>"A key"[1]</c>. A dot before an index is accepted
/// (<c>Keys.[0]</c>) and the canonical text omits it.
/// </summary>
public static class NbtPathParser
{
	/// <summary>Parses one path; an empty text is the root path.</summary>
	public static bool TryParse(string text, out NbtPath path, out string? error)
	{
		path = NbtPath.Root;
		error = null;
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}

		var index = 0;
		var segments = new List<NbtPathSegment>();
		while (true)
		{
			if (index >= text.Length)
			{
				break;
			}

			if (text[index] == '[')
			{
				if (segments.Count == 0)
				{
					error = "a path starts with a key, not an index";
					return false;
				}

				if (!TryReadIndex(text, ref index, out var parsed, out error))
				{
					return false;
				}

				segments.Add(parsed);
			}
			else
			{
				if (!TryReadKey(text, ref index, out var key, out error))
				{
					return false;
				}

				if (segments.Count == 0 && key.Length == 0)
				{
					error = "an empty key is not accepted";
					return false;
				}

				segments.Add(new NbtPathKey(key));
			}

			if (index >= text.Length)
			{
				break;
			}

			if (text[index] == '.')
			{
				index++;
				if (index >= text.Length)
				{
					error = "the path ends with a dot";
					return false;
				}

				continue;
			}

			if (text[index] == '[')
			{
				continue;
			}

			error = $"at {index + 1}: expected '.', '[' or the end of the path, found '{text[index]}'";
			return false;
		}

		path = new NbtPath(segments);
		return true;
	}

	private static bool TryReadKey(string text, ref int index, out string key, out string? error)
	{
		key = "";
		error = null;
		if (text[index] is '"' or '\'')
		{
			if (!SnbtText.TryReadQuoted(text, ref index, out key, out error))
			{
				return false;
			}

			if (key.Length == 0)
			{
				error = "an empty key is not accepted";
				return false;
			}

			return true;
		}

		var start = index;
		while (index < text.Length && IsBareChar(text[index]))
		{
			index++;
		}

		if (index == start)
		{
			error = $"at {index + 1}: expected a key, found '{text[index]}'";
			return false;
		}

		key = text.Substring(start, index - start);
		return true;
	}

	private static bool TryReadIndex(string text, ref int index, out NbtPathSegment segment, out string? error)
	{
		segment = new NbtPathIndex(0);
		error = null;
		index++; // '['
		var start = index;
		var hasSign = index < text.Length && text[index] == '-';
		if (hasSign)
		{
			index++;
		}

		while (index < text.Length && char.IsDigit(text[index]))
		{
			index++;
		}

		if (index == start || (hasSign && index == start + 1))
		{
			if (!hasSign && index < text.Length && text[index] == ']')
			{
				error = "the wildcard '[]' is not part of this path language — name one index, e.g. <list>[0]";
				return false;
			}

			if (!hasSign && index < text.Length && text[index] == '{')
			{
				error = "a filter '[...]' is not part of this path language — name one index, e.g. <list>[0]";
				return false;
			}

			error = "a list index is a whole number, e.g. <list>[0]";
			return false;
		}

		if (index >= text.Length || text[index] != ']')
		{
			error = $"at {index + 1}: expected ']' after a list index";
			return false;
		}

		if (!int.TryParse(text.Substring(start, index - start), out var parsed))
		{
			error = $"at {start + 1}: the list index is out of range";
			return false;
		}

		index++;
		segment = new NbtPathIndex(parsed);
		return true;
	}

	private static bool IsBareChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '+';
}
