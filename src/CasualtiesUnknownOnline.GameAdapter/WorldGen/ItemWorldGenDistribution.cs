using System;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Automatic loose world-spawn distribution for mod-bound custom items. It is
/// called from the vanilla <c>WorldGeneration.PlaceCrystals</c> postfix, so all
/// <c>UnityEngine.Random</c> consumption lands inside the sealed generation
/// stream: both sides generate the same ground items from the same baseline.
/// Spawns use <c>Utils.Create</c>, so CUO custom item templates are
/// materialized; the existing generation-item snapshot later binds both sides
/// to the host's authoritative instance ids. No wire message, no JObject
/// snapshot, and no game or Unity type crosses Abstractions.
/// </summary>
internal sealed class ItemWorldGenDistribution(
	GameAdapterItemContentProvider itemContent,
	ILogger<ItemWorldGenDistribution> log)
{
	private static readonly int GroundMask = LayerMask.GetMask("Ground");

	private readonly GameAdapterItemContentProvider _itemContent = itemContent;
	private readonly ILogger<ItemWorldGenDistribution> _log = log;

	internal void Scatter(WorldGeneration world)
	{
		if (world is null) // Unity object — ==
		{
			return;
		}

		if (world.biomeOverride != WorldGeneration.OverrideSceneType.None)
		{
			return;
		}

		var definitions = _itemContent.GetDefinitionsForWorldSpawn();
		if (definitions.Count == 0)
		{
			return;
		}

		var spawned = 0;
		var skipped = 0;
		foreach (var pair in definitions)
		{
			var perChunk = pair.Value.WorldSpawnPerChunk ?? 0f;
			if (perChunk <= 0f)
			{
				continue;
			}

			var count = Mathf.RoundToInt(world.chunkWidth * world.chunkHeight * perChunk);
			for (var i = 0; i < count; i++)
			{
				if (TrySpawnLooseItem(world, pair.Key))
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
				"[ItemWorldGen] distribution complete on depth {Depth}: {Spawned} items spawned, {Skipped} skipped.",
				world.biomeDepth, spawned, skipped);
		}
	}

	private bool TrySpawnLooseItem(WorldGeneration world, string itemId)
	{
		var randomPos = new Vector2(
			Random.Range(-(float)world.halfWidth, world.halfWidth),
			Random.Range(-(float)world.halfHeight, world.halfHeight));

		if (Physics2D.OverlapPoint(randomPos, GroundMask))
		{
			return false;
		}

		var hit = Physics2D.Raycast(randomPos, Vector2.down, WorldGeneration.CHUNKSIZE, GroundMask);
		if (!hit)
		{
			return false;
		}

		try
		{
			var created = Utils.Create(itemId, hit.point + Vector2.up, Random.Range(0f, 360f));
			if (created is null) // Unity object — ==
			{
				_log.LogWarning("[ItemWorldGen] {Id} could not be created — skipped.", itemId);
				return false;
			}

			var item = created.GetComponent<Item>();
			if (item != null) // Unity object — ==
			{
				item.condition = 1f;
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "[ItemWorldGen] {Id} failed to spawn on the surface — skipped.", itemId);
			return false;
		}
	}
}
