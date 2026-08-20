using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Engine-agnostic RGBA color. The Runtime never references UnityEngine, so a
/// synced color (the crystalenemy presentation tint carried as creation data)
/// travels as NetColorRgba and the Game Adapter converts to/from Unity's Color
/// at the boundary.
/// </summary>
public readonly struct NetColorRgba(float r, float g, float b, float a)
{
	public static NetColorRgba None { get; } = new(0f, 0f, 0f, 0f);

	public readonly float R = r;
	public readonly float G = g;
	public readonly float B = b;
	public readonly float A = a;

	/// <summary>Domain → wire; the reverse lives on <see cref="NetColorRgbaMsg"/>.</summary>
	public NetColorRgbaMsg ToNetColorRgbaMsg() => new(R, G, B, A);

	public override string ToString() => $"({R:F2}, {G:F2}, {B:F2}, {A:F2})";
}
