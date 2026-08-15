namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 8; // v8: runtime enemy spawn binding + late-joiner materialization (EnemySnapshot.RuntimeSpawns) — a v7 peer would leave cave-tick spawns unsynced, so mixed-version sessions are refused instead of silently degrading enemy sync
}
