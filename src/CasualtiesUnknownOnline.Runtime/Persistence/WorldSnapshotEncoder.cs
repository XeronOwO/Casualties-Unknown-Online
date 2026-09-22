using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Application.Kernel;

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

	/// <summary>
	/// Builds every payload file of one cut, dropping the row counts — the projection the
	/// suites that pin the payload's row SHAPES use. The cut writer calls
	/// <see cref="EncodeWithCounts"/> instead, because the line it logs has to name the
	/// rows the ARCHIVE holds.
	/// </summary>
	public IReadOnlyList<SavePayloadFile> Encode(WorldSnapshotPayload payload) => EncodeWithCounts(payload).Files;

	/// <summary>
	/// Builds every payload file of one cut, together with the per-domain row counts the
	/// ARCHIVE will hold. Throws when the payload has no run baseline — a snapshot without
	/// one could not restore into any layer.
	///
	/// The counts come from the row lists this method actually wrote, never from the
	/// checkpoint it was handed: a layer-end cut DROPS in-layer rows, so the checkpoint's
	/// own counts describe a different set than the archive does — and the restore that
	/// reads that archive can only report the archive's.
	/// </summary>
	internal EncodedSnapshot EncodeWithCounts(WorldSnapshotPayload payload)
	{
		var checkpoint = payload.Checkpoint;
		if (checkpoint.Run is null)
		{
			throw new ArgumentException(
				"a snapshot without a run baseline would not know which layer to restore into; refusing to write it",
				nameof(payload));
		}

		WarnAboutUnpersistedDomains(payload);

		var runRows = RunRows(checkpoint.Run, payload.RunFields, payload.Kind);
		var playerRows = (checkpoint.Players?.Players ?? []).Select(KernelDomainWireMapper.ToWirePlayerState).ToList();
		var itemRows = ItemRows(checkpoint, payload.Kind);
		var entityRows = WorldEntityRows(checkpoint.WorldEntities ?? WorldEntityState.Empty, payload.Kind);
		var enemyRows = EnemyRows(checkpoint.Enemies ?? EnemyStateTable.Empty, payload.Kind);

		// A layer-end cut has no in-layer deviations: the blocks the player changed
		// and the transient world facts are DROPPED here, because the layer it names
		// is regenerated from the run baseline (§3.4/§4). Every other kind writes the
		// payload's facts as typed rows.
		var fluidRows = FluidRows(checkpoint.Fluids, payload.Kind);
		var blockRows = RowsFor(payload, payload.WorldBlocks);
		var transientRows = RowsFor(payload, payload.WorldTransients);

		var files = new List<SavePayloadFile>(12 + payload.Characters.Count)
		{
			Json(SaveArchiveFormat.RunFileName, runRows),
			Json(SaveArchiveFormat.PlayersFileName, playerRows),
			Json(SaveArchiveFormat.ItemsFileName, itemRows),
			Json(SaveArchiveFormat.WorldEntitiesFileName, entityRows),
			Json(SaveArchiveFormat.EnemiesFileName, enemyRows),
			Json(SaveArchiveFormat.FluidsFileName, fluidRows),
			Json(SaveArchiveFormat.WorldBlocksFileName, blockRows),
			Json(SaveArchiveFormat.WorldTransientsFileName, transientRows),
		};

		foreach (var character in payload.Characters)
		{
			files.Add(Json(SaveArchiveFormat.CharacterFilePath(character.PlayerKey), new List<CharacterDataMsg> { character.Character }));
		}

		var counts = new WorldSnapshotCounts(
			itemRows.Count,
			playerRows.Count,
			enemyRows.Count,
			fluidRows.Count,
			entityRows.Count,
			payload.Characters.Count,
			blockRows.Count,
			transientRows.Count,
			payload.RunFields is { } fields ? fields.Recipes.Count : null);

		// Debug, not Information: the commit line of the cut these files were encoded for
		// reports the same account (with the world id, the kind, the phase, the revision,
		// the layer and the backup), and a second Information line for one cut is the noise
		// that hides the line a reader is looking for.
		_log.LogDebug("Encoded snapshot payload: {Files} file(s), epoch {Epoch}, revision {Revision}, layer {Layer}; {Counts}.",
			files.Count, checkpoint.RunEpoch.Value, checkpoint.GlobalRevision, checkpoint.Run.LayerIndex, counts.Describe());
		return new EncodedSnapshot(files, counts);
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
	private List<SaveRunRow> RunRows(RunState run, NativeRunFields? runFields, WorldCutKind kind)
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
				// A layer-end cut records no layer timer: the layer it names is regenerated,
				// so the pair would describe the layer being replaced (the encoder drops
				// every other in-layer fact for the same reason).
				LayerTimeSpent = kind == WorldCutKind.LayerEnd ? null : captured.LayerTimeSpent,
			}));
		}

		return rows;
	}

	/// <summary>
	/// <c>items.json</c>'s rows. A layer-end cut records no in-layer fact, and a
	/// WORLD-ROOTED item IS one: it lies in the layer being replaced — and so does
	/// everything inside a container that lies there. Those records are dropped with
	/// the very rule the layer-boundary reset uses
	/// (<see cref="ItemLocationChain.IsWorldRooted"/>), so the archive and the kernel
	/// can never disagree about what "in the world" means, and a container's
	/// contents never outlive their container in the file. Carried records (the
	/// player carries them across the boundary) and terminal tombstones stay.
	///
	/// The kernel's own copy is normally already gone by the time this cut is
	/// written (the generation boundary resets the layer tables before the advance
	/// that takes the cut), so a row that still arrives is NAMED rather than silently
	/// trimmed. The rule is order-independent on purpose: a solo boundary's table
	/// reset is a sibling-domain concern, and a snapshot must never describe a layer
	/// a restore would have to drop.
	/// </summary>
	private List<WireItem> ItemRows(GameCheckpoint checkpoint, WorldCutKind kind)
	{
		if (kind != WorldCutKind.LayerEnd)
		{
			return [.. checkpoint.Items.Select(KernelWireMapper.ToWireItem)];
		}

		// The kernel's item table is keyed by instance id, so a parent lookup map is
		// enough — and a duplicate id (only a broken caller can produce one) must not
		// abort a cut from inside a LOOKUP helper, so the last row wins instead of
		// ToDictionary's throw.
		var byInstanceId = new Dictionary<ulong, ItemState>();
		foreach (var item in checkpoint.Items)
		{
			byInstanceId[item.Identity.InstanceId] = item;
		}
		var rows = new List<WireItem>(checkpoint.Items.Count);
		var dropped = 0;
		foreach (var item in checkpoint.Items)
		{
			if (ItemLocationChain.IsWorldRooted(item, id => byInstanceId.TryGetValue(id, out var parent) ? parent : null))
			{
				dropped++;
				continue;
			}

			rows.Add(KernelWireMapper.ToWireItem(item));
		}

		if (dropped > 0)
		{
			_log.LogWarning(
				"A layer-end cut carries {Count} world-rooted item record(s), and a layer-end cut writes none of them: the layer it names is regenerated from the run baseline, so those items belong to the layer being replaced and a restore could not give them back. They are not written (§3.4).",
				dropped);
		}

		return rows;
	}

	/// <summary>
	/// <c>world-entities.json</c>'s rows: the per-entity facts of ONE layer (consumed
	/// traps, the durable trap-state machine, opened lockables, building health), all
	/// position-keyed.
	///
	/// A layer-end cut records none of them: the layer it names is regenerated from
	/// the run baseline, so every one of these facts belongs to the layer being
	/// replaced — the same rule as the two world-fact files, and the same rule the
	/// layer-boundary reset applies to the kernel's copy. That reset is what makes
	/// the cut's own kernel tables empty in a host session; in any non-host state
	/// (including SOLO, where the game reports no role at all) the world-entity
	/// registries keep them, because their reset runs for a host only — which is
	/// exactly why the archive must not carry them: a layer-end restore puts the rows
	/// it reads back into the kernel, and a later guest join would then be handed
	/// facts about a layer the world no longer is. A caller's rows are NAMED, never
	/// silently trimmed.
	/// </summary>
	private List<SaveWorldEntityRow> WorldEntityRows(WorldEntityState state, WorldCutKind kind)
	{
		if (kind != WorldCutKind.LayerEnd)
		{
			return
			[
				.. state.Consumptions.Select(SaveWorldEntityRow.OfConsumption),
				.. state.BuildingHealth.Select(SaveWorldEntityRow.OfBuildingHealth),
				.. state.OpenedEntities.Select(SaveWorldEntityRow.OfOpenedEntity),
				.. state.TrapStates.Select(SaveWorldEntityRow.OfTrapState),
			];
		}

		var inLayerFactCount = state.Consumptions.Count + state.TrapStates.Count + state.OpenedEntities.Count + state.BuildingHealth.Count;
		if (inLayerFactCount > 0)
		{
			_log.LogWarning(
				"A layer-end cut carries {Count} world-entity fact(s) (consumed traps, trap states, opened entities, building health), and a layer-end cut writes none of them: the layer it names is regenerated from the run baseline, so those facts belong to the layer being replaced. They are not written (§3.4).",
				inLayerFactCount);
		}

		return [];
	}

	/// <summary>
	/// <c>enemies.json</c>'s rows: the LIVE enemy facts plus the terminal tombstones.
	///
	/// A layer-end cut writes the tombstones but NONE of the live rows — the same rule
	/// the item, world-entity, world-block and transient files already apply, and the
	/// one the layer-boundary reset applies to the kernel's own copy: a live enemy row
	/// names a position in the layer being LEFT and an id the host allocated from a
	/// per-session counter, so the layer the cut NAMES (the one being entered,
	/// regenerated from the run baseline, with its own enemies) has no such fact to
	/// restore and a row that reached it could be bound to a different enemy. The rule
	/// is written here as well as in the reset because the two have to agree even if a
	/// caller hands the encoder a kernel whose table the boundary has not cleared yet:
	/// the reset and the layer-end cut run inside ONE call stack (the generation
	/// boundary resets, then publishes the params that commit the advance that takes the
	/// cut), and the per-frame enemy projection mirrors the LIVE scene back into the
	/// kernel, so which of the two runs first is a frame-timing fact a snapshot must not
	/// depend on. A caller's live rows are NAMED, never silently trimmed.
	///
	/// A tombstone is NOT in that set: "this enemy was killed" is a terminal fact the
	/// player earned, it stops a stale live row from resurrecting the id, and it names no
	/// position of the replaced layer.
	/// </summary>
	private List<SaveEnemyRow> EnemyRows(EnemyStateTable table, WorldCutKind kind)
	{
		if (kind != WorldCutKind.LayerEnd)
		{
			return [.. table.Enemies.Select(SaveEnemyRow.OfEnemy), .. table.Removed.Select(SaveEnemyRow.OfRemoved)];
		}

		if (table.Enemies.Count > 0)
		{
			_log.LogWarning(
				"A layer-end cut carries {Count} live enemy row(s), and a layer-end cut writes none of them: they name positions and ids of the layer being replaced, and the layer it names is regenerated from the run baseline with its own enemies. They are not written (§3.4); the {Tombstones} terminal tombstone(s) are.",
				table.Enemies.Count, table.Removed.Count);
		}

		return [.. table.Removed.Select(SaveEnemyRow.OfRemoved)];
	}

	/// <summary>
	/// <c>fluids.json</c>'s rows, on the same rule as <see cref="EnemyRows"/>: a fluid
	/// chunk is a coarse total of ONE layer's grid, so a layer-end cut writes none of
	/// them — the regenerated layer runs its own aggregation, and a restored chunk total
	/// would be stale truth about a grid that no longer exists. There is no tombstone
	/// shape here, so a layer-end cut simply writes the empty array.
	/// </summary>
	private List<WireFluidRegionState> FluidRows(FluidStateTable? table, WorldCutKind kind)
	{
		var regions = table?.Regions ?? [];
		if (kind != WorldCutKind.LayerEnd)
		{
			return [.. regions.Select(KernelDomainWireMapper.ToWireFluidRegionState)];
		}

		if (regions.Count > 0)
		{
			_log.LogWarning(
				"A layer-end cut carries {Count} fluid chunk(s), and a layer-end cut writes none of them: they are totals of the replaced layer's grid, and the layer it names is regenerated from the run baseline. They are not written (§3.4).",
				regions.Count);
		}

		return [];
	}

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
