using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One colour read from the game's own UI, as four 0..1 channels. The Runtime cannot reference
/// UnityEngine, so the adapter hands the channels across the boundary as this plain value and the
/// reading side formats them — which is what keeps a fact the later stages consume testable.
/// </summary>
public readonly record struct OnlineUiNativeRgba(float R, float G, float B, float A)
{
	/// <summary>The colour in the game's own hex idiom (<c>#RRGGBBAA</c>), invariant culture.</summary>
	public string ToHex() => string.Format(
		CultureInfo.InvariantCulture,
		"#{0:X2}{1:X2}{2:X2}{3:X2}",
		Channel(R),
		Channel(G),
		Channel(B),
		Channel(A));

	/// <summary>One channel as a byte. An out-of-range channel is clamped rather than thrown on: the probe
	/// reports what the game actually has, and a surprising channel is a fact, not a crash.</summary>
	private static int Channel(float value)
	{
		var clamped = value < 0f ? 0f : value > 1f ? 1f : value;
		return (int)((clamped * 255f) + 0.5f);
	}
}
