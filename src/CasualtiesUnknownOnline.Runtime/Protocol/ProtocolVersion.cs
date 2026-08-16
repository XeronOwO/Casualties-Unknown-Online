namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 11; // v11: building-entity health snapshot (NetMsg 88) — a v10 peer would drop the damaged-entity backfill, so mixed-version sessions are refused instead of silently degrading
}
