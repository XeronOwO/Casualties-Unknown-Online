using System;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// The number grammar of the item state language: the literal forms Minecraft's current
/// SNBT accepts, resolved to this language's two numeric kinds.
///
/// <para>
/// The suffixes are Minecraft's — <c>b</c>/<c>s</c>/<c>i</c>/<c>L</c> for integers,
/// <c>f</c>/<c>d</c> for floats, the <c>s</c>/<c>u</c> signedness prefixes (25w09a),
/// hexadecimal/binary literals and <c>_</c> digit groups — and a literal with no suffix
/// is an int when it has no decimal point and a double when it has one. A value that is
/// not finite is refused: it would travel into the record as poison.
/// </para>
///
/// <para>
/// The base prefix is scanned BEFORE the suffix: <c>F</c> is a hexadecimal digit and also
/// the float suffix, so <c>0x1F</c> is one number, exactly as Minecraft reads it.
/// </para>
/// </summary>
internal static class SnbtNumber
{
	internal static bool TryParse(string literal, out SnbtValue value, out string reason)
	{
		value = new SnbtInteger(0);
		reason = "";
		if (literal.Length == 0)
		{
			reason = "an empty number";
			return false;
		}

		var index = 0;
		var negative = false;
		if (literal[0] is '-' or '+')
		{
			negative = literal[0] == '-';
			index = 1;
		}

		if (index >= literal.Length)
		{
			reason = $"'{literal}' has a sign but no digits";
			return false;
		}

		var isHex = index + 1 < literal.Length && literal[index] == '0' && (literal[index + 1] is 'x' or 'X');
		var isBinary = !isHex && index + 1 < literal.Length && literal[index] == '0' && (literal[index + 1] is 'b' or 'B');
		if (isHex || isBinary)
		{
			index += 2;
			var digitStart = index;
			while (index < literal.Length && (IsBaseDigit(literal[index], isHex) || literal[index] == '_'))
			{
				if (literal[index] == '_' && !UnderscoreBetweenDigits(literal, index, c => IsBaseDigit(c, isHex)))
				{
					reason = "an underscore must sit between two digits";
					return false;
				}

				index++;
			}

			var digits = literal.Substring(digitStart, index - digitStart).Replace("_", "");
			if (digits.Length == 0)
			{
				reason = $"'{literal}' has no digits after its base prefix";
				return false;
			}

			if (!TryReadSuffix(literal, index, out var baseSuffix, out var baseUnsigned, out reason))
			{
				return false;
			}

			if (baseSuffix is 'f' or 'd')
			{
				reason = $"'{literal}' puts a float suffix on a {(isHex ? "hexadecimal" : "binary")} integer";
				return false;
			}

			return TryBuildInteger(literal, negative, digits, isHex ? 16 : 2, baseSuffix, baseUnsigned, out value, out reason);
		}

		var integerStart = index;
		while (index < literal.Length && (char.IsDigit(literal[index]) || literal[index] == '_'))
		{
			if (literal[index] == '_' && !UnderscoreBetweenDigits(literal, index, char.IsDigit))
			{
				reason = "an underscore must sit between two digits";
				return false;
			}

			index++;
		}

		var hasPoint = false;
		if (index < literal.Length && literal[index] == '.')
		{
			hasPoint = true;
			index++;
			while (index < literal.Length && (char.IsDigit(literal[index]) || literal[index] == '_'))
			{
				if (literal[index] == '_' && !UnderscoreBetweenDigits(literal, index, char.IsDigit))
				{
					reason = "an underscore must sit between two digits";
					return false;
				}

				index++;
			}
		}

		var hasExponent = false;
		if (index < literal.Length && literal[index] is 'e' or 'E')
		{
			hasExponent = true;
			index++;
			if (index < literal.Length && literal[index] is '-' or '+')
			{
				index++;
			}

			while (index < literal.Length && char.IsDigit(literal[index]))
			{
				index++;
			}
		}

		if (index == integerStart && !hasPoint)
		{
			reason = $"'{literal}' has no digits";
			return false;
		}

		if (!TryReadSuffix(literal, index, out var suffix, out var unsigned, out reason))
		{
			return false;
		}

		var body = literal.Substring(integerStart, index - integerStart).Replace("_", "");
		if (hasPoint || hasExponent || suffix is 'f' or 'd')
		{
			return TryBuildFloat(literal, negative, body, suffix, out value, out reason);
		}

		return TryBuildInteger(literal, negative, body, 10, suffix, unsigned, out value, out reason);
	}

	/// <summary>The suffix must be the whole tail: nothing follows it, and at most the signedness letter plus the type letter are there.</summary>
	private static bool TryReadSuffix(string literal, int index, out char kind, out bool unsigned, out string reason)
	{
		kind = '\0';
		unsigned = false;
		reason = "";
		var tail = literal.Substring(index);
		if (tail.Length == 0)
		{
			return true;
		}

		if (tail.Length > 2)
		{
			reason = $"'{literal}' has text after its number suffix";
			return false;
		}

		kind = char.ToLowerInvariant(tail[tail.Length - 1]);
		if (kind is not ('b' or 's' or 'i' or 'l' or 'f' or 'd'))
		{
			reason = $"'{literal}' does not end in a number suffix (b, s, i, L, f, d)";
			return false;
		}

		if (tail.Length == 2)
		{
			var signedness = char.ToLowerInvariant(tail[0]);
			if (signedness is not ('s' or 'u'))
			{
				reason = $"'{literal}' has two suffix letters but the first is not 's' or 'u'";
				return false;
			}

			if (kind is 'f' or 'd')
			{
				reason = $"'{literal}' puts a signedness suffix on a float";
				return false;
			}

			unsigned = signedness == 'u';
		}

		return true;
	}

	private static bool TryBuildFloat(string literal, bool negative, string body, char kind, out SnbtValue value, out string reason)
	{
		value = new SnbtFloat(0, false);
		reason = "";
		if (!double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
		{
			reason = $"'{literal}' is not a finite number";
			return false;
		}

		if (negative)
		{
			number = -number;
		}

		if (double.IsNaN(number) || double.IsInfinity(number))
		{
			reason = $"'{literal}' is not a finite number";
			return false;
		}

		if (kind == 'f')
		{
			var single = (float)number;
			if (float.IsInfinity(single) || float.IsNaN(single))
			{
				reason = $"'{literal}' does not fit a 32-bit float";
				return false;
			}

			value = new SnbtFloat(single, Single: true);
			return true;
		}

		value = new SnbtFloat(number);
		return true;
	}

	private static bool TryBuildInteger(string literal, bool negative, string digits, int radix, char kind, bool unsigned, out SnbtValue value, out string reason)
	{
		value = new SnbtInteger(0);
		reason = "";
		if (!TryParseMagnitude(digits, radix, out var magnitude))
		{
			reason = $"'{literal}' is not a {(radix == 16 ? "hexadecimal" : radix == 2 ? "binary" : "decimal")} integer";
			return false;
		}

		var width = kind switch
		{
			'b' => 8,
			's' => 16,
			'i' => 32,
			'l' => 64,
			_ => 32, // an unsuffixed literal is an int, as Minecraft resolves it
		};

		if (unsigned)
		{
			if (width < 64)
			{
				var mask = (1UL << width) - 1;
				var narrowed = magnitude & mask;
				var signBit = 1UL << (width - 1);
				value = new SnbtInteger((narrowed & signBit) != 0 ? (long)narrowed - (long)(1UL << width) : (long)narrowed);
				return true;
			}

			value = new SnbtInteger(unchecked((long)magnitude));
			return true;
		}

		if (magnitude > (ulong)long.MaxValue && !(negative && magnitude == (ulong)long.MaxValue + 1))
		{
			reason = $"'{literal}' is out of range";
			return false;
		}

		var signed = negative
			? magnitude == (ulong)long.MaxValue + 1 ? long.MinValue : -(long)magnitude
			: (long)magnitude;
		if (width < 64)
		{
			var minimum = -(1L << (width - 1));
			var maximum = (1L << (width - 1)) - 1;
			if (signed < minimum || signed > maximum)
			{
				reason = kind == '\0'
					? $"'{literal}' does not fit a 32-bit int — add 'L' for a long"
					: $"'{literal}' does not fit a {width}-bit {(kind == 'b' ? "byte" : kind == 's' ? "short" : "int")}";
				return false;
			}
		}

		value = new SnbtInteger(signed);
		return true;
	}

	private static bool TryParseMagnitude(string digits, int radix, out ulong value)
	{
		value = 0;
		if (digits.Length == 0)
		{
			return false;
		}

		foreach (var c in digits)
		{
			int digit;
			if (c >= '0' && c <= '9')
			{
				digit = c - '0';
			}
			else if (radix == 16 && c >= 'a' && c <= 'f')
			{
				digit = c - 'a' + 10;
			}
			else if (radix == 16 && c >= 'A' && c <= 'F')
			{
				digit = c - 'A' + 10;
			}
			else
			{
				return false;
			}

			if (digit >= radix)
			{
				return false;
			}

			if (value > (ulong.MaxValue - (ulong)digit) / (ulong)radix)
			{
				return false;
			}

			value = (value * (ulong)radix) + (ulong)digit;
		}

		return true;
	}

	private static bool IsBaseDigit(char c, bool hex) =>
		char.IsDigit(c) || (hex && (c is >= 'a' and <= 'f' or >= 'A' and <= 'F'));

	private static bool UnderscoreBetweenDigits(string text, int index, Func<char, bool> isDigit) =>
		index > 0 && index + 1 < text.Length && isDigit(text[index - 1]) && isDigit(text[index + 1]);
}
