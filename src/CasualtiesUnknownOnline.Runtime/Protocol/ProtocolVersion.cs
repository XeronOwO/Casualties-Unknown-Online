namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 12; // v12: partial block-damage snapshot (NetMsg 89) + BlockDamagedMsg.MetalBonus — a v11 peer would lose accumulated block HP and apply the wrong metallic multiplier, so mixed-version sessions are refused instead of silently degrading
}
