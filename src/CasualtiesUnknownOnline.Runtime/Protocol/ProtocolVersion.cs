namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 7; // v7: host-ordered enemy attacks (EnemyAttack 83 / EnemyLunge 84) — a v6 peer would neither order nor apply remote-player attacks, so mixed-version sessions are refused by the version gate instead of silently degrading combat
}
