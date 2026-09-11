using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Writes a RESTORED cut into the live world at the first moment the world can
/// take it. A restore does not hand the world back to the game that wrote it:
/// the kernel restores the run baseline at the Continue click, the game
/// regenerates that layer from it, and then every in-layer fact has to be written
/// onto the fresh copy — the block diff, the partial block damage, the decided
/// keypad codes and geyser liquid types, and the radiation line.
///
/// The values arrive from two owners. The Runtime holds the block diff and the
/// radiation line in the tables a late joiner would have received, and reports a
/// pending live-world replay (<see cref="IWorldFactSource.HasPendingLiveReplay"/>)
/// because those tables must survive the world-entry reset. The adapter holds the
/// native values (keypad codes, geyser liquid types and the GAME's own
/// partial-damage list, which has no Runtime table at all) because no Runtime
/// table can express them.
///
/// The order is load-bearing: the block diff lands before the partial damage that
/// survives only on top of it, the decided values land after both, and the whole
/// replay must run BEFORE the world-entry keypad broadcast so the peers receive
/// the restored codes.
///
/// The handover is a READ-THEN-COMMIT, never a take: the replay writes first and
/// commits only when the live world took every value. A generation that cannot
/// take them (the world vanished between the readiness check and the write, a
/// bounded table refused a row, a keypad's Openable is not there) reports the loss
/// at error level and RELEASES both halves — the entry seam runs once per
/// generation, and any later generation is a DIFFERENT layer, so an armed "retry"
/// would only risk writing this layer's rows into the next one.
/// </summary>
internal sealed class RestoredWorldFactReplay(
	IWorldFactSource facts,
	INativeWorldFacts? nativeFacts,
	IRestoredWorldFactSink sink,
	ILogger<RestoredWorldFactReplay> log,
	WorldRestoreAudit? audit = null)
{
	private readonly IWorldFactSource _facts = facts;
	private readonly INativeWorldFacts? _nativeFacts = nativeFacts;
	private readonly IRestoredWorldFactSink _sink = sink;
	private readonly ILogger<RestoredWorldFactReplay> _log = log;
	private readonly WorldRestoreAudit? _audit = audit;

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

		// 2. The partial damage. The game's list is the ONLY partial-damage table
		// there is (the CUO registry that used to sit beside it was deleted), so the
		// cut's damage rows ARE its rows — no routing decision, and no second set
		// whose cap could refuse them.
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

		// The LIVE-WRITE account, in the words the restore report uses. The Continue
		// click returned long before this seam ran, so this is the only way the
		// caller that started the restore learns the live world did not take a row
		// (§6's "every dropped entry is surfaced" applies to the native half too).
		var refusedDetail = new List<string>();
		if (blocks.Refused > 0)
		{
			refusedDetail.Add($"{blocks.Refused} block-state row(s)");
		}

		if (damages.Refused > 0)
		{
			refusedDetail.Add($"{damages.Refused} partial-damage row(s)");
		}

		if (keypads.Refused > 0)
		{
			refusedDetail.Add($"{keypads.Refused} keypad code(s)");
		}

		if (geysers.Refused > 0)
		{
			refusedDetail.Add($"{geysers.Refused} geyser type(s)");
		}

		if (radiation is not null && !radiationApplied)
		{
			refusedDetail.Add("the radiation line");
		}

		_audit?.LiveWriteFinished(
			liveWorldComplete,
			refusedDetail,
			liveWorldComplete
				? $"the live world took every restored fact ({blocks.Applied} block-state, {damages.Applied} partial-damage, {keypads.Applied} keypad, {geysers.Applied} geyser row(s))"
				: $"the live world did not take {string.Join(", ", refusedDetail)}");

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
			liveWorldComplete ? "complete" : "INCOMPLETE (reported at error level, not retried)");

		if (!liveWorldComplete)
		{
			// A restored fact the live world did not take is lost state, not a
			// detail — and there is no retry path to leave armed: the world-entry
			// seam runs once per generation, and every later generation belongs to a
			// different layer (whose boundary cancels a handover anyway). So the loss
			// is named here and BOTH handovers are released; keeping them would only
			// risk replaying this layer's rows into another one.
			_nativeFacts?.CancelPendingRestore();
			_facts.ClearPendingLiveReplay();
			_log.LogError(
				"[SaveFacts] the live world did NOT take every restored fact ({Blocks} block-state, {Damages} partial-damage, {Keypads} keypad and {Geysers} geyser row(s) refused, radiation {Radiation}) — the restored state is INCOMPLETE and those rows are NOT in the live world.",
				blocks.Refused, damages.Refused, keypads.Refused, geysers.Refused,
				radiation is null ? "absent" : radiationApplied ? "applied" : "no live line");
		}
	}
}
