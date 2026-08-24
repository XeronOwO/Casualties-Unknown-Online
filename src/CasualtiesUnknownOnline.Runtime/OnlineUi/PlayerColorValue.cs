namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// A player marker color as plain float RGBA. Kept in the Runtime instead of
/// using UnityEngine.Color so the palette logic is testable in L0 and the
/// Plugin layer remains the only place that sees Unity types.
/// </summary>
public readonly struct PlayerColorValue(float r, float g, float b, float a = 1f)
{
	public float R { get; } = r;

	public float G { get; } = g;

	public float B { get; } = b;

	public float A { get; } = a;
}
