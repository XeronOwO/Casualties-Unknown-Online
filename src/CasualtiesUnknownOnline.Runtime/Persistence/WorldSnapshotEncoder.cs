using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The in-memory → disk half of §3.4: the kernel checkpoint becomes the
/// snapshot's domain files, one file per domain table, every file an entry array
/// (the shape <see cref="SaveArchiveReader.ReadSalvage"/> decodes one entry at a
/// time).
///
/// The domain payloads are the wire checkpoint's own DTOs — the mapping a guest
/// receives on join and the mapping a restore reads back are the same code, so a
/// restored host holds exactly what a late-joining guest would have been given.
/// A table with more than one fact shape (world entities, enemies) writes typed
/// rows instead of one blob so §6's salvage stays per entry.
/// </summary>
public sealed class WorldSnapshotEncoder(ILogger<WorldSnapshotEncoder> log)
{
	private readonly ILogger<WorldSnapshotEncoder> _log = log;

	/// <summary>Builds every payload file of one cut. Throws when the payload has no run baseline — a snapshot without one could not restore into any layer.</summary>
	public IReadOnlyList<SavePayloadFile> Encode(WorldSnapshotPayload payload)
	{
		var checkpoint = payload.Checkpoint;
		if (checkpoint.Run is null)
		{
			throw new ArgumentException(
				"a snapshot without a run baseline would not know which layer to restore into; refusing to write it",
				nameof(payload));
		}

		WarnAboutUnpersistedDomains(payload);

		var files = new List<SavePayloadFile>(12 + payload.Characters.Count)
		{
			Json(SaveArchiveFormat.RunFileName, RunRows(checkpoint.Run, payload.RunFields)),
			Json(SaveArchiveFormat.PlayersFileName, (checkpoint.Players?.Players ?? []).Select(KernelDomainWireMapper.ToWirePlayerState).ToList()),
			Json(SaveArchiveFormat.ItemsFileName, checkpoint.Items.Select(KernelWireMapper.ToWireItem).ToList()),
			Json(SaveArchiveFormat.WorldEntitiesFileName, WorldEntityRows(checkpoint.WorldEntities ?? WorldEntityState.Empty)),
			Json(SaveArchiveFormat.EnemiesFileName, EnemyRows(checkpoint.Enemies ?? EnemyStateTable.Empty)),
			Json(SaveArchiveFormat.FluidsFileName, (checkpoint.Fluids?.Regions ?? []).Select(KernelDomainWireMapper.ToWireFluidRegionState).ToList()),

			// A layer-end cut has no in-layer deviations: the blocks the player
			// changed and the transient world facts are DROPPED here, because the
			// layer it names is regenerated from the run baseline (§3.4/§4). Every
			// other kind writes the payload's facts as typed rows.
			Json(SaveArchiveFormat.WorldBlocksFileName, RowsFor(payload, payload.WorldBlocks)),
			Json(SaveArchiveFormat.WorldTransientsFileName, RowsFor(payload, payload.WorldTransients)),
		};

		foreach (var character in payload.Characters)
		{
			files.Add(Json(SaveArchiveFormat.CharacterFilePath(character.PlayerKey), new List<CharacterDataMsg> { character.Character }));
		}

		_log.LogInformation("Encoded snapshot payload: {Files} file(s), epoch {Epoch}, revision {Revision}, layer {Layer}, {Items} item(s), {Players} player(s), {Characters} character(s), run fields {RunFields}.",
			files.Count, checkpoint.RunEpoch.Value, checkpoint.GlobalRevision, checkpoint.Run.LayerIndex, checkpoint.Items.Count, checkpoint.Players?.Players.Count ?? 0, payload.Characters.Count,
			payload.RunFields is null ? "absent" : $"present ({payload.RunFields.Value.Recipes.Count} recipe row(s))");
		return files;
	}

	/// <summary>
	/// <c>run.json</c>'s rows: the kernel baseline, and the native run fields when
	/// the cut captured them.
	///
	/// The baseline's rarity multipliers are the ones the kernel captured at the
	/// named layer's GENERATION BOUNDARY, and they are written as they are: that
	/// capture reads the live world after the game applied the layer's accumulation
	/// (`WorldGeneration.cs:1061-1062`) and its Start-time trap term (`:257`), so it
	/// IS the value the layer was generated with — and, for a layer-end cut, the
	/// value the layer it names will be generated with.
	///
	/// The cut instant's own read is used to WARN when the two disagree, never to
	/// overwrite: the live world can already be one layer ahead of the baseline (the
	/// game accumulates the next layer's multipliers while it clears the old one, and
	/// a cut taken in that window would otherwise stamp the NEXT layer's values onto
	/// the layer its row names — a restore would then rebuild the named layer with
	/// the wrong loot/trap density).
	/// </summary>
	private List<SaveRunRow> RunRows(RunState run, NativeRunFields? runFields)
	{
		var wire = KernelDomainWireMapper.ToWireRun(run);
		if (runFields is { } fields
			&& (fields.LootRarityMultiplier != run.LootRarityMultiplier || fields.TrapRarityMultiplier != run.TrapRarityMultiplier))
		{
			_log.LogWarning(
				"The live world's rarity multipliers (loot {LiveLoot:F3}, trap {LiveTrap:F3}) differ from the run baseline this cut records (loot {RunLoot:F3}, trap {RunTrap:F3}) at layer {Layer}. The baseline — the value that layer was generated with — is what the snapshot carries; a cut taken while the game is already accumulating the next layer is the usual reason.",
				fields.LootRarityMultiplier, fields.TrapRarityMultiplier, run.LootRarityMultiplier, run.TrapRarityMultiplier, run.LayerIndex);
		}

		var rows = new List<SaveRunRow>(2) { SaveRunRow.OfRun(wire) };
		if (runFields is { } captured)
		{
			rows.Add(SaveRunRow.OfNativeRunFields(new SaveNativeRunFields
			{
				SavedRunTime = captured.SavedRunTime,
				Recipes = [.. captured.Recipes],
			}));
		}

		return rows;
	}

	private static List<SaveWorldEntityRow> WorldEntityRows(WorldEntityState state) =>
	[
		.. state.Consumptions.Select(SaveWorldEntityRow.OfConsumption),
		.. state.BuildingHealth.Select(SaveWorldEntityRow.OfBuildingHealth),
		.. state.OpenedEntities.Select(SaveWorldEntityRow.OfOpenedEntity),
		.. state.TrapStates.Select(SaveWorldEntityRow.OfTrapState),
	];

	private static List<SaveEnemyRow> EnemyRows(EnemyStateTable table) =>
	[
		.. table.Enemies.Select(SaveEnemyRow.OfEnemy),
		.. table.Removed.Select(SaveEnemyRow.OfRemoved),
	];

	/// <summary>
	/// The world facts one cut writes, or none. The kind owns the decision: a
	/// layer-end cut records no in-layer fact at all, so a caller that gathered
	/// the live tables for one (they do exist mid-layer, e.g. at a deliberate
	/// menu return) would otherwise lose them silently — the mismatch is NAMED
	/// here instead, and the cut still writes the empty arrays it must.
	/// </summary>
	private List<T> RowsFor<T>(WorldSnapshotPayload payload, IReadOnlyList<T>? facts)
	{
		if (payload.Kind != WorldCutKind.LayerEnd)
		{
			return facts is null ? [] : [.. facts];
		}

		if (facts is { Count: > 0 })
		{
			_log.LogWarning(
				"A {Kind} cut carries {Count} world fact(s), and a layer-end cut writes none of them: the layer it names is regenerated from the run baseline. The facts are not written (§3.4).",
				payload.Kind, facts.Count);
		}

		return [];
	}

	/// <summary>
	/// Domains the kernel checkpoint carries but no S2 file does. Nothing
	/// populates them yet (the RNG streams arrive with S3's consistent cut), and
	/// dropping a non-empty one silently is exactly what §6 forbids — so it is
	/// refused loudly here instead.
	/// </summary>
	private void WarnAboutUnpersistedDomains(WorldSnapshotPayload payload)
	{
		var streams = payload.Checkpoint.RandomStreams;
		if (streams is null || streams.Count == 0)
		{
			return;
		}

		// S2's file set has no file for the RNG streams, and dropping them would make a
		// restore regenerate a different world than the run that was cut — §6 forbids
		// a silent drop, so the cut is REFUSED until S3 adds the file. Nothing produces
		// streams today, so this is a guard, not a path in use.
		throw new NotSupportedException(
			$"the checkpoint carries {streams.Count} random stream(s), and S2's snapshot file set has no file for them; " +
			"refusing the cut rather than writing a lossy snapshot (S3 owns the consistent cut)");
	}

	private static SavePayloadFile Json<T>(string path, T value) => new(path, SaveArchiveJson.Serialize(value));
}
