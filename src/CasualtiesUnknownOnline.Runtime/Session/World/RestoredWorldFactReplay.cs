using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Writes a RESTORED cut into the live world at the first moment the world can
/// take it. A restore does not hand the world back to the game that wrote it:
/// the kernel restores the run baseline at the Continue click, the game
/// regenerates that layer from it, and then every in-layer fact has to be written
/// onto the fresh copy — the block diff, the partial damage, the decided keypad
/// codes and geyser liquid types, and the radiation line.
///
/// The values arrive from two owners. The Runtime holds the block diff, CUO's
/// partial damage and the radiation line in the tables a late joiner would have
/// received, and reports a pending live-world replay
/// (<see cref="IWorldFactSource.HasPendingLiveReplay"/>) because those tables must
/// survive the world-entry reset. The adapter holds the native values (keypad
/// codes, geyser liquid types and the game's OWN partial-damage list) because no
/// Runtime table can express them.
///
/// The order is load-bearing: the block diff lands before the partial damage that
/// survives only on top of it, the decided values land after both, and the whole
/// replay must run BEFORE the world-entry keypad broadcast so the peers receive
/// the restored codes.
///
/// The handover is a READ-THEN-COMMIT, never a take: a generation that cannot take
/// every value (the world vanished between the readiness check and the write, a
/// bounded table refused a row, a keypad's Openable is not there) keeps BOTH
/// halves pending and reports an error, so the next generation retries instead of
/// the restore being reported complete over tables the live world does not have.
/// </summary>
internal sealed class RestoredWorldFactReplay(
	IWorldFactSource facts,
	INativeWorldFacts? nativeFacts,
	IRestoredWorldFactSink sink,
	ILogger<RestoredWorldFactReplay> log)
{
	private readonly IWorldFactSource _facts = facts;
	private readonly INativeWorldFacts? _nativeFacts = nativeFacts;
	private readonly IRestoredWorldFactSink _sink = sink;
	private readonly ILogger<RestoredWorldFactReplay> _log = log;

	/// <summary>
	/// Host: a restored cut is waiting for the world-entry seam — the Runtime's
	/// world-fact tables hold it, the adapter's native handover does, or both.
	/// The world-entry hook asks BEFORE it decides whether to run the
	/// layer-boundary reset.
	/// </summary>
	internal bool HasPending => _facts.HasPendingLiveReplay || (_nativeFacts?.HasPendingRestore ?? false);

	/// <summary>
	/// Host: write the pending restored cut into the live world. A no-op when
	/// nothing is pending (every normal generation) or when the world is not ready
	/// yet — the values stay pending in that case, because the entry seam runs once
	/// per generation and a half-generated world would silently drop them.
	/// </summary>
	internal void ApplyIfPending()
	{
		if (!_sink.IsWorldReady)
		{
			return;
		}

		var runtimePending = _facts.HasPendingLiveReplay;
		var nativePending = _nativeFacts?.HasPendingRestore ?? false;
		if (!runtimePending && !nativePending)
		{
			return;
		}

		// READ, do not take: only a replay whose every row landed commits.
		var restore = _nativeFacts?.ReadPendingRestore() ?? NativeWorldFactRestore.Empty;

		// 1. The block diff. The baseline capture already ran, so these writes are
		// exactly the deviations from the layer the game just generated.
		var blockStates = _facts.CaptureBlockStates();
		var blocks = _sink.WriteBlockStates(blockStates);

		// 2. The partial damage. The cut is the whole truth for the game's own
		// list, so the write clears it first.
		//
		// ONLY the game's own rows land here. The cut also carries CUO's registry
		// rows, but those belong to the Runtime table the late-joiner snapshot
		// ships (IWorldFactSource.ApplyFacts put them back): the game's list is
		// bounded at 128 and CUO's registry at 256, so a long run's registry legally
		// holds more cells than the game can — writing them here would fill the list
		// and refuse the game's OWN rows, which are exactly the rows the saved world
		// had.
		var damages = _sink.ReplaceGameBlockDamages(restore.BlockDamages);

		// 3. The decided native values the game would otherwise have re-rolled.
		var keypads = _sink.ApplyKeypadCodes(restore.Keypads);
		var geysers = _sink.ApplyGeysers(restore.Geysers);

		// 4. The radiation line: the regenerated line is inactive, and the next
		// publish would silently overwrite a restored value with it.
		var radiation = _facts.CaptureRadiationLine();
		var radiationApplied = radiation is not null && _sink.ApplyRadiationLine(radiation);

		var refused = blocks.Refused + damages.Refused + keypads.Refused + geysers.Refused;
		var liveWorldComplete = refused == 0 && (radiation is null || radiationApplied);

		if (liveWorldComplete)
		{
			// Every value is in the live world: end both handovers.
			_nativeFacts?.CommitPendingRestore();
			_facts.ClearPendingLiveReplay();
		}

		_log.LogInformation(
			"[SaveFacts] restored the live world: {Blocks} block-state row(s) written, {Damages} partial-damage row(s) applied, {Keypads} keypad code(s) applied (the archive carried {KeypadTotal}), {Geysers} geyser type(s) applied (the archive carried {GeyserTotal}), radiation line {Radiation}, {Refused} row(s) not taken, restore {Restore}.",
			blocks.Applied, damages.Applied,
			keypads.Applied, restore.Keypads.Count, geysers.Applied, restore.Geysers.Count,
			radiation is null ? "absent" : radiationApplied ? "applied" : "no live line",
			refused,
			liveWorldComplete ? "complete" : "STILL PENDING (the next generation retries it)");

		if (!liveWorldComplete)
		{
			// A restored fact the live world did not take is lost state, not a
			// detail: the cut named it, the world does not have it, and BOTH handovers
			// are deliberately left pending so the next generation tries again.
			_log.LogError(
				"[SaveFacts] the live world did NOT take every restored fact ({Blocks} block-state, {Damages} partial-damage, {Keypads} keypad and {Geysers} geyser row(s) refused, radiation {Radiation}) — the restore stays PENDING for the next generation.",
				blocks.Refused, damages.Refused, keypads.Refused, geysers.Refused,
				radiation is null ? "absent" : radiationApplied ? "applied" : "no live line");
		}
	}
}
