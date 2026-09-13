using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Writes a RESTORED cut into the live world at the first moment the world can
/// take it. A restore does not hand the world back to the game that wrote it:
/// the kernel restores the run baseline at the Continue click, the game
/// regenerates that layer from it, and then every in-layer fact has to be written
/// onto the fresh copy — the block diff, the partial block damage, the decided
/// keypad codes and geyser liquid types, the radiation line, and the per-entity
/// world facts (consumed traps, opened lockables, building health).
///
/// The values this replay WRITES arrive from three owners — the gate below reads a
/// fourth, the item half, which this replay never writes (the generation reconcile
/// owns it) but must not let the layer-boundary reset erase. The Runtime holds the
/// block diff and the
/// radiation line in the tables a late joiner would have received, and reports a
/// pending live-world replay (<see cref="IWorldFactSource.HasPendingLiveReplay"/>)
/// because those tables must survive the world-entry reset. The adapter holds the
/// native values (keypad codes, geyser liquid types and the GAME's own
/// partial-damage list, which has no Runtime table at all) because no Runtime
/// table can express them. And the kernel holds the per-entity facts the restore
/// put back, deferred by <see cref="WorldEntityKernelProjection"/> for exactly
/// this seam (<see cref="IRestoredWorldEntitySource"/>): a guest projects them
/// immediately, because its live world already IS the restored layer, while the
/// host/solo side has only the layer being replaced alive at the click.
///
/// The order is load-bearing where a fact can only survive on top of another: the
/// block diff lands before the partial damage that survives only on top of it, the
/// decided values land after both, the entity facts land in the world their cells
/// describe, and the whole replay must run BEFORE the world-entry keypad broadcast
/// so the peers receive the restored codes.
///
/// The handover is a READ-THEN-COMMIT, never a take: the replay writes first and
/// commits only when the live world took every value. A generation that cannot
/// take them (the world vanished between the readiness check and the write, a
/// bounded table refused a row, a keypad's Openable is not there, an entity the
/// regenerated layer does not have) reports the loss at error level and RELEASES
/// every half — the entry seam runs once per generation, and any later generation
/// is a DIFFERENT layer, so an armed "retry" would only risk writing this layer's
/// rows into the next one.
///
/// Each half reports to <see cref="WorldRestoreAudit"/> on its own, because each
/// one can land or be refused independently: a refused entity row is not evidence
/// that the block diff did not land, and the restore's account must be complete
/// either way.
/// </summary>
internal sealed class RestoredWorldFactReplay(
	IWorldFactSource facts,
	INativeWorldFacts? nativeFacts,
	IRestoredWorldFactSink sink,
	ILogger<RestoredWorldFactReplay> log,
	WorldRestoreAudit? audit = null,
	IRestoredWorldEntitySource? worldEntities = null,
	IRestoredWorldItemSource? restoredWorldItems = null)
{
	private readonly IWorldFactSource _facts = facts;
	private readonly INativeWorldFacts? _nativeFacts = nativeFacts;
	private readonly IRestoredWorldFactSink _sink = sink;
	private readonly ILogger<RestoredWorldFactReplay> _log = log;
	private readonly WorldRestoreAudit? _audit = audit;
	private readonly IRestoredWorldEntitySource? _worldEntities = worldEntities;
	private readonly IRestoredWorldItemSource? _restoredWorldItems = restoredWorldItems;

	/// <summary>
	/// Host: a restored cut is waiting for the world-entry seam — the Runtime's
	/// world-fact tables hold it, the adapter's native handover does, the kernel's
	/// restored world-entity facts do, the item domain's restored world items do, or
	/// any combination of the four.
	///
	/// The world-entry hook asks BEFORE it decides whether to run the
	/// layer-boundary reset. The item half is the one that is easy to miss: it is
	/// written by the generation reconcile rather than by this replay, and it can be
	/// the ONLY half still owed — the reset drops every world-rooted row, which is
	/// exactly the set the reconcile exists to materialize.
	/// </summary>
	internal bool HasPending =>
		_facts.HasPendingLiveReplay
		|| (_nativeFacts?.HasPendingRestore ?? false)
		|| (_worldEntities?.HasPendingRestore ?? false)
		|| (_restoredWorldItems?.RestoredWorldItemsPending ?? false);

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
		var entitiesPending = _worldEntities?.HasPendingRestore ?? false;

		// The ATTEMPT each arm belongs to, captured BEFORE anything is written or
		// committed: a contribution is attributed to the restore that armed the data it
		// describes, never to whatever account happens to be open when it arrives. The
		// world-fact half (the Runtime tables and the adapter's native handover) is one
		// attempt's work — WorldFactRestore applies both in one call — so it carries the
		// sequence the tables were applied with, pending or not, and the world-entity
		// half carries its own arm's.
		var factSequence = _facts.AppliedRestoreSequence;
		var entitySequence = entitiesPending ? _worldEntities!.PendingRestoreSequence : 0;
		if (!runtimePending && !nativePending && !entitiesPending)
		{
			// A restored cut that carried no live-world fact at all (a layer-end cut)
			// has nothing to write. The world-entry seam is where the restore's audit
			// learns that, instead of waiting for a write that will never come — but
			// ONLY a restore that is actually waiting: this method runs once per
			// generation, and a normal generation after a completed restore must report
			// nothing, or the player would be told about a restore that already
			// finished.
			if (_audit is { AwaitingLiveWrite: true })
			{
				_audit.LiveWriteFinished(factSequence, complete: true, refused: [], summary: "the restored cut carried no live-world fact to write");
			}

			return;
		}

		// READ, do not take: only a replay whose every row landed commits.
		var restore = _nativeFacts?.ReadPendingRestore() ?? NativeWorldFactRestore.Empty;
		LiveWorldWriteOutcome blocks;
		LiveWorldWriteOutcome damages;
		LiveWorldWriteOutcome keypads;
		LiveWorldWriteOutcome geysers;
		LiveWorldWriteOutcome recipes;
		LiveWorldWriteOutcome? entities = null;
		RadiationLineStateMsg? radiation;
		bool radiationApplied;
		try
		{
			// 1. The block diff. The baseline capture already ran, so these writes are
			// exactly the deviations from the layer the game just generated.
			var blockStates = _facts.CaptureBlockStates();
			blocks = _sink.WriteBlockStates(blockStates);

			// 2. The partial damage. The game's list is the ONLY partial-damage table
			// there is (the CUO registry that used to sit beside it was deleted), so the
			// cut's damage rows ARE its rows — no routing decision, and no second set
			// whose cap could refuse them.
			damages = _sink.ReplaceGameBlockDamages(restore.BlockDamages);

			// 3. The decided native values the game would otherwise have re-rolled.
			keypads = _sink.ApplyKeypadCodes(restore.Keypads);
			geysers = _sink.ApplyGeysers(restore.Geysers);

			// 4. The recipe unlock table. It belongs to this seam, not to the native
			// save slot: the game rebuilds Recipes.recipes in WorldGeneration.Awake and
			// CUO's mod-content provider appends the custom recipes on a later Update
			// frame, so an early write would see a table missing every custom recipe.
			recipes = _sink.ApplyRecipeUnlocks(restore.Recipes);

			// 5. The radiation line: the regenerated line is inactive, and the next
			// publish would silently overwrite a restored value with it.
			radiation = _facts.CaptureRadiationLine();
			radiationApplied = radiation is not null && _sink.ApplyRadiationLine(radiation);

			// 6. The per-entity facts. They land last because they name objects that
			// have to stand in a world whose cells are already the restored ones.
			if (entitiesPending)
			{
				entities = _sink.ApplyWorldEntities(_worldEntities!.ReadPendingFacts());
			}
		}
		catch (Exception ex)
		{
			// An engine call threw: nothing about this replay can be trusted — not the
			// rows written before the throw, not the ones after it. "The write threw" is
			// the verdict the restore report carries AND the reason EVERY handover is
			// released here: keeping one would replay this layer's rows into the next
			// generation, which is the one thing the replay's read-then-commit exists to
			// prevent. Every half that was owed reports, so the audit never waits for a
			// contribution that this path just made impossible.
			_audit?.LiveWriteFinished(
				factSequence,
				complete: false,
				refused: [$"the live-world write threw ({ex.Message})"],
				summary: $"the live-world write threw: {ex.Message}");
			if (entitiesPending)
			{
				_audit?.LiveWriteFinished(
					entitySequence,
					complete: false,
					refused: ["the world-entity facts were not written (the live-world write threw)"],
					summary: "the restored world-entity facts were not written: the live-world write threw");
			}

			_nativeFacts?.CancelPendingRestore();
			_facts.ClearPendingLiveReplay();
			_worldEntities?.CancelPendingRestore($"the live-world write threw ({ex.Message})");
			_log.LogError(ex, "[SaveFacts] the live-world write threw — the restored state is INCOMPLETE and every handover is released.");
			return;
		}

		var refused = blocks.Refused + damages.Refused + keypads.Refused + geysers.Refused + recipes.Refused;
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

		if (recipes.Refused > 0)
		{
			refusedDetail.Add($"{recipes.Refused} recipe unlock row(s)");
		}

		if (radiation is not null && !radiationApplied)
		{
			refusedDetail.Add("the radiation line");
		}

		_audit?.LiveWriteFinished(
			factSequence,
			liveWorldComplete,
			refusedDetail,
			liveWorldComplete
				? $"the live world took every restored fact ({blocks.Applied} block-state, {damages.Applied} partial-damage, {keypads.Applied} keypad, {geysers.Applied} geyser, {recipes.Applied} recipe unlock row(s))"
				: $"the live world did not take {string.Join(", ", refusedDetail)}");

		if (liveWorldComplete)
		{
			// Every value is in the live world: end both handovers of this half.
			_nativeFacts?.CommitPendingRestore();
			_facts.ClearPendingLiveReplay();
		}
		else
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
				"[SaveFacts] the live world did NOT take every restored fact ({Blocks} block-state, {Damages} partial-damage, {Keypads} keypad, {Geysers} geyser and {Recipes} recipe unlock row(s) refused, radiation {Radiation}) — the restored state is INCOMPLETE and those rows are NOT in the live world.",
				blocks.Refused, damages.Refused, keypads.Refused, geysers.Refused, recipes.Refused,
				radiation is null ? "absent" : radiationApplied ? "applied" : "no live line");
		}

		if (entities is { } entityWrite)
		{
			// The world-entity half is accounted on its own: it is the one half whose
			// rows can be refused individually (the regenerated layer is expected to
			// hold the identical entity at the identical position, so a missing one is
			// divergence), and a refusal there says nothing about the rows above.
			var entitiesComplete = entityWrite.Refused == 0;
			_audit?.LiveWriteFinished(
				entitySequence,
				entitiesComplete,
				entitiesComplete ? [] : [$"{entityWrite.Refused} world-entity row(s)"],
				entitiesComplete
					? $"the live world took every restored world-entity fact ({entityWrite.Applied} row(s))"
					: $"the live world did not take {entityWrite.Refused} world-entity row(s)");

			if (entitiesComplete)
			{
				_worldEntities?.CommitPendingRestore();
			}
			else
			{
				_worldEntities?.CancelPendingRestore("the regenerated layer has no entity at this cut's position");
				_log.LogError(
					"[SaveFacts] the live world did NOT take {Refused} of {Rows} restored world-entity row(s) — the regenerated layer has no such entity where the cut recorded it, and those facts are NOT in the live world.",
					entityWrite.Refused, entityWrite.Applied + entityWrite.Refused);
			}
		}

		_log.LogInformation(
			"[SaveFacts] restored the live world: {Blocks} block-state row(s) written, {Damages} partial-damage row(s) applied, {Keypads} keypad code(s) applied (the archive carried {KeypadTotal}), {Geysers} geyser type(s) applied (the archive carried {GeyserTotal}), {Recipes} recipe unlock row(s) applied (the archive carried {RecipeTotal}), radiation line {Radiation}, {Entities} world-entity row(s), {Refused} row(s) not taken, restore {Restore}.",
			blocks.Applied, damages.Applied,
			keypads.Applied, restore.Keypads.Count, geysers.Applied, restore.Geysers.Count,
			recipes.Applied, restore.Recipes.Count,
			radiation is null ? "absent" : radiationApplied ? "applied" : "no live line",
			entities is { } written ? $"{written.Applied} applied / {written.Refused} refused" : "not carried",
			refused + (entities?.Refused ?? 0),
			liveWorldComplete && (entities?.Refused ?? 0) == 0 ? "complete" : "INCOMPLETE (reported at error level, not retried)");
	}
}
