namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 28; // v28: PlayerHealRequestMsg / PlayerHealResultMsg (NetMsg 102-103) — direct cross-player heal; v27 peers cannot participate in the heal interaction

}
