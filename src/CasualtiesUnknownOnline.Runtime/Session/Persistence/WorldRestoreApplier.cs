using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The restore half of the save system: opening ONE archive and producing the run it
/// describes — the kernel checkpoint, the world facts, and the characters each side
/// claims. It is split out of <see cref="WorldSaveService"/>, which owns the CUT half
/// (the trigger lifecycle and the world this session writes into); the two meet in
/// exactly one place, the identity a restore produced, which the service adopts as its
/// write target.
///
/// It also owns the two decisions only a restore can make:
///
/// - **A layer-end cut's character positions are not applied.** That snapshot names
///   the layer being ENTERED and the restore regenerates it, so a position captured
///   while the body still stood in the layer being left does not describe the world
///   the restore builds (the native save carries no position at all, and the game
///   places the body itself — <c>WorldGeneration.WorldPlacePlayer</c>). A mid-run cut
///   names the layer its bodies stood in, so there the position IS restored.
/// - **Which character the LOCAL player got.** It is the only one this process can put
///   on a body, and it comes back with the outcome because the body does not exist yet
///   (the scene loads after the click). The character-table slot it is also bound into
///   cannot carry that meaning: the same slot holds the live 1 Hz snapshot.
///
/// It never decides WHETHER a continue may happen — the caller resolves the target
/// world (<see cref="WorldSaveService.ContinueWorldId"/>) and owns that rule.
/// </summary>
internal sealed class WorldRestoreApplier(
	WorldRepository? repository,
	ItemKernelAuthority kernel,
	ICharacterDataControl characters,
	IWorldFactSource worldFacts,
	INativeWorldFacts? nativeWorldFacts,
	WorldCharacterBinder binder,
	IItemControl? items,
	WorldRestoreAudit? audit,
	Func<ulong> layerActor,
	ILoggerFactory loggerFactory,
	ILogger<WorldRestoreApplier> log,
	IRestoredWorldEntitySource? worldEntities = null)
{
	/// <summary>The world-fact half: which restored row goes back to which table, and what could not be put back.</summary>
	private readonly WorldFactRestore _factRestore = new(worldFacts, nativeWorldFacts, loggerFactory.CreateLogger<WorldFactRestore>());

	/// <summary>The item half: a mid-run cut's world items are reconciled against the regenerated layer (GeneratedItemAuthority); a layer-end cut's rows are dropped.</summary>
	private readonly IItemControl? _items = items;

	/// <summary>The world-entity half: the kernel's restored per-entity facts are written at the world-entry seam; a layer-end cut's rows are dropped here.</summary>
	private readonly IRestoredWorldEntitySource? _worldEntities = worldEntities;

	/// <summary>
	/// The retry a decode-level refusal gets (§6): its own object, because walking candidate
	/// archives and promoting one is not what this class is about — the applier APPLIES the
	/// snapshot the recovery settled on.
	/// </summary>
	private readonly WorldRestoreRecovery _recovery = new(repository, loggerFactory, loggerFactory.CreateLogger<WorldRestoreRecovery>());

	/// <summary>
	/// Reads the LOCAL peer a host-local kernel reset runs as (see
	/// <see cref="DropReplacedLayerKernelTables"/>). A delegate rather than a captured
	/// value because the session identity is not final when the composition root is
	/// built — the Steam id is 0 until Steam initializes and this applier outlives that
	/// moment — and it is not a session handle: the reset needs the actor, and this type
	/// must stay constructible without a session at all.
	/// </summary>
	private readonly Func<ulong> _layerActor = layerActor;

	/// <summary>
	/// What one applied archive produced. The identity fields are meaningful only
	/// when <see cref="Started"/> is true: a refusal applied nothing and must not
	/// move the session's write target.
	///
	/// <see cref="Damage"/> is the attempt's itemized account, one line per loss:
	/// a skipped entry with the content id it named, a stored character no present
	/// player could be given, a native field the local body cannot take, a backup
	/// the load fell back to. It travels BESIDE the outcome because the outcome's
	/// summary is one joined line — §6 requires the ids to be readable in-game, and
	/// only the itemized form carries them. A refusal fills it too: what was wrong
	/// with a refused archive is exactly what its player needs to read.
	/// </summary>
	internal readonly record struct Result(
		WorldContinueOutcome Outcome,
		string WorldId,
		string DisplayName,
		IReadOnlyList<SavedCharacter> Characters,
		IReadOnlyList<string> Damage)
	{
		internal bool Started => Outcome.Started;

		/// <summary>
		/// This attempt as the player-facing account (see <see cref="WorldRestoreReport"/>).
		/// The disposition IS <see cref="Started"/> — a refusal applied nothing, so the run
		/// must not start — and it is derived HERE rather than re-decided by the caller that
		/// raises the report: the account and the verdict are then one fact, not two that
		/// can disagree.
		/// </summary>
		internal WorldRestoreReport Report => new(
			Outcome.WorldId,
			Started ? WorldRestoreReport.Disposition.Applied : WorldRestoreReport.Disposition.Refused,
			Outcome.Summary,
			Damage);
	}

	/// <summary>
	/// How many live-world halves this restore owes, counted from the writers that
	/// are ACTUALLY armed rather than from the cut kind. The audit raises a restore's
	/// report only when the LAST one has arrived, so a count that names a half nobody
	/// will report leaves the restore awaiting forever, and a count that is too low
	/// raises the report before the last writer ran (then raises a second one when it
	/// does).
	///
	/// The world-fact half always reports: the replay at the world-entry seam reports
	/// it even when the cut carried no fact at all ("carried nothing to write"). A
	/// writer that is absent from this composition (`IItemControl` /
	/// `IRestoredWorldEntitySource` are optional by design) or whose arm a layer-end
	/// cut just dropped is not owed, which is exactly what the two flags say.
	/// </summary>
	internal static int LiveWorldHalves(bool worldEntityHalfArmed, bool itemReconcileArmed) =>
		1 + (worldEntityHalfArmed ? 1 : 0) + (itemReconcileArmed ? 1 : 0);

	/// <summary>
	/// Applies the archive the caller resolved. <paramref name="worldId"/> is null when
	/// no CUO world is openable at all, which is a refusal like any other (decision 165:
	/// the native regenerate path is never the fallback).
	/// </summary>
	internal Result TryApply(string? worldId)
	{
		// A new attempt SUPERSEDES the previous one, whose arms are all released below —
		// nothing of the attempt that did not apply may survive into the generation this
		// one arms.
		if (repository is null)
		{
			return Refuse(string.Empty, "this composition root has no world repository");
		}

		if (worldId is null)
		{
			return Refuse(string.Empty, "no CUO world exists to continue");
		}

		// The restore is about to APPLY the payload, so it verifies the manifest's
		// digests — a listing would not (WorldLoadOptions).
		var options = new WorldLoadOptions { VerifyChecksums = true, RepairMode = true };

		// The world folder has ONE writer, and a restore is a write path (§5's lease):
		// another CUO instance playing this world must be named HERE, before a checkpoint
		// is applied — not discovered later, when this session's first cut is refused and
		// the player has already been playing a world that will never be saved.
		if (!repository.TryHoldWorld(worldId, out var leaseRefusal))
		{
			log.LogError("Continue refused for world {WorldId}: {Refusal}", worldId, leaseRefusal);
			return Refuse(worldId, leaseRefusal);
		}

		var load = repository.LoadSnapshot(worldId, options);
		if (load.Content is null)
		{
			log.LogError("Continue refused for world {WorldId}: {Summary}", worldId, load.Summary);
			return Refuse(worldId, load.Summary, new SalvageResult(load.Report));
		}

		var decoder = new WorldSnapshotDecoder(load.Content.Manifest, loggerFactory.CreateLogger<WorldSnapshotDecoder>());
		var (_, salvage) = repository.ReadSalvage(load, decoder.DecodeEntry, options);
		var decode = decoder.Finish();
		var recoveryAccount = new List<string>();
		if (decode.Checkpoint is null)
		{
			// A decode-level refusal is the one refusal the reader's fallback cannot see: the
			// manifest WAS read, so the load returned, and the contradiction only shows once
			// the payload is decoded. This is the retry §6 owes it (S3 scope 7), and it
			// PROMOTES the backup it recovered from: the refused snapshots stay as evidence
			// and the pre-restore copy is archived, because a restore read out of an archive
			// that never becomes live is undone by the very next cut.
			var recovered = _recovery.TryRecover(worldId, options, load, decode.Refusal ?? "the snapshot was refused");
			if (recovered is null)
			{
				var refusal = $"{decode.Refusal}; {salvage.Report.Describe()}";
				log.LogError("Continue refused for world {WorldId}: {Refusal}", worldId, refusal);
				return Refuse(worldId, refusal, salvage);
			}

			load = recovered.Load;
			decode = recovered.Decode;
			salvage = recovered.Salvage;
			recoveryAccount.AddRange(recovered.Account);
		}

		// The two facts every step below needs, made local: a snapshot was OPENED and it
		// DECODED. The recovery returns exactly that pair and refuses otherwise, but the
		// invariant belongs here — every use below reads the snapshot's manifest and the
		// checkpoint's run baseline.
		if (load.Content is not { } content || decode.Checkpoint is not { } checkpoint)
		{
			var guard = $"{decode.Refusal}; {salvage.Report.Describe()}";
			log.LogError("Continue refused for world {WorldId}: {Guard}", worldId, guard);
			return Refuse(worldId, guard, salvage);
		}

		// A new restore SUPERSEDES the previous attempt: its account is closed (a restore
		// that never reached its seam is not one that succeeded) and every handover it
		// left armed is released — BEFORE the kernel restore below arms this attempt's
		// own halves, so that NOTHING is armed while this attempt's arms are created.
		//
		// This is not tidiness. The world-entry seam and the generation reconcile act on
		// PRESENCE (they write whatever is armed), and this method's expectation counts
		// the writers that are armed, while a contribution is attributed by the STAMP its
		// arm carries. An arm a dead attempt left behind has to go for both reasons: kept,
		// its rows would be written into THIS layer, and the half would be counted as one
		// this restore owes while its identity can never match the new account's — leaving
		// that account awaiting a contribution the audit refuses, forever, which is the
		// worst outcome this accounting has. It is also why an arm an attempt does not
		// carry (a cut whose checkpoint projects no world-entity fact, a cut with no native
		// values) must not silently keep its predecessor's.
		//
		// The Runtime world-fact marker goes with them even though ApplyFacts would replace
		// it: that replacement needs the restore to APPLY, and the refusal paths below must
		// not leave a dead attempt's "the live world still owes these facts" marker behind
		// for the next generation to act on.
		audit?.AbandonRestore();
		worldFacts.ClearPendingLiveReplay();
		_worldEntities?.CancelPendingRestore("a new restore superseded the previous attempt's world-entity facts");
		nativeWorldFacts?.CancelPendingRestore();
		_items?.CancelRestoredWorldItems("a new restore superseded the previous attempt's world items");

		var restored = kernel.Restore(checkpoint);
		if (!restored.Success)
		{
			var refusal = $"the kernel rejected the checkpoint: {restored.Error}";
			log.LogError("Continue refused for world {WorldId}: {Refusal}", worldId, refusal);
			return Refuse(worldId, refusal, salvage);
		}

		// The ATTEMPT's identity: the kernel restore just bumped it, and every arm this
		// restore creates stamped itself with the same value — the world-fact tables
		// and the adapter's native handover below, the restored world-item set and the
		// world-entity facts inside that kernel restore. The account opened at the end
		// of this method is opened for this sequence, so a contribution that reaches it
		// from an EARLIER attempt (a reconcile of the previous generation, a write that
		// outlived the restore it belonged to) is attributed to that attempt instead of
		// standing in for a half this one still owes.
		var restoreSequence = kernel.RestoreSequence;

		// The world facts the kernel does not own come back BEFORE the run's own
		// state is applied, and the interface's contract is absolute: the apply
		// resets the tables first (see IWorldFactSource), so a fact left over from
		// the previous session cannot survive a cut that never named it. A refused
		// snapshot never gets here, so nothing of a refused cut is written.
		var factDamage = _factRestore.Apply(restoreSequence, decode.UsableWorldBlocks, decode.UsableWorldTransients, decode.UsableNativeRunFields);

		if (content.Manifest.Kind == WorldCutKind.LayerEnd)
		{
			// A layer-end cut names the layer being ENTERED, so its world-item rows
			// describe the layer being LEFT: they are not restored — the layer reset
			// drops them with the old scene. Nothing will reconcile them, so the
			// expectation the checkpoint restore armed is dropped BEFORE the audit
			// begins: a cancelled half is not a lost one.
			_items?.CancelRestoredWorldItems("the cut is a layer-end cut: its world items belong to the layer being replaced");
			// The same rule for the per-entity facts (consumed traps, opened lockables,
			// damaged buildings): they describe the layer being replaced, and the layer
			// the restore regenerates starts with every entity untouched. Writing them
			// into it would damage or kill entities at coordinates that belong to another
			// layer's layout, so the pending write is ended here — before the audit
			// begins, exactly like the item half, because a dropped half this cut never
			// owed is not a lost one.
			_worldEntities?.CancelPendingRestore("the cut is a layer-end cut: its world-entity facts describe the layer being replaced");
			DropReplacedLayerKernelTables();
			DropReplacedLayerPositions(decode.UsableCharacters);
		}

		// The archive is authoritative for this world: the in-memory reconnect table
		// is cleared before the archive's characters are bound, so a player the
		// package omits cannot be resurrected from an earlier snapshot of this run,
		// and the table then holds exactly the archive's claims (decisions 162/178).
		characters.ClearSavedCharacters();
		var bound = binder.Apply(decode.UsableCharacters);
		repository.SetLastOpenedWorld(worldId);

		// The live-world half of this restore lands at the world-entry seam, after
		// this call returned. The audit carries that half's outcome back to the
		// caller: a restore is not "successful" until the live world took every row.
		// The count comes from the writers that are armed RIGHT NOW — the layer-end
		// cancels above have already run — see LiveWorldHalves.
		audit?.BeginRestore(
			worldId,
			restoreSequence,
			LiveWorldHalves(
				worldEntityHalfArmed: _worldEntities?.HasPendingRestore ?? false,
				itemReconcileArmed: _items?.RestoredWorldItemsPending ?? false));

		// The summary is the account the caller logs (and S4's surface reads), so it
		// is built from the WHOLE report — a backup fallback is repository-scope
		// damage that a per-entry "clean" check would hide (§6: silent loss is
		// forbidden). The world facts that could not be put back at the click (no
		// native applier, an applier that threw) are part of that account too, and so
		// is every restored character whose native character fields cannot be put
		// back — the live body then keeps the game's defaults for them, which the
		// player would otherwise never be told (§6.1). Only the characters that were
		// actually BOUND are described: a file no present peer claims restores
		// nothing, so naming its gaps would describe a degradation nobody gets.
		var damages = new List<string>(recoveryAccount);
		damages.AddRange(factDamage);
		var boundKeys = new HashSet<string>(bound.BoundPlayerKeys, StringComparer.Ordinal);
		foreach (var character in decode.UsableCharacters)
		{
			if (boundKeys.Contains(character.PlayerKey))
			{
				damages.AddRange(CharacterNativeFieldPolicy.Missing(character.PlayerKey, character.Character));
			}
		}

		// A stored character that was REFUSED a claimant is part of the same account: an
		// ambiguous claim (two present players whose transport-scoped keys collide) and a
		// key-space mismatch both drop a character a present player may have earned, so they
		// are named beside the damage — never only logged. An absent player's file is NOT
		// here: decision 162 makes that a new character, not a degradation.
		damages.AddRange(bound.ClaimRefusals);

		// The itemized form of the same account, for the in-game surface (§6): the
		// Runtime damage above, then ONE line per entry the load or the decode skipped.
		// That second half is why this list exists — the summary's grouped count names
		// the domain file and the reason but never the affected content id, and the id
		// is what tells the player WHAT is gone.
		var details = new List<string>(damages);
		details.AddRange(salvage.Report.DescribeLines());

		if (salvage.Report.Entries.Count > 0)
		{
			damages.Add(salvage.Report.Describe());
		}

		var source = load.SourceName;
		var summary = damages.Count == 0
			? $"world {worldId} restored from {source}"
			: $"world {worldId} restored with damage: {string.Join("; ", damages)}";

		// ONE line per restore (S4 scope 3): which world, which kind of cut taken at
		// which phase, the revision and layer it was frozen at, where it was read from,
		// and the per-domain counts — the same domains the cut's own line reports, in
		// the same order (WorldSnapshotCounts), so a restore that took fewer rows than
		// the cut wrote is a two-line comparison. The reader keeps its own lines for
		// the passes it ran; THIS is the restore's account.
		var counts = WorldSnapshotCounts.Of(
			checkpoint,
			decode.UsableCharacters.Count,
			decode.UsableWorldBlocks.Count,
			decode.UsableWorldTransients.Count,
			decode.UsableNativeRunFields?.Recipes.Count);
		log.LogInformation(
			"Continue restored world {WorldId}: {Kind} cut taken at {CutPhase}, revision {Revision}, layer {Layer}, from {Source} ({SourcePath}); {Counts}; {Summary}",
			worldId, SaveArchiveFormat.CutKindName(content.Manifest.Kind), content.Manifest.CutPhase, checkpoint.GlobalRevision,
			checkpoint.Run?.LayerIndex ?? -1, source, content.SourcePath, counts.Describe(), summary);
		return new Result(
			new WorldContinueOutcome(true, worldId, summary, salvage, bound.LocalCharacter),
			worldId,
			content.Manifest.DisplayName,
			decode.UsableCharacters,
			details);
	}

	/// <summary>
	/// A layer-end cut names the layer being ENTERED, and this restore regenerates that
	/// layer from the baseline: a position captured while its body was still standing in
	/// the layer being LEFT does not describe the world being built, and applying it
	/// would teleport the body into a layer that no longer exists. (The same rule
	/// <c>RespawnPolicy.PrepareRespawn</c> applies to a next-level respawn.) The decoded
	/// rows are this restore's own copy — the decoder produced them and nothing else
	/// holds them yet — so the claim is cleared on the character the caller will bind,
	/// not on a live snapshot.
	/// </summary>
	private void DropReplacedLayerPositions(IReadOnlyList<SavedCharacter> stored)
	{
		foreach (var character in stored)
		{
			if (character.Character.Position is { } stale)
			{
				character.Character.Position = null;
				log.LogInformation(
					"Character {PlayerKey} of the layer-end cut carries a position ({X:F1},{Y:F1}) from the layer being replaced; it is not restored (the layer the snapshot names is regenerated).",
					character.PlayerKey, stale.X, stale.Y);
			}
		}
	}

	/// <summary>
	/// A layer-end cut's LAYER-SCOPED kernel rows describe the layer being LEFT, so the
	/// restore must not carry them into the layer it regenerates — the same rule the two
	/// halves above apply to the item rows and the world-entity facts, and the same rule
	/// the layer-boundary reset applies on the live path (<c>WorldService.ResetWorldLayerTables</c>,
	/// which resets exactly this family when a new layer is generated).
	///
	/// The two tables are the enemy rows and the fluid chunks. They are kernel table
	/// state rather than a handover, so the only way to drop them is the layer-boundary
	/// reset: the checkpoint restore put them in, and these two resets take them out.
	/// The ENEMY TOMBSTONES deliberately SURVIVE (the reset's own semantics — see
	/// <c>EnemyStateTable.WithoutLiveEnemies</c>): a tombstone is a terminal fact the
	/// killer earned, and the acceptance rule is that a killed enemy never comes back.
	/// What must not survive is a LIVE row, because it names a position and an id of the
	/// replaced layer. A MID-RUN cut keeps both tables: the layer it names is the one the
	/// restore rebuilds, so its enemies and its fluid chunks are that layer's own facts.
	/// </summary>
	private void DropReplacedLayerKernelTables()
	{
		var liveEnemyRows = kernel.QueryEnemies()?.Enemies.Count ?? 0;
		var fluidRows = kernel.QueryFluids()?.Regions.Count ?? 0;
		if (liveEnemyRows == 0 && fluidRows == 0)
		{
			return;
		}

		// A refused reset is logged rather than thrown: the live path resets the same
		// family again at the world-entry seam, and aborting a restore over one table
		// would lose the whole archive. It is never silent, though — an un-dropped
		// foreign-layer row is exactly the leak this method exists to close.
		// Resolved once per attempt, at the moment the resets run: the local identity is
		// read from the session rather than captured, so a late Steam init cannot leave a
		// 0 actor stamped on the committed reset batch.
		var actor = _layerActor();
		if (!kernel.TryResetEnemies(actor, out _, out var enemyRejection))
		{
			log.LogWarning(
				"The layer-end cut's {Count} live enemy row(s) could not be dropped ({Reason}: {Message}); the world-entry layer reset is the remaining guard.",
				liveEnemyRows, enemyRejection!.Reason, enemyRejection.Message);
		}

		if (!kernel.TryResetFluids(actor, out _, out var fluidRejection))
		{
			log.LogWarning(
				"The layer-end cut's {Count} fluid chunk(s) could not be dropped ({Reason}: {Message}); the world-entry layer reset is the remaining guard.",
				fluidRows, fluidRejection!.Reason, fluidRejection.Message);
		}

		log.LogInformation(
			"A layer-end cut names the layer being ENTERED, so its {Enemies} live enemy row(s) and {Fluids} fluid chunk(s) describe the layer being replaced and are not restored; its enemy tombstones stay terminal.",
			liveEnemyRows, fluidRows);
	}

	/// <summary>
	/// A refusal carries the itemized account exactly like an applied restore: the
	/// skipped content ids and the backup that could not be used are what the player
	/// has to read when the Continue click does nothing (§6).
	/// </summary>
	private Result Refuse(string worldId, string summary, SalvageResult? salvage = null)
	{
		var loaded = salvage ?? new SalvageResult(DamageReport.Empty);
		return new(
			WorldContinueOutcome.Refused(worldId, summary, loaded),
			string.Empty,
			string.Empty,
			[],
			loaded.Report.DescribeLines());
	}
}
