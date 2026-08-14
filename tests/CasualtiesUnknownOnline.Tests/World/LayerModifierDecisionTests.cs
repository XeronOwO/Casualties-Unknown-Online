using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The layer-modifier sync's pure decisions (extracted from
/// LayerModifierSync): the snapshot-apply matrix — apply now / defer while
/// generating / drop as an idempotent repeat, with the disagreement and
/// baseline-divergence diagnostics — and the pump's local-replay-wins choice.
/// The domain burned three live-verification rounds (#90); the matrix that
/// burned them is now locked.
/// </summary>
public class LayerModifierDecisionTests
{
	[Fact]
	public void Snapshot_OutsideGeneration_NotApplied_Applies()
	{
		var d = LayerModifierDecide.OnSnapshot(
			localDecided: false, localIndex: -1, localEntryState: null,
			snapshotIndex: 2, snapshotRandomState: null, applied: -1, generating: false);

		Assert.True(d.Next == LayerModifierDecision.Action.Apply, $"a fresh snapshot applies, got {d.Next}");
		Assert.False(d.IndexDisagrees && d.BaselineDiverged, "no local replay — no diagnostics");
	}

	[Fact]
	public void Snapshot_DuringGeneration_DefersToThePump()
	{
		var d = LayerModifierDecide.OnSnapshot(
			localDecided: false, localIndex: -1, localEntryState: null,
			snapshotIndex: 2, snapshotRandomState: null, applied: -1, generating: true);

		Assert.True(d.Next == LayerModifierDecision.Action.Pending,
			$"a mid-generation snapshot defers (Initialize would conflict with the terrain writes), got {d.Next}");
	}

	[Fact]
	public void Snapshot_AlreadyApplied_Drops()
	{
		// The snapshot of the layer's own roll (applied via the local replay)
		// or a periodic repeat — idempotent, nothing runs twice.
		var d = LayerModifierDecide.OnSnapshot(
			localDecided: true, localIndex: 2, localEntryState: null,
			snapshotIndex: 2, snapshotRandomState: null, applied: 2, generating: false);

		Assert.True(d.Next == LayerModifierDecision.Action.Drop, $"an applied index drops, got {d.Next}");
	}

	[Fact]
	public void Snapshot_IndexDisagreesWithTheLocalReplay()
	{
		var d = LayerModifierDecide.OnSnapshot(
			localDecided: true, localIndex: 1, localEntryState: null,
			snapshotIndex: 3, snapshotRandomState: null, applied: -1, generating: false);

		Assert.True(d.IndexDisagrees, "a different snapshot index than the local roll is the disagreement diagnostic");
		Assert.True(d.Next == LayerModifierDecision.Action.Apply, "the host's snapshot wins either way");
	}

	[Fact]
	public void Snapshot_BaselineDiverges_WhenTheSegmentStartsDiffer()
	{
		var local = new byte[] { 1, 2, 3 };
		var host = new byte[] { 1, 2, 4 };
		var same = new byte[] { 1, 2, 3 };

		Assert.True(LayerModifierDecide.OnSnapshot(true, 0, local, 0, host, -1, false).BaselineDiverged,
			"a bit-different decision-entry state is the baseline-divergence diagnostic");
		Assert.False(LayerModifierDecide.OnSnapshot(true, 0, local, 0, same, -1, false).BaselineDiverged,
			"bit-identical entry states agree");
		Assert.False(LayerModifierDecide.OnSnapshot(true, 0, local, 0, null, -1, false).BaselineDiverged,
			"a missing host state cannot diverge (nothing to compare)");
	}

	[Fact]
	public void Pump_LocalReplayWins_OverADeferredSnapshot()
	{
		var next = LayerModifierDecide.NextApply(localDecided: true, localIndex: 1, applied: -1, pendingIndex: 3);
		Assert.True(next is { UseLocal: true, Index: 1 },
			"the local replay applies first (its banner was built at generation finish — no resend)");
	}

	[Fact]
	public void Pump_DeferredSnapshotApplies_WithoutALocalDecision()
	{
		var next = LayerModifierDecide.NextApply(localDecided: false, localIndex: -1, applied: -1, pendingIndex: 3);
		Assert.True(next is { UseLocal: false, Index: 3 }, "the deferred snapshot applies when no local decision exists");
	}

	[Fact]
	public void Pump_AlreadyApplied_AppliesNothing()
	{
		Assert.Null(LayerModifierDecide.NextApply(localDecided: true, localIndex: 1, applied: 1, pendingIndex: -1));
		Assert.Null(LayerModifierDecide.NextApply(localDecided: false, localIndex: -1, applied: 3, pendingIndex: 3));
		Assert.Null(LayerModifierDecide.NextApply(localDecided: true, localIndex: -1, applied: -1, pendingIndex: -1));
	}
}
