using System.Globalization;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// The item state language's text writer: the canonical form `/data get` prints and the
/// commands accept back.
///
/// <para>
/// The canonical form is Minecraft's own conversion rule — an integer carries no suffix,
/// a float carries <c>f</c>, a double <c>d</c>, a long <c>L</c>; a key or string is
/// quoted only when it has to be — so what the operator reads is what they can paste
/// back, and a value's kind is visible in the text the adapter projected.
/// </para>
/// </summary>
public static class SnbtWriter
{
	/// <summary>Writes one value in the canonical form.</summary>
	public static string Write(SnbtValue value)
	{
		var builder = new StringBuilder();
		Append(builder, value);
		return builder.ToString();
	}

	/// <summary>Whether a key needs quoting under the unquoted-key rule (digits, letters, <c>_ - . +</c>, not starting with a digit/sign/dot).</summary>
	internal static bool KeyNeedsQuotes(string key)
	{
		if (key.Length == 0)
		{
			return true;
		}

		if (char.IsDigit(key[0]) || key[0] is '-' or '.' or '+')
		{
			return true;
		}

		foreach (var c in key)
		{
			if (!IsBareChar(c))
			{
				return true;
			}
		}

		return false;
	}

	private static void Append(StringBuilder builder, SnbtValue value)
	{
		switch (value)
		{
			case SnbtString text:
				AppendQuoted(builder, text.Value);
				break;
			case SnbtInteger integer:
				builder.Append(integer.Value.ToString(CultureInfo.InvariantCulture));
				if (integer.Value is < int.MinValue or > int.MaxValue)
				{
					builder.Append('L');
				}

				break;
			case SnbtFloat number when number.Single:
				builder.Append(((float)number.Value).ToString("R", CultureInfo.InvariantCulture)).Append('f');
				break;
			case SnbtFloat number:
				builder.Append(number.Value.ToString("R", CultureInfo.InvariantCulture)).Append('d');
				break;
			case SnbtBool flag:
				builder.Append(flag.Value ? "true" : "false");
				break;
			case SnbtList list:
				builder.Append('[');
				for (var i = 0; i < list.Items.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					Append(builder, list.Items[i]);
				}

				builder.Append(']');
				break;
			case SnbtCompound compound:
				builder.Append('{');
				for (var i = 0; i < compound.Entries.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					AppendKey(builder, compound.Entries[i].Key);
					builder.Append(':');
					Append(builder, compound.Entries[i].Value);
				}

				builder.Append('}');
				break;
		}
	}

	private static void AppendKey(StringBuilder builder, string key)
	{
		if (KeyNeedsQuotes(key))
		{
			AppendQuoted(builder, key);
			return;
		}

		builder.Append(key);
	}

	private static void AppendQuoted(StringBuilder builder, string text)
	{
		builder.Append('"');
		foreach (var c in text)
		{
			switch (c)
			{
				case '"':
					builder.Append("\\\"");
					break;
				case '\\':
					builder.Append("\\\\");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				case '\r':
					builder.Append("\\r");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				case '\b':
					builder.Append("\\b");
					break;
				case '\f':
					builder.Append("\\f");
					break;
				default:
					if (c < ' ')
					{
						builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						builder.Append(c);
					}

					break;
			}
		}

		builder.Append('"');
	}

	private static bool IsBareChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or '+';
}
