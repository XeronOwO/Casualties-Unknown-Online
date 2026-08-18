namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 22; // v22: EntityEventKind.MinePressed — a v21 peer would drop the transient mine press visual (the handshake refuses silent cross-version degradation by policy)
}
