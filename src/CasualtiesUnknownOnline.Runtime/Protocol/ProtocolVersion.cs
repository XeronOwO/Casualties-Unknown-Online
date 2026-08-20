namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 24; // v24: EntitySpawnedMsg.HasEnemyTint + EnemySpawnEntryMsg tint — a v23 peer spawns mimic-triggered crystalenemy copies WITHOUT the presentation tint (SetColor stays trigger-side local); v23 was CrystalUnstableTicked
}
