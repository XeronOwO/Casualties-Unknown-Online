namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 14; // v14: CrystalMimicTriggered entity event (kind 30) — a v13 peer would leave the mimic's one-shot latch unconsumed and re-trigger the crystalenemy spawn, so mixed-version sessions are refused instead of silently degrading
}
