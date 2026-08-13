namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 4; // v4: the crafting sync domain (CraftReportMsg/RecipeUnlockMsg, NetMsg 76/77). Additive on the wire (old peers decode existing messages fine) but BEHAVIORALLY additive: a peer without the craft domain silently diverges on every crafting operation, so mixed-version sessions are refused by the version gate instead of drifting
}
