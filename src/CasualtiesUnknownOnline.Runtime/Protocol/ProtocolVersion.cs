namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 2; // v2: PlayerJoin roster fields, PlayerLeave active, SceneState relay id
}
