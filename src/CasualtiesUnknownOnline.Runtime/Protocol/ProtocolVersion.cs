namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 41; // v41: CharacterAttackAnim — peers replay Body.Attack's attackAnim prefab on the owner's clone

}
