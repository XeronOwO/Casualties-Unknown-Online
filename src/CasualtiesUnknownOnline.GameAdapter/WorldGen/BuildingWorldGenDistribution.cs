using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Automatic world-generation distribution for mod-bound custom building
/// entities. It is called from the vanilla <c>WorldGeneration.PlaceCrystals</c>
/// postfix, so all <c>UnityEngine.Random</c> consumption lands inside the sealed
/// generation stream: both sides create the same building entities from the same
/// baseline. Spawns use <c>Utils.Create</c>, so CUO custom building templates are
/// materialized; <c>BuildingEntity.Start</c> reports are suppressed while
/// generation is active, so the deterministic world needs no extra wire message.
/// No game or Unity type crosses Abstractions.
/// </summary>
internal sealed class BuildingWorldGenDistribution(
	GameAdapterBuildingContentProvider buildingContent,
	ILogger<BuildingWorldGenDistribution> log)
{
	private static readonly int GroundMask = LayerMask.GetMask("Ground");

	private readonly GameAdapterBuildingContentProvider _buildingContent = buildingContent;
	private readonly ILogger<BuildingWorldGenDistribution> _log = log;

	internal void Distribute(WorldGeneration world)
	{
		if (world is null) // Unity object — ==
		{
			return;
		}

		if (world.biomeOverride != WorldGeneration.OverrideSceneType.None)
		{
			return;
		}

		var definitions = _buildingContent.GetDefinitionsForWorldGen();
		if (definitions.Count == 0)
		{
			return;
		}

		var spawned = 0;
		var skipped = 0;
		foreach (var pair in definitions)
		{
			if (!pair.Value.CanSpawnInLayer(world.biomeDepth))
			{
				continue;
			}

			var min = pair.Value.SpawnMinPerChunk ?? 0f;
			var max = pair.Value.SpawnMaxPerChunk ?? 0f;
			if (max <= 0f)
			{
				continue;
			}

			var perCell = Random.Range(min, max);
			var count = Mathf.RoundToInt(world.chunkWidth * world.chunkHeight * perCell);
			for (var i = 0; i < count; i++)
			{
				var spawnedHere = pair.Value.GenerationStyle == ModBuildingGenerationStyle.DropPod
					? TrySpawnDropPod(world, pair.Key, pair.Value)
					: TrySpawnStandard(world, pair.Key, pair.Value);
				if (spawnedHere)
				{
					spawned++;
				}
				else
				{
					skipped++;
				}
			}
		}

		if (spawned > 0 || skipped > 0)
		{
			_log.LogInformation(
				"[BuildingWorldGen] distribution complete on depth {Depth}: {Spawned} buildings spawned, {Skipped} skipped.",
				world.biomeDepth, spawned, skipped);
		}
	}

	private bool TrySpawnStandard(WorldGeneration world, string id, ModBuildingDefinition definition)
	{
		var randomPos = new Vector2(
			Random.Range(-(float)world.halfWidth, world.halfWidth),
			Random.Range(-(float)world.halfHeight, world.halfHeight));

		if (Physics2D.OverlapPoint(randomPos, GroundMask) && !definition.SpawnInGround)
		{
			return false;
		}

		var direction = DirectionForPlacement(definition.Placement);
		var hit = Physics2D.Raycast(randomPos, direction, WorldGeneration.CHUNKSIZE, GroundMask);
		if (!hit)
		{
			return false;
		}

		if (Mathf.Abs(hit.point.x) >= world.halfWidth - 1f
			|| Mathf.Abs(hit.point.y) >= world.halfHeight - 1f)
		{
			return false;
		}

		var surfaceOffset = definition.SurfaceOffset ?? 0.5f;
		var spawnPos = hit.point - direction * surfaceOffset;
		var created = Utils.Create(id, spawnPos, 0f);
		if (created is null) // Unity object — ==
		{
			_log.LogWarning("[BuildingWorldGen] {Id} could not be created — skipped.", id);
			return false;
		}

		ApplyRandomFlip(created, definition);
		return true;
	}

	private bool TrySpawnDropPod(WorldGeneration world, string id, ModBuildingDefinition definition)
	{
		var randomPos = new Vector2(
			Random.Range(-(float)world.halfWidth + 50f, world.halfWidth - 50f),
			Random.Range(-(float)world.halfHeight + 50f, world.halfHeight - 50f));

		var hit = Physics2D.Raycast(randomPos, Vector2.down, 400f, GroundMask);
		var finalPos = hit ? hit.point : randomPos;
		if (Mathf.Abs(finalPos.x) >= world.halfWidth - 40f
			|| Mathf.Abs(finalPos.y) >= world.halfHeight - 40f)
		{
			return false;
		}

		var created = Utils.Create(id, finalPos, Random.Range(0f, 360f));
		if (created is null) // Unity object — ==
		{
			_log.LogWarning("[BuildingWorldGen] {Id} could not be created as a drop pod — skipped.", id);
			return false;
		}

		ApplyRandomFlip(created, definition);
		return true;
	}

	private static Vector2 DirectionForPlacement(ModBuildingPlacement placement) =>
		placement switch
		{
			ModBuildingPlacement.Ceiling => Vector2.up,
			ModBuildingPlacement.Wall => Random.value > 0.5f ? Vector2.right : Vector2.left,
			_ => Vector2.down
		};

	private static void ApplyRandomFlip(GameObject created, ModBuildingDefinition definition)
	{
		if ((definition.RandomFlip ?? true) && Random.value > 0.5f)
		{
			created.transform.localScale = new Vector3(
				-created.transform.localScale.x,
				created.transform.localScale.y,
				created.transform.localScale.z);
		}
	}
}
