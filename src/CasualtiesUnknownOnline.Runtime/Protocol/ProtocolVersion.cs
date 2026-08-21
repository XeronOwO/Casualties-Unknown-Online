namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 27; // v27: PlayerCarryStartRequestMsg / PlayerCarryStopRequestMsg / PlayerCarryStateMsg (NetMsg 99-101) — direct cross-player carry/release; v26 peers cannot participate in the carry interaction

}
