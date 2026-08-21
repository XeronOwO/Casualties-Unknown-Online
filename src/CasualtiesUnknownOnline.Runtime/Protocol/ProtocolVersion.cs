namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 26; // v26: PlayerInventoryTakeRequestMsg / PlayerInventoryTransferMsg (NetMsg 97/98) — direct cross-player item taking; v25 peers cannot participate in the take interaction

}
