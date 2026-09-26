using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// A player marker color as plain float RGBA. Kept in the Runtime instead of
/// using UnityEngine.Color so the palette logic is testable in L0 and the
/// Plugin layer remains the only place that sees Unity types.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S3) the marker color is a free choice rather than
/// an index into the palette, so this type also carries the hex codec the player's typed value is read
/// and written through.
/// </para>
/// </summary>
public readonly struct PlayerColorValue(float r, float g, float b, float a = 1f)
{
	/// <summary>The length of <c>#RRGGBB</c>, the form a colour is stored in when it is opaque.</summary>
	public const int HexLength = 7;

	/// <summary>The length of <c>#RRGGBBAA</c>, the form a colour is stored in when it is not.</summary>
	public const int HexAlphaLength = 9;

	public float R { get; } = r;

	public float G { get; } = g;

	public float B { get; } = b;

	public float A { get; } = a;

	public NetColorRgba ToNetColorRgba() => new(R, G, B, A);

	public static PlayerColorValue FromNetColorRgba(NetColorRgba value) => new(value.R, value.G, value.B, value.A);

	/// <summary>The same colour as the adapter boundary's plain value (the Runtime's own RGBA record).</summary>
	public OnlineUiNativeRgba ToRgba() => new(R, G, B, A);

	/// <summary>
	/// Reads a colour from its hex text — the game's own way of naming a colour
	/// (<c>ConsoleSettings.hexBackgroundColor</c> / <c>hexTextColor</c>), so a player has the same form
	/// here as in the game's console.
	///
	/// <para>
	/// Two forms are accepted, and only these: <c>#RRGGBB</c> and <c>#RRGGBBAA</c>. The three-digit
	/// shorthand is deliberately NOT accepted, because a value that is still being typed must not parse:
	/// with it, <c>#FF0</c> would commit a colour on the way to <c>#FF0000</c>. Surrounding white space is
	/// ignored and the digits may be either case; anything else — a missing <c>#</c>, a name such as
	/// <c>red</c>, a wrong digit count — is refused rather than guessed at.
	/// </para>
	/// </summary>
	public static bool TryParseHex(string? text, out PlayerColorValue color)
	{
		color = default;
		var hex = text?.Trim();
		if (hex is null || (hex.Length != HexLength && hex.Length != HexAlphaLength) || hex[0] != '#')
		{
			return false;
		}

		if (!TryByte(hex, 1, out var red) || !TryByte(hex, 3, out var green) || !TryByte(hex, 5, out var blue))
		{
			return false;
		}

		var alpha = (byte)255;
		if (hex.Length == HexAlphaLength && !TryByte(hex, 7, out alpha))
		{
			return false;
		}

		color = new PlayerColorValue(red / 255f, green / 255f, blue / 255f, alpha / 255f);
		return true;
	}

	/// <summary>
	/// The colour as the text the field shows and the configuration stores: <c>#RRGGBB</c>, or
	/// <c>#RRGGBBAA</c> when the colour is not opaque. Uppercase and invariant, so the text of a colour
	/// compares by equality — which is how the page tells whether the stored colour is one of the palette.
	/// </summary>
	public string ToHexString()
	{
		var hex = ToRgba().ToHex();
		return A >= 1f ? hex.Substring(0, HexLength) : hex;
	}

	/// <summary>Two hex digits at <paramref name="start"/>, each of them checked by hand: the numeric
	/// parsers accept white space and a sign, and a colour must carry neither.</summary>
	private static bool TryByte(string hex, int start, out byte value)
	{
		if (TryNibble(hex[start], out var high) && TryNibble(hex[start + 1], out var low))
		{
			value = (byte)((high << 4) | low);
			return true;
		}

		value = 0;
		return false;
	}

	private static bool TryNibble(char digit, out int value)
	{
		if (digit >= '0' && digit <= '9')
		{
			value = digit - '0';
			return true;
		}

		if (digit >= 'a' && digit <= 'f')
		{
			value = digit - 'a' + 10;
			return true;
		}

		if (digit >= 'A' && digit <= 'F')
		{
			value = digit - 'A' + 10;
			return true;
		}

		value = 0;
		return false;
	}
}
