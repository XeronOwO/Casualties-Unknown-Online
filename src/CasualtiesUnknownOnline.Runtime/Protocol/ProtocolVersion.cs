namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 38; // v38: WorldSnapshotComplete marks the end of the world-entry snapshot group — a v37 peer cannot know when a join backfill is complete

}
