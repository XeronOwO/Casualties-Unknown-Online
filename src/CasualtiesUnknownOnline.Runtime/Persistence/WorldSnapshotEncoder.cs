using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
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

		var files = new List<SavePayloadFile>(9 + payload.Characters.Count)
		{
			Json(SaveArchiveFormat.RunFileName, new List<WireRunState> { KernelDomainWireMapper.ToWireRun(checkpoint.Run) }),
			Json(SaveArchiveFormat.PlayersFileName, (checkpoint.Players?.Players ?? []).Select(KernelDomainWireMapper.ToWirePlayerState).ToList()),
			Json(SaveArchiveFormat.ItemsFileName, checkpoint.Items.Select(KernelWireMapper.ToWireItem).ToList()),
			Json(SaveArchiveFormat.WorldEntitiesFileName, WorldEntityRows(checkpoint.WorldEntities ?? WorldEntityState.Empty)),
			Json(SaveArchiveFormat.EnemiesFileName, EnemyRows(checkpoint.Enemies ?? EnemyStateTable.Empty)),
			Json(SaveArchiveFormat.FluidsFileName, (checkpoint.Fluids?.Regions ?? []).Select(KernelDomainWireMapper.ToWireFluidRegionState).ToList()),

			// A layer-end cut has no in-layer deviations: the blocks the player
			// changed and the transient world facts (S3's domain) are regenerated
			// from the run baseline. The files exist now so S3 fills them without
			// a schema change (§3.4).
			Json(SaveArchiveFormat.WorldBlocksFileName, new List<object>()),
			Json(SaveArchiveFormat.WorldTransientsFileName, new List<object>()),
		};

		foreach (var character in payload.Characters)
		{
			files.Add(Json(SaveArchiveFormat.CharacterFilePath(character.PlayerKey), new List<CharacterDataMsg> { character.Character }));
		}

		_log.LogInformation("Encoded snapshot payload: {Files} file(s), epoch {Epoch}, revision {Revision}, layer {Layer}, {Items} item(s), {Players} player(s), {Characters} character(s).",
			files.Count, checkpoint.RunEpoch.Value, checkpoint.GlobalRevision, checkpoint.Run.LayerIndex, checkpoint.Items.Count, checkpoint.Players?.Players.Count ?? 0, payload.Characters.Count);
		return files;
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
	/// Domains the kernel checkpoint carries but no S2 file does. Nothing
	/// populates them yet (the RNG streams arrive with S3's consistent cut), and
	/// dropping a non-empty one silently is exactly what §6 forbids — so it is
	/// refused loudly here instead.
	/// </summary>
	private void WarnAboutUnpersistedDomains(WorldSnapshotPayload payload)
	{
		var streams = payload.Checkpoint.RandomStreams;
		if (streams is not null && streams.Count > 0)
		{
			_log.LogWarning("The checkpoint carries {Count} random stream(s); S2's snapshot file set has no file for them yet — they are NOT part of this save (S3 owns the consistent cut).",
				streams.Count);
		}
	}

	private static SavePayloadFile Json<T>(string path, T value) => new(path, SaveArchiveJson.Serialize(value));
}
