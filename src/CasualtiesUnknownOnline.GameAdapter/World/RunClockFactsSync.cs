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
/// The write never moves either value backwards — see
/// <see cref="INativeWorldFacts.ApplyRunFacts"/> — so a late duplicate (the 60 s repair
/// group re-sends the same absolute value) is harmless by construction rather than by
/// timing.
/// </summary>
internal sealed class RunClockFactsSync(
	IWorldControl world,
	INativeWorldFacts nativeFacts,
	ILogger<RunClockFactsSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly INativeWorldFacts _nativeFacts = nativeFacts;
	private readonly ILogger<RunClockFactsSync> _log = log;

	/// <summary>The last facts this side applied, re-published at the world-entry edge so a member that entered the world afterwards is still sent them.</summary>
	private RunClockFacts? _applied;

	internal void BindToSession() => _world.RunFactsReceived += OnRunFactsReceived;

	internal void Unbind()
	{
		_world.RunFactsReceived -= OnRunFactsReceived;
		_applied = null;
	}

	/// <summary>
	/// A generation boundary is starting a new world: the per-world write marker is
	/// re-armed, the value captured from the world that is ending becomes this side's own
	/// base, and it is published so members get it with the entry and repair groups. On the
	/// host this is also what writes the clock, so neither side depends on the other for
	/// its own clock.
	/// </summary>
	internal void SettleAtGenerationBoundary()
	{
		_nativeFacts.SettleRunClockFacts();
		_applied = _nativeFacts.CaptureRunClockFacts();
		_world.PublishRunFacts(_applied.Value);
		if (_applied.Value.Failure is { } failure)
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

		_applied = _nativeFacts.CaptureRunClockFacts();
		_world.PublishRunFacts(_applied.Value);
	}

	private void Apply(RunFactsMsg facts, float layerTimeSpent, float maxTimePerLayer)
	{
		// The published value stays the host's, not the local write's: a member that enters
		// the world later must be sent the authority's clock, and this side's own world may
		// hold a lower one (it has just been generated).
		_nativeFacts.ApplyRunFacts(new RunClockFacts(facts.RunClockBase, layerTimeSpent, maxTimePerLayer, Failure: null));
		_applied = new RunClockFacts(facts.RunClockBase, facts.LayerTimeSpent, facts.MaxTimePerLayer, Failure: null);
	}
}
