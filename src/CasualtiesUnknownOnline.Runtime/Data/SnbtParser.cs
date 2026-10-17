using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// The item state language's text reader: the SNBT subset the item-data commands accept,
/// as Minecraft's current SNBT defines it (compounds, lists, quoted and unquoted strings,
/// typed number literals, escapes, heterogeneous lists, trailing commas).
///
/// <para>
/// What this language deliberately does NOT take is refused BY NAME with the accepted
/// form, never guessed at: typed arrays (<c>[B;…]</c>), the <c>bool(…)</c>/<c>uuid(…)</c>
/// operations, and wildcards or filters in a path (<see cref="NbtPathParser"/>). The item
/// state carries none of them, so accepting one would name a value the adapter cannot
/// bind.
/// </para>
///
/// <para>
/// Every refusal carries a 1-based position and says what was expected, because the
/// operator is typing this text by hand: "at 12: expected ':' after the key
/// 'Condition'". A depth bound keeps a pathological input from exhausting the stack; the
/// wire request's length is bounded by its own handler.
/// </para>
/// </summary>
public static class SnbtParser
{
	/// <summary>The deepest compound/list nesting this parser accepts; Minecraft's own NBT bound is far higher and the item state nests three levels.</summary>
	internal const int MaxDepth = 64;

	/// <summary>Parses one complete SNBT value; the whole text must be consumed.</summary>
	public static bool TryParse(string text, out SnbtValue value, out string? error)
	{
		value = SnbtCompound.Empty;
		error = null;
		if (text is null)
		{
			error = "No SNBT was given.";
			return false;
		}

		var reader = new Reader(text);
		if (!reader.TryReadValue(out var parsed, depth: 0))
		{
			error = reader.Error;
			return false;
		}

		reader.SkipWhitespace();
		if (!reader.AtEnd)
		{
			error = reader.Refuse($"expected the end of the value, found '{reader.Peek()}'");
			return false;
		}

		value = parsed;
		return true;
	}

	private sealed class Reader(string text)
	{
		private readonly string _text = text;
		private int _index;

		internal string? Error { get; private set; }

		internal bool AtEnd => _index >= _text.Length;

		internal char Peek() => _text[_index];

		internal bool TryReadValue(out SnbtValue value, int depth)
		{
			value = SnbtCompound.Empty;
			if (depth > MaxDepth)
			{
				Error = Refuse($"the value nests deeper than {MaxDepth} compounds/lists");
				return false;
			}

			SkipWhitespace();
			if (AtEnd)
			{
				Error = Refuse("expected a value, found the end of the text");
				return false;
			}

			var c = Peek();
			if (c == '{')
			{
				return TryReadCompound(out value, depth);
			}

			if (c == '[')
			{
				return TryReadList(out value, depth);
			}

			if (c is '"' or '\'')
			{
				if (!TryReadQuoted(out var quoted))
				{
					return false;
				}

				value = new SnbtString(quoted);
				return true;
			}

			if (char.IsDigit(c) || c is '-' or '+' or '.')
			{
				return TryReadNumber(out value);
			}

			if (IsUnquotedStart(c))
			{
				return TryReadWord(out value);
			}

			Error = Refuse($"expected a value, found '{c}'");
			return false;
		}

		private bool TryReadCompound(out SnbtValue value, int depth)
		{
			value = SnbtCompound.Empty;
			_index++; // '{'
			var entries = new List<SnbtEntry>();
			SkipWhitespace();
			if (!AtEnd && Peek() == '}')
			{
				_index++;
				value = new SnbtCompound(entries);
				return true;
			}

			while (true)
			{
				SkipWhitespace();
				if (AtEnd)
				{
					Error = Refuse("expected a key or '}', found the end of the text");
					return false;
				}

				if (!TryReadKey(out var key))
				{
					return false;
				}

				foreach (var entry in entries)
				{
					if (string.Equals(entry.Key, key, StringComparison.Ordinal))
					{
						Error = Refuse($"the key '{key}' appears twice in one compound");
						return false;
					}
				}

				SkipWhitespace();
				if (AtEnd || Peek() != ':')
				{
					Error = Refuse($"expected ':' after the key '{key}'");
					return false;
				}

				_index++;
				if (!TryReadValue(out var item, depth + 1))
				{
					return false;
				}

				entries.Add(new SnbtEntry(key, item));
				SkipWhitespace();
				if (AtEnd)
				{
					Error = Refuse("expected ',' or '}' , found the end of the text");
					return false;
				}

				if (Peek() == ',')
				{
					_index++;
					SkipWhitespace();
					if (!AtEnd && Peek() == '}')
					{
						_index++;
						value = new SnbtCompound(entries);
						return true;
					}

					continue;
				}

				if (Peek() == '}')
				{
					_index++;
					value = new SnbtCompound(entries);
					return true;
				}

				Error = Refuse($"expected ',' or '}}' after the value of '{key}', found '{Peek()}'");
				return false;
			}
		}

		private bool TryReadList(out SnbtValue value, int depth)
		{
			value = SnbtList.Empty;
			_index++; // '['
			SkipWhitespace();
			if (TryRefuseTypedArray())
			{
				return false;
			}

			var items = new List<SnbtValue>();
			if (!AtEnd && Peek() == ']')
			{
				_index++;
				value = new SnbtList(items);
				return true;
			}

			while (true)
			{
				if (!TryReadValue(out var item, depth + 1))
				{
					return false;
				}

				items.Add(item);
				SkipWhitespace();
				if (AtEnd)
				{
					Error = Refuse("expected ',' or ']', found the end of the text");
					return false;
				}

				if (Peek() == ',')
				{
					_index++;
					SkipWhitespace();
					if (!AtEnd && Peek() == ']')
					{
						_index++;
						value = new SnbtList(items);
						return true;
					}

					continue;
				}

				if (Peek() == ']')
				{
					_index++;
					value = new SnbtList(items);
					return true;
				}

				Error = Refuse($"expected ',' or ']', found '{Peek()}'");
				return false;
			}
		}

		/// <summary>A typed array is a DIFFERENT type from a list in Minecraft; this language has no field for it, so it says so instead of reading it as a list.</summary>
		private bool TryRefuseTypedArray()
		{
			if (AtEnd || !char.IsLetter(Peek()))
			{
				return false;
			}

			var letter = char.ToUpperInvariant(Peek());
			if (letter is not ('B' or 'I' or 'L'))
			{
				return false;
			}

			var probe = _index + 1;
			while (probe < _text.Length && char.IsWhiteSpace(_text[probe]))
			{
				probe++;
			}

			if (probe >= _text.Length || _text[probe] != ';')
			{
				return false;
			}

			Error = Refuse($"a typed array ('[{(letter == 'L' ? "L" : letter == 'I' ? "I" : "B")};…]') is not part of an item's state — write a plain list, e.g. [1,2]");
			return true;
		}

		private bool TryReadKey(out string key)
		{
			key = "";
			if (AtEnd)
			{
				Error = Refuse("expected a key");
				return false;
			}

			var c = Peek();
			if (c is '"' or '\'')
			{
				if (!TryReadQuoted(out key))
				{
					return false;
				}

				if (key.Length == 0)
				{
					Error = Refuse("an empty key is not accepted");
					return false;
				}

				return true;
			}

			var start = _index;
			while (!AtEnd && IsBareChar(Peek()))
			{
				_index++;
			}

			if (_index == start)
			{
				Error = Refuse($"expected a key, found '{c}'");
				return false;
			}

			key = _text.Substring(start, _index - start);
			return true;
		}

		private bool TryReadQuoted(out string value)
		{
			var index = _index;
			if (!SnbtText.TryReadQuoted(_text, ref index, out value, out var error))
			{
				Error = error;
				return false;
			}

			_index = index;
			return true;
		}

		private bool TryReadWord(out SnbtValue value)
		{
			value = SnbtCompound.Empty;
			var start = _index;
			while (!AtEnd && IsBareChar(Peek()))
			{
				_index++;
			}

			var word = _text.Substring(start, _index - start);
			if (string.Equals(word, "true", StringComparison.OrdinalIgnoreCase))
			{
				value = new SnbtBool(true);
				return true;
			}

			if (string.Equals(word, "false", StringComparison.OrdinalIgnoreCase))
			{
				value = new SnbtBool(false);
				return true;
			}

			if (string.Equals(word, "bool", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "uuid", StringComparison.OrdinalIgnoreCase))
			{
				Error = Refuse($"the '{word}(…)' operation is not part of the item state language — write the value itself");
				return false;
			}

			if (char.IsDigit(word[0]) || word[0] is '-' or '+' or '.')
			{
				Error = Refuse($"'{word}' is not a number");
				return false;
			}

			value = new SnbtString(word);
			return true;
		}

		private bool TryReadNumber(out SnbtValue value)
		{
			value = SnbtCompound.Empty;
			var start = _index;
			while (!AtEnd && IsNumberChar(Peek()))
			{
				_index++;
			}

			var literal = _text.Substring(start, _index - start);
			if (literal.Length == 0)
			{
				Error = Refuse($"expected a number, found '{Peek()}'");
				return false;
			}

			if (!SnbtNumber.TryParse(literal, out value, out var reason))
			{
				Error = Refuse(reason);
				return false;
			}

			return true;
		}

		internal void SkipWhitespace()
		{
			while (!AtEnd && char.IsWhiteSpace(Peek()))
			{
				_index++;
			}
		}

		internal string Refuse(string reason) => $"at {_index + 1}: {reason}";

		private static bool IsBareChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or '+';

		private static bool IsUnquotedStart(char c) => char.IsLetter(c) || c == '_';

		private static bool IsNumberChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '+' or '.' or '_';
	}
}
