using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The rule behind one numeric field of a page (ticket online-ui-paired-controls-and-page-scroll, S2): what
/// text counts as a number, what number a range allows, and how a value reads back into the field.
///
/// <para>
/// It is pure and lives in the Runtime for the reason <see cref="OnlineUiRowLayout"/> and
/// <see cref="OnlineUiControlBox"/> do: the policy a player's typed value is judged by is settled and tested
/// without a Unity runtime, and both sides of the surface read the same answer — the field beside a slider
/// and the slider itself are written from one value, so a value that arrived by dragging and one that
/// arrived by typing cannot disagree about what it means.
/// </para>
///
/// <para>
/// Nothing is clamped. A value outside the range is REFUSED rather than moved to the nearest end — the
/// player has to be able to see that the number they typed is not the number the session holds, and a
/// silently clamped value is one they believe they set (the same rule the colour field's own parse follows).
/// </para>
/// </summary>
public static class OnlineUiNumberField
{
	/// <summary>
	/// The number a committed text means, or null when it means none: a text that does not parse, a value
	/// outside <paramref name="minimum"/>-<paramref name="maximum"/>, and a text that is not finite. A blank
	/// text, a half-typed <c>-</c> and an overflowing number all answer null, which is the whole refusal
	/// policy in one place.
	/// </summary>
	public static float? Parse(string text, float minimum, float maximum)
	{
		if (string.IsNullOrWhiteSpace(text)
			|| !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
			|| float.IsNaN(value)
			|| float.IsInfinity(value))
		{
			return null;
		}

		return value >= minimum && value <= maximum ? value : null;
	}

	/// <summary>
	/// How a value reads in the field and beside the slider: the shortest form that still shows the hundredth
	/// it can be set to (<c>0.8</c> for four fifths, <c>3</c> for three), invariant so the text a player types
	/// is the text the surface parses. It is the ONE formatting of a numeric value on this surface, which is
	/// what keeps the field and the slider's own reading from drifting apart.
	/// </summary>
	public static string Format(float value) =>
		value.ToString("0.00", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
}
