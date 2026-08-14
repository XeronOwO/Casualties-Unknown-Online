using System;

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

internal static class LayerModifierDecide
{
	/// <summary>The snapshot-index apply decision: the disagreement/baseline
	/// flags are diagnostics (the host's snapshot wins either way); the action
	/// is the apply flow.</summary>
	internal static LayerModifierDecision OnSnapshot(
		bool localDecided, int localIndex, byte[]? localEntryState,
		int snapshotIndex, byte[]? snapshotRandomState, int applied, bool generating)
	{
		return new LayerModifierDecision
		{
			Next = snapshotIndex == applied
				? LayerModifierDecision.Action.Drop
				: generating ? LayerModifierDecision.Action.Pending : LayerModifierDecision.Action.Apply,
			IndexDisagrees = localDecided && snapshotIndex != localIndex,
			BaselineDiverged = localEntryState is not null && snapshotRandomState is not null
				&& !localEntryState.AsSpan().SequenceEqual(snapshotRandomState),
		};
	}

	/// <summary>The pump's apply choice once generation finished: the LOCAL
	/// replay wins (its banner was built at generation finish reading the
	/// already-filled prefix — no banner resend); a deferred snapshot applies
	/// only when no local decision exists. Returns null when neither applies
	/// (e.g. the local index already applied).</summary>
	internal static (bool UseLocal, int Index)? NextApply(bool localDecided, int localIndex, int applied, int pendingIndex)
	{
		if (localDecided && localIndex >= 0 && localIndex != applied)
		{
			return (true, localIndex);
		}

		if (pendingIndex >= 0 && pendingIndex != applied)
		{
			return (false, pendingIndex);
		}

		return null;
	}
}
