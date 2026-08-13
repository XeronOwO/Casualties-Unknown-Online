namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 5; // v5: the reconnect-restore fix round (OpenedEntitiesSnapshot NetMsg 78 + CharacterDataMsg.Position field 7). Additive on the wire (old peers decode existing messages fine) but BEHAVIORALLY additive: a peer without the fixes silently diverges on every reconnect (item identity, position, trap/opened snapshots), so mixed-version sessions are refused by the version gate instead of drifting
}
