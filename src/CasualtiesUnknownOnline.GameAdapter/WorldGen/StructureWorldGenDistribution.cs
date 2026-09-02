using System.Collections;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Automatic world-generation distribution for mod-bound multi-block structures.
/// It is driven as a coroutine wrapper around the vanilla
/// <c>WorldGenerateWorldBorders</c> iterator, so it runs inside
/// <see cref="WorldGenRandomIsolation"/>: every Random consumption is part of
/// the sealed generation stream and therefore identical on every side. The
/// seam places only the static compiled block grid — no entity, loot, liquid or
/// background layer — and calls the vanilla <c>WorldGeneration.SetBlock</c>
/// path while <c>generatingWorld</c> is still true, so the existing
/// <c>BlockPlaced</c> relay and difference table intentionally see nothing
/// (the generated world is the baseline itself).
/// </summary>
internal sealed class StructureWorldGenDistribution(
	GameAdapterStructureContentProvider structureContent,
	GameAdapterTileContentProvider tileContent,
	ILogger<StructureWorldGenDistribution> log)
{
	private const int BorderMargin = 50;
	private const int MaxPlacementAttempts = 24;
	private const int LargeSpawnCountWarningThreshold = 40;

	private readonly GameAdapterStructureContentProvider _structureContent = structureContent;
	private readonly GameAdapterTileContentProvider _tileContent = tileContent;
	private readonly ILogger<StructureWorldGenDistribution> _log = log;
	private readonly HashSet<string> _reportedTileFailures = [];

	internal IEnumerator Wrap(IEnumerator original, WorldGeneration world)
	{
		while (original.MoveNext())
		{
			yield return original.Current;
		}

		if (world == null) // Unity object — ==
		{
			yield break;
		}

		if ((int)world.biomeOverride == (int)WorldGeneration.OverrideSceneType.Tutorial)
		{
			yield break;
		}

		Distribute(world);
	}

	private void Distribute(WorldGeneration world)
	{
		var depth = world.biomeDepth;
		var worldWidth = (int)world.width;
		var worldHeight = (int)world.height;
		if (worldWidth <= BorderMargin * 2 || worldHeight <= BorderMargin * 2)
		{
			_log.LogWarning(
				"[StructureWorldGen] world {Width}x{Height} is too small for the {Margin}-block border margin — no custom structures distributed.",
				worldWidth, worldHeight, BorderMargin);
			return;
		}

		var placed = 0;
		var skipped = 0;
		var totalRequested = 0;
		foreach (var pair in _structureContent.GetCompiledForWorldGen())
		{
			if (!_structureContent.TryGetDefinition(pair.Key, out var definition))
			{
				continue;
			}

			if (!definition.TryGetSpawnCount(depth, out var count) || count <= 0)
			{
				continue;
			}

			totalRequested += count;
			if (totalRequested > LargeSpawnCountWarningThreshold && totalRequested - count <= LargeSpawnCountWarningThreshold)
			{
				_log.LogWarning(
					"[StructureWorldGen] structures request {Total} placements on depth {Depth}; large counts can extend world generation.",
					totalRequested, depth);
			}

			for (var index = 0; index < count; index++)
			{
				if (TryPlaceOnce(world, pair.Key, pair.Value, out var originX, out var originY))
				{
					placed++;
					_log.LogInformation(
						"[StructureWorldGen] placed {StructureId} at block ({X},{Y}) on depth {Depth} ({Index}/{Count}).",
						pair.Key, originX, originY, depth, index + 1, count);
				}
				else
				{
					skipped++;
					_log.LogWarning(
						"[StructureWorldGen] could not place {StructureId} on depth {Depth} after {Attempts} attempts ({Index}/{Count}).",
						pair.Key, depth, MaxPlacementAttempts, index + 1, count);
				}
			}
		}

		if (totalRequested > 0)
		{
			_log.LogInformation(
				"[StructureWorldGen] distribution complete on depth {Depth}: {Placed} placed, {Skipped} skipped, {Requested} requested.",
				depth, placed, skipped, totalRequested);
		}
		else
		{
			_log.LogDebug("[StructureWorldGen] no worldgen structures requested on depth {Depth}.", depth);
		}
	}

	private bool TryPlaceOnce(
		WorldGeneration world,
		string structureId,
		GameAdapterStructureContentProvider.CompiledStructure structure,
		out int originX,
		out int originY)
	{
		originX = 0;
		originY = 0;
		var worldWidth = (int)world.width;
		var worldHeight = (int)world.height;

		for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
		{
			var centerX = Random.Range(BorderMargin, worldWidth - BorderMargin);
			var centerY = Random.Range(BorderMargin, worldHeight - BorderMargin);
			var candidateOriginX = centerX - structure.Width / 2;
			var candidateOriginY = centerY - structure.Height / 2;

			if (!TryPrepareWrites(world, structureId, structure, candidateOriginX, candidateOriginY, worldWidth, worldHeight, out var writes))
			{
				continue;
			}

			foreach (var write in writes)
			{
				world.SetBlock(write.Pos, write.Block);
			}

			originX = candidateOriginX;
			originY = candidateOriginY;
			return true;
		}

		return false;
	}

	private bool TryPrepareWrites(
		WorldGeneration world,
		string structureId,
		GameAdapterStructureContentProvider.CompiledStructure structure,
		int originX,
		int originY,
		int worldWidth,
		int worldHeight,
		out List<(Vector2Int Pos, ushort Block)> writes)
	{
		writes = [];
		foreach (var cell in structure.Cells)
		{
			var x = originX + cell.X;
			var y = originY + cell.Y;
			if (x < 0 || y < 0 || x >= worldWidth || y >= worldHeight)
			{
				return false;
			}

			if (cell.IsCustomTile)
			{
				if (!_tileContent.TryPrepareForPlacement(cell.TileId!, world, out var customIndex))
				{
					if (_reportedTileFailures.Add(structureId))
					{
						_log.LogWarning(
							"[StructureWorldGen] structure {StructureId} references custom tile {TileId}, which is not available during world generation — no copies distributed.",
							structureId, cell.TileId);
					}

					return false;
				}

				writes.Add((new Vector2Int(x, y), customIndex));
			}
			else
			{
				writes.Add((new Vector2Int(x, y), (ushort)cell.VanillaBlockIndex));
			}
		}

		return writes.Count > 0;
	}
}
