using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

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
