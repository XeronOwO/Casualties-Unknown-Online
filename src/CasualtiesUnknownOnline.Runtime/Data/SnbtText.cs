using System.Globalization;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// The quoted-string reader shared by the value parser and the path parser: one escape
/// table, one refusal wording, so a quoted key reads the same in both places.
///
/// <para>
/// The escapes are Minecraft's: <c>\\ \" \' \n \t \r \b \f \s \xhh \uhhhh \Uhhhhhhhh</c>.
/// A named escape (<c>\N{…}</c>) is refused by name, like the value parser refuses it.
/// </para>
/// </summary>
internal static class SnbtText
{
	internal static bool TryReadQuoted(string text, ref int index, out string value, out string? error)
	{
		value = "";
		error = null;
		if (index >= text.Length || text[index] is not ('"' or '\''))
		{
			error = $"at {index + 1}: expected a quoted string";
			return false;
		}

		var quote = text[index];
		index++;
		var builder = new StringBuilder();
		while (true)
		{
			if (index >= text.Length)
			{
				error = $"at {index + 1}: the quoted string does not close";
				return false;
			}

			var c = text[index];
			index++;
			if (c == quote)
			{
				value = builder.ToString();
				return true;
			}

			if (c != '\\')
			{
				builder.Append(c);
				continue;
			}

			if (index >= text.Length)
			{
				error = $"at {index + 1}: the quoted string ends in the middle of an escape";
				return false;
			}

			var escape = text[index];
			index++;
			switch (escape)
			{
				case '\\' or '"' or '\'':
					builder.Append(escape);
					break;
				case 'n':
					builder.Append('\n');
					break;
				case 't':
					builder.Append('\t');
					break;
				case 'r':
					builder.Append('\r');
					break;
				case 'b':
					builder.Append('\b');
					break;
				case 'f':
					builder.Append('\f');
					break;
				case 's':
					builder.Append(' ');
					break;
				case 'x':
					if (!TryReadHex(text, ref index, 2, builder, out error))
					{
						return false;
					}

					break;
				case 'u':
					if (!TryReadHex(text, ref index, 4, builder, out error))
					{
						return false;
					}

					break;
				case 'U':
					if (!TryReadCodePoint(text, ref index, builder, out error))
					{
						return false;
					}

					break;
				case 'N':
					error = $"at {index + 1}: a named escape (\\N{{…}}) is not part of the item state language";
					return false;
				default:
					error = $"at {index + 1}: '\\{escape}' is not an escape this language knows";
					return false;
			}
		}
	}

	private static bool TryReadHex(string text, ref int index, int digits, StringBuilder builder, out string? error)
	{
		error = null;
		if (index + digits > text.Length
			|| !int.TryParse(text.Substring(index, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
		{
			error = $"at {index + 1}: an escape needs {digits} hexadecimal digit(s)";
			return false;
		}

		index += digits;
		builder.Append((char)code);
		return true;
	}

	private static bool TryReadCodePoint(string text, ref int index, StringBuilder builder, out string? error)
	{
		error = null;
		if (index + 8 > text.Length
			|| !int.TryParse(text.Substring(index, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
			|| code < 0
			|| code > 0x10FFFF
			|| code is >= 0xD800 and <= 0xDFFF)
		{
			error = $"at {index + 1}: an escape needs 8 hexadecimal digit(s) naming a Unicode code point";
			return false;
		}

		index += 8;
		builder.Append(char.ConvertFromUtf32(code));
		return true;
	}
}
