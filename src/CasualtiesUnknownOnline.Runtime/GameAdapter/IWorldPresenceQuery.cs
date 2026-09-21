namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// Whether the game currently holds a world. The lobby switches, the world
/// library's armed restore and the Online UI's Worlds page all gate on it, and
/// none of them needs any other adapter capability.
/// </summary>
public interface IWorldPresenceQuery
{
	/// <summary>True while the local player is in a world or one is generating — lobby switches are refused in that window (menu-only switch policy).</summary>
	bool IsInWorldOrGenerating { get; }
}
