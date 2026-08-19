namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 23; // v23: EntityEventKind.CrystalUnstableTicked — a v22 peer would drop the transient crystal ticking visual (the handshake refuses silent cross-version degradation by policy); v22 was MinePressed
}
