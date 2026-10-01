using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The run clock base and the layer's radiation-timer accounting — the two clocks the
/// game keeps per PROCESS (<c>SaveSystem.savedRunTime</c> is a static, and
/// <c>WorldGeneration.world.layerTimeSpent</c> belongs to the generated world) and that
/// the kernel's run baseline deliberately does not hold: nothing about a layer's shape is
/// generated from a clock, so the run baseline is not their carrier.
///
/// The two halves run at the one instant that owns them both, the generation boundary:
/// the boundary's snapshot of <c>savedRunTime + realTimeElapsed</c> IS the base the new
/// scene derives from, so it is captured here and published for the members; and a member
/// that followed the run receives the host's value and applies it when the live world can
/// take it (the native save slot, before <c>WorldGeneration.Start</c> derives the layer's
/// time limit from it).
///
/// What a MEMBER is sent is re-read at the SEND point: the fan-out's entry and 60 s repair
/// groups ask <see cref="INativeWorldFacts.CaptureRunClockFacts"/> through the message
/// service, so a member that enters mid-run gets the host's total at its own entry instead
/// of the last boundary capture (batch 20261001-o row 1). The receiving side maps that
/// total onto its own world's epoch (<c>total - world.realTimeElapsed</c>) when it writes it
/// — see <see cref="INativeWorldFacts.ApplyRunFacts"/> — so the boundary capture below is
/// this SIDE's own base, never what the next member is told.
///
/// The layer TIMER takes one seam longer than the clock: the game zeroes it while the
/// generation finishes (<c>WorldGeneration.FinishWorldGeneration</c> sets
/// <c>layerTimeSpent = 0</c> on its first line, and that runs after the native save slot),
/// so it lands at the first moment a live, non-generating world can take it — the
/// world-entry edge for a value that arrived before the world was ready, or the apply of
/// the message itself for a member already in the world (the 60 s repair). A timer
/// written at the save slot is erased and the continued layer restarts its radiation
/// countdown (batch 20261001-m Run A, row 1).
///
/// The write never moves either value backwards — see
/// <see cref="INativeWorldFacts.ApplyRunFacts"/> — so a late duplicate (the 60 s repair
/// group re-sending an absolute total, which maps back onto the same world base) is
/// harmless up to the two sends' transport jitter rather than a jump of the interval.
/// </summary>
internal sealed class RunClockFactsSync(
	IWorldControl world,
	INativeWorldFacts nativeFacts,
	ILogger<RunClockFactsSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly INativeWorldFacts _nativeFacts = nativeFacts;
	private readonly ILogger<RunClockFactsSync> _log = log;

	internal void BindToSession() => _world.RunFactsReceived += OnRunFactsReceived;

	internal void Unbind() => _world.RunFactsReceived -= OnRunFactsReceived;

	/// <summary>
	/// A generation boundary is starting a new world: the per-world write marker is
	/// re-armed and the value captured from the world that is ending becomes this side's own
	/// base (on the host this is also what writes the clock, so neither side depends on the
	/// other for its own clock). It is published as this side's settled value; a member's
	/// entry/repair group re-reads the live world at its own send point instead of carrying
	/// this capture.
	/// </summary>
	internal void SettleAtGenerationBoundary()
	{
		_nativeFacts.SettleRunClockFacts();
		var captured = _nativeFacts.CaptureRunClockFacts();
		_world.PublishRunFacts(captured);
		if (captured.Failure is { } failure)
		{
			_log.LogWarning("[RunFacts] the generation boundary could not read the run clock ({Failure}); members keep their own clock and layer timer.", failure);
		}
	}

	/// <summary>
	/// Guest: the host's clocks. Applied to the live world when it exists, otherwise held for
	/// the world-entry seam. The LAYER timer and its limit travel together or not at all: a
	/// pair whose stamp names another layer is dropped as one value, while the run clock (a
	/// run-scoped total) is still offered to the write guard.
	/// </summary>
	internal void OnRunFactsReceived(RunFactsMsg facts, bool layerTimerApplies) =>
		Apply(facts, layerTimerApplies ? facts.LayerTimeSpent : -1f, layerTimerApplies ? facts.MaxTimePerLayer : -1f);

	/// <summary>
	/// The world-entry edge: write anything the received clocks left waiting, then publish
	/// this side's own (now settled) values. Runs on every side, host and guest alike — a
	/// side that followed the run has the host's values by now, so what it publishes is the
	/// same run's clock, and a side that hosted it publishes what the boundary captured.
	/// </summary>
	internal void PublishAtWorldEntry()
	{
		if (!_nativeFacts.TryWritePendingRunFields())
		{
			return;
		}

		// The layer timer lands at THIS seam, not at the save slot: the caller reaches it
		// only while the game reports a live, non-generating world, and that edge is the
		// first write that survives the generation coroutine's own
		// `layerTimeSpent = 0` (WorldGeneration.cs:3609). See INativeWorldFacts.
		_nativeFacts.TryWritePendingLayerTimer();

		_world.PublishRunFacts(_nativeFacts.CaptureRunClockFacts());
	}

	private void Apply(RunFactsMsg facts, float layerTimeSpent, float maxTimePerLayer) =>
		// The value handed to the world is the peer's TOTAL: the native write maps it onto
		// this world's epoch (see INativeWorldFacts.ApplyRunFacts), and what a member is
		// sent is re-read at that send point rather than kept here.
		_nativeFacts.ApplyRunFacts(new RunClockFacts(facts.RunClockBase, layerTimeSpent, maxTimePerLayer, Failure: null));
}
