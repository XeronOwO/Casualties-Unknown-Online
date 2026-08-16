namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 13; // v13: world-time request/broadcast (NetMsg 90/91) — a v12 peer would keep its own timeScale and diverge world timers, so mixed-version sessions are refused instead of silently degrading
}
