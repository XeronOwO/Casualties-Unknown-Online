namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// Engine-agnostic 2D vector. The Runtime never references UnityEngine, so
/// synced positions travel as NetVector2 and the Game Adapter converts to/from
/// Unity's Vector2 at the boundary.
/// </summary>
public readonly struct NetVector2(float x, float y)
{
	public readonly float X = x;
	public readonly float Y = y;
}
