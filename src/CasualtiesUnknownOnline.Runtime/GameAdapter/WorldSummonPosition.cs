namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// Where a <c>/summon</c> puts its object: a keyword resolved on the acting client's scene, or the literal
/// <c>&lt;x&gt;,&lt;y&gt;</c> the operator typed (<see cref="Anchor"/> is null in that case).
/// </summary>
public readonly record struct WorldSummonPosition(WorldSummonAnchor? Anchor, float X, float Y)
{
	/// <summary>The native command's default: the world point under the pointer.</summary>
	public static WorldSummonPosition Default => new(WorldSummonAnchor.Cursor, 0f, 0f);
}
