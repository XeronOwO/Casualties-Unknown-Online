namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The layer-modifier sync's pure decisions — extracted from the adapter's
/// LayerModifierSync so the apply matrix is unit-testable (the domain that
/// burned three live-verification rounds, #90): what a snapshot does when it
/// arrives (apply now / defer to the generation-finished pump / drop as an
/// idempotent repeat) and what the pump applies first (the local replay over
/// a deferred snapshot). Pure — no Unity, no game state.
/// </summary>
internal readonly struct LayerModifierDecision
{
	internal enum Action
	{
		Apply, // apply now (outside a generation, not yet applied)
		Pending, // the world is still generating — defer to the pump
		Drop, // already applied — the snapshot of the layer's own roll or a periodic repeat
	}

	internal Action Next { get; init; }

	internal bool IndexDisagrees { get; init; }

	internal bool BaselineDiverged { get; init; }
}
