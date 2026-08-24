namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 48; // v48: cross-player consumable use (PlayerItemUseRequest/Result)

}
