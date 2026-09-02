using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Automatic ore-style world-generation distribution for mod-bound custom tiles.
/// It is called from the vanilla <c>WorldGeneration.GenerateOres</c> postfix, so
/// all <c>UnityEngine.Random</c> consumption lands inside the sealed generation
/// stream: both sides generate the same custom ore deposits from the same
/// baseline. Placement writes directly into the game's <c>worldBlocks</c> array
/// (the same writer the vanilla ore pass uses); every custom tile is first
/// injected into the current <c>WorldGeneration.tiles</c> palette through the
/// tile content provider. No wire message, no JObject snapshot, and no game or
/// Unity type crosses Abstractions.
/// </summary>
internal sealed class TileWorldGenDistribution(
	GameAdapterTileContentProvider tileContent,
	ILogger<TileWorldGenDistribution> log)
{
	private static readonly ModTileGenerationStyle[] AllStyles =
	[
		ModTileGenerationStyle.Vein,
		ModTileGenerationStyle.HeavyVeins,
		ModTileGenerationStyle.Singular,
		ModTileGenerationStyle.Stripe,
		ModTileGenerationStyle.Inner,
		ModTileGenerationStyle.Outskirt
	];

	private readonly GameAdapterTileContentProvider _tileContent = tileContent;
	private readonly ILogger<TileWorldGenDistribution> _log = log;

	internal void Distribute(WorldGeneration world)
	{
		if (world is null) // Unity object — ==
		{
			return;
		}

		if ((int)world.biomeOverride == (int)WorldGeneration.OverrideSceneType.Tutorial)
		{
			return;
		}

		var worldBlocks = HarmonyTraverse.ReadWorldBlocks(world);
		if (worldBlocks is null)
		{
			_log.LogWarning("[TileWorldGen] worldBlocks is unavailable — no custom tile ore distributed.");
			return;
		}

		var definitions = _tileContent.GetDefinitionsForWorldGen();
		if (definitions.Count == 0)
		{
			return;
		}

		var placed = 0;
		var skipped = 0;
		foreach (var pair in definitions)
		{
			if (!CanSpawn(pair.Value, world))
			{
				continue;
			}

			if (!_tileContent.TryPrepareForPlacement(pair.Key, world, out var index))
			{
				skipped++;
				_log.LogWarning("[TileWorldGen] {Id} could not be prepared for world generation — no copies distributed.", pair.Key);
				continue;
			}

			var style = pair.Value.GenerationStyle == ModTileGenerationStyle.None
				? ModTileGenerationStyle.Vein
				: pair.Value.GenerationStyle;
			var styleCount = CountStyles(style);
			var styleAmount = pair.Value.SpawnAmount / styleCount;
			placed += ApplyStyles(world, worldBlocks, index, styleAmount, style);
		}

		if (placed > 0 || skipped > 0)
		{
			_log.LogInformation(
				"[TileWorldGen] distribution complete on depth {Depth}: {Placed} cells placed, {Skipped} tiles skipped.",
				world.biomeDepth, placed, skipped);
		}
	}

	private static bool CanSpawn(ModTileDefinition definition, WorldGeneration world) =>
		definition is not null
		&& definition.SpawnAmount > 0f
		&& definition.CanSpawnInLayer(world.biomeDepth);

	private static int CountStyles(ModTileGenerationStyle style)
	{
		var count = 0;
		foreach (var candidate in AllStyles)
		{
			if ((style & candidate) != 0)
			{
				count++;
			}
		}

		return Math.Max(1, count);
	}

	private int ApplyStyles(
		WorldGeneration world,
		ushort[,] worldBlocks,
		ushort tileIndex,
		float spawnAmount,
		ModTileGenerationStyle style)
	{
		var placed = 0;
		if ((style & ModTileGenerationStyle.Vein) != 0)
		{
			placed += GenerateVeins(world, worldBlocks, tileIndex, spawnAmount, 1, 25);
		}

		if ((style & ModTileGenerationStyle.HeavyVeins) != 0)
		{
			placed += GenerateVeins(world, worldBlocks, tileIndex, spawnAmount * 2f, 18, 43);
		}

		if ((style & ModTileGenerationStyle.Singular) != 0)
		{
			placed += GenerateSingular(world, worldBlocks, tileIndex, spawnAmount);
		}

		if ((style & ModTileGenerationStyle.Stripe) != 0)
		{
			placed += GenerateStripes(world, worldBlocks, tileIndex, spawnAmount);
		}

		if ((style & ModTileGenerationStyle.Inner) != 0)
		{
			placed += GenerateClusters(world, tileIndex, spawnAmount, innerBias: true);
		}

		if ((style & ModTileGenerationStyle.Outskirt) != 0)
		{
			placed += GenerateClusters(world, tileIndex, spawnAmount, innerBias: false);
		}

		return placed;
	}

	private int GenerateVeins(
		WorldGeneration world,
		ushort[,] worldBlocks,
		ushort tileIndex,
		float spawnAmount,
		int minSteps,
		int maxStepsExclusive)
	{
		var width = (int)world.width;
		var height = (int)world.height;
		var worldPlaced = 0;
		var attempts = GetAttempts(world, spawnAmount);
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			var x = Random.Range(0, width);
			var y = Random.Range(0, height);
			var steps = Random.Range(minSteps, maxStepsExclusive);
			for (var step = 0; step < steps; step++)
			{
				if (x > 0 && x < width - 1 && y > 0 && y < height - 1 && worldBlocks[x, y] > 0)
				{
					worldBlocks[x, y] = tileIndex;
					worldPlaced++;
				}

				var dx = Random.value > 0.5f ? (Random.value > 0.5f ? 1 : -1) : 0;
				var dy = Random.value > 0.5f ? (Random.value > 0.5f ? 1 : -1) : 0;
				x += dx;
				y += dy;
			}
		}

		return worldPlaced;
	}

	private int GenerateSingular(
		WorldGeneration world,
		ushort[,] worldBlocks,
		ushort tileIndex,
		float spawnAmount)
	{
		var width = (int)world.width;
		var height = (int)world.height;
		if (width <= 2 || height <= 2)
		{
			return 0;
		}

		var worldPlaced = 0;
		var attempts = GetAttempts(world, spawnAmount);
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			var x = Random.Range(1, width - 1);
			var y = Random.Range(1, height - 1);
			if (worldBlocks[x, y] > 0)
			{
				worldBlocks[x, y] = tileIndex;
				worldPlaced++;
			}
		}

		return worldPlaced;
	}

	private int GenerateStripes(
		WorldGeneration world,
		ushort[,] worldBlocks,
		ushort tileIndex,
		float spawnAmount)
	{
		var width = (int)world.width;
		var height = (int)world.height;
		var stripeCount = Math.Max(1, Mathf.RoundToInt(GetAttempts(world, spawnAmount) / 12f));
		var worldPlaced = 0;
		for (var stripe = 0; stripe < stripeCount; stripe++)
		{
			var horizontal = Random.value > 0.5f;
			var stripeWidth = Random.Range(2, 6);
			var stripeLength = Random.Range(18, 56);
			var originX = Random.Range(0, width);
			var originY = Random.Range(0, height);
			for (var step = 0; step < stripeLength; step++)
			{
				for (var offset = -stripeWidth; offset <= stripeWidth; offset++)
				{
					var x = horizontal ? originX + step : originX + offset;
					var y = horizontal ? originY + offset : originY + step;
					if (TrySetGeneratedBlock(worldBlocks, x, y, tileIndex, width, height))
					{
						worldPlaced++;
					}
				}
			}
		}

		return worldPlaced;
	}

	private int GenerateClusters(
		WorldGeneration world,
		ushort tileIndex,
		float spawnAmount,
		bool innerBias)
	{
		var width = (int)world.width;
		var height = (int)world.height;
		var clusterCount = Math.Max(1, Mathf.RoundToInt(GetAttempts(world, spawnAmount) / 18f));
		var horizontalRadius = width * (innerBias ? 0.18f : 0.42f);
		var verticalRadius = height * (innerBias ? 0.18f : 0.42f);
		var center = new Vector2(world.halfWidth, world.halfHeight);
		for (var cluster = 0; cluster < clusterCount; cluster++)
		{
			var position = center + SampleEllipseOffset(horizontalRadius, verticalRadius, innerBias);
			var size = Random.Range(innerBias ? 4 : 3, innerBias ? 9 : 7);
			var chance = innerBias ? 0.95f : 0.9f;
			var chanceEnd = innerBias ? 0.45f : 0.15f;
			world.GenerateBlockCircle(position, size, tileIndex, chance, chanceEnd, false, false, false);
		}

		return 0;
	}

	private static bool TrySetGeneratedBlock(
		ushort[,] worldBlocks,
		int x,
		int y,
		ushort tileIndex,
		int width,
		int height)
	{
		if (x <= 0 || x >= width - 1 || y <= 0 || y >= height - 1)
		{
			return false;
		}

		if (worldBlocks[x, y] <= 0)
		{
			return false;
		}

		worldBlocks[x, y] = tileIndex;
		return true;
	}

	private static Vector2 SampleEllipseOffset(float horizontalRadius, float verticalRadius, bool innerBias)
	{
		var radius = innerBias
			? Mathf.Sqrt(Random.value) * 0.7f
			: Mathf.Lerp(0.72f, 1f, Mathf.Sqrt(Random.value));
		var angle = Random.Range(0f, Mathf.PI * 2f);
		return new Vector2(
			Mathf.Cos(angle) * horizontalRadius * radius,
			Mathf.Sin(angle) * verticalRadius * radius);
	}

	private static int GetAttempts(WorldGeneration world, float spawnAmount)
	{
		var oreAmount = WorldGeneration.GetRunSettingFloat("oreamount");
		var cellCount = (long)world.chunkWidth * world.chunkHeight;
		return Mathf.RoundToInt(cellCount / 2f * oreAmount * Mathf.Max(0f, spawnAmount));
	}
}
