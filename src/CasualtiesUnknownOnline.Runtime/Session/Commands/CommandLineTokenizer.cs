using System.Collections.Generic;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Whitespace/quote/bracket-aware command line splitting. It treats double and
/// single quotes as literal grouping, backslash as escaping, and keeps
/// <c>[]</c>/<c>{}</c>/<c>()</c> groups as one token when they contain spaces —
/// the minimal support needed for selectors and JSON-like values without
/// knowing every command's grammar.
/// </summary>
public static class CommandLineTokenizer
{
	/// <summary>One raw token plus its span and quoted flag.</summary>
	public readonly record struct Token(string Text, int Start, int Length, bool Quoted)
	{
		/// <summary>The token text with one level of surrounding quotes removed.</summary>
		public string Unquoted => Unquote(Text, Quoted);
	}

	/// <summary>Splits a command line into raw tokens.</summary>
	public static IReadOnlyList<Token> Tokenize(string text)
	{
		var tokens = new List<Token>();
		var start = -1;
		var length = 0;
		var quote = '\0';
		var depth = 0;
		var escaped = false;
		var hadQuote = false;

		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (escaped)
			{
				if (start < 0)
				{
					start = i;
				}

				length++;
				escaped = false;
				continue;
			}

			if (c == '\\')
			{
				escaped = true;
				if (start < 0)
				{
					start = i;
				}

				length++;
				continue;
			}

			if (quote != '\0')
			{
				hadQuote = true;
				if (start < 0)
				{
					start = i;
				}

				length++;
				if (c == quote)
				{
					quote = '\0';
				}

				continue;
			}

			if (c == '"' || c == '\'')
			{
				quote = c;
				hadQuote = true;
				if (start < 0)
				{
					start = i;
				}

				length++;
				continue;
			}

			if (c is '[' or '{' or '(')
			{
				depth++;
				if (start < 0)
				{
					start = i;
				}

				length++;
				continue;
			}

			if (c is ']' or '}' or ')')
			{
				if (depth > 0)
				{
					depth--;
				}

				if (start < 0)
				{
					start = i;
				}

				length++;
				continue;
			}

			if (char.IsWhiteSpace(c) && quote == '\0' && depth == 0)
			{
				if (start >= 0)
				{
					tokens.Add(new Token(text.Substring(start, length), start, length, hadQuote));
					start = -1;
					length = 0;
					hadQuote = false;
				}

				continue;
			}

			if (start < 0)
			{
				start = i;
			}

			length++;
		}

		if (start >= 0)
		{
			tokens.Add(new Token(text.Substring(start, length), start, length, hadQuote));
		}

		return tokens;
	}

	/// <summary>Returns the token being edited at the end of the line, or an
	/// empty token at <paramref name="text"/>.Length for a trailing whitespace
	/// position.</summary>
	public static Token CurrentToken(string text)
	{
		var tokens = Tokenize(text);
		if (tokens.Count == 0)
		{
			return new Token(string.Empty, text.Length, 0, false);
		}

		var last = tokens[tokens.Count - 1];
		var end = last.Start + last.Length;
		if (end < text.Length)
		{
			return new Token(string.Empty, text.Length, 0, false);
		}

		return last;
	}

	/// <summary>Quotes a value when it needs grouping for the command line.</summary>
	public static string QuoteIfNeeded(string value)
	{
		if (value.Length == 0)
		{
			return "\"\"";
		}

		var needsQuote = ContainsWhitespaceOrSpecial(value);
		if (!needsQuote)
		{
			return value;
		}

		var builder = new StringBuilder(value.Length + 2);
		builder.Append('"');
		foreach (var c in value)
		{
			if (c == '"' || c == '\\')
			{
				builder.Append('\\');
			}

			builder.Append(c);
		}

		builder.Append('"');
		return builder.ToString();
	}

	private static bool ContainsWhitespaceOrSpecial(string value)
	{
		foreach (var c in value)
		{
			if (char.IsWhiteSpace(c) || c == '"' || c == '\\')
			{
				return true;
			}
		}

		return false;
	}

	private static string Unquote(string text, bool hadQuote)
	{
		if (!hadQuote || text.Length < 2)
		{
			return text;
		}

		var first = text[0];
		var last = text[text.Length - 1];
		if ((first != '"' && first != '\'') || first != last)
		{
			return text;
		}

		var inner = text.Substring(1, text.Length - 2);
		var builder = new StringBuilder(inner.Length);
		var escaped = false;
		foreach (var c in inner)
		{
			if (escaped)
			{
				builder.Append(c);
				escaped = false;
				continue;
			}

			if (c == '\\')
			{
				escaped = true;
				continue;
			}

			builder.Append(c);
		}

		if (escaped)
		{
			builder.Append('\\');
		}

		return builder.ToString();
	}
}
