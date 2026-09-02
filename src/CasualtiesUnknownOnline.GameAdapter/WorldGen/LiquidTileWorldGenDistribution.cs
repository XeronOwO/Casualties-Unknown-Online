using System;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Automatic world-generation distribution for mod-bound liquid tiles. It is
/// called from the vanilla <c>WorldGeneration.GenerateOres</c> postfix, so all
/// <c>UnityEngine.Random</c> consumption lands inside the sealed generation
/// stream: both sides generate the same custom fluid pools from the same
/// baseline. Placement calls the game's existing <c>PlaceLiquids</c> /
/// <c>FluidManager.StartFill</c> path and writes the same byte grid the CUO
/// fluid domain already streams. No wire message and no JObject snapshot.
/// </summary>
internal sealed class LiquidTileWorldGenDistribution(
	GameAdapterLiquidTileContentProvider liquidTileContent,
	ILogger<LiquidTileWorldGenDistribution> log)
{
	private readonly GameAdapterLiquidTileContentProvider _liquidTileContent = liquidTileContent;
	private readonly ILogger<LiquidTileWorldGenDistribution> _log = log;

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

		if (FluidManager.main == null) // Unity object — ==
		{
			_log.LogWarning("[LiquidTileWorldGen] FluidManager.main is unavailable — no custom liquid tiles distributed.");
			return;
		}

		var definitions = _liquidTileContent.GetDefinitionsForWorldGen();
		if (definitions.Count == 0)
		{
			return;
		}

		var placed = 0;
		var skipped = 0;
		foreach (var pair in definitions)
		{
			if (pair.Value.SpawnAmount <= 0f || !pair.Value.CanSpawnInLayer(world.biomeDepth))
			{
				continue;
			}

			if (!_liquidTileContent.TryPrepareForWorldGen(pair.Key, out var worldByte))
			{
				skipped++;
				_log.LogWarning("[LiquidTileWorldGen] {Id} could not be prepared for world generation — no pools distributed.", pair.Key);
				continue;
			}

			world.PlaceLiquids(pair.Value.SpawnAmount, worldByte, Math.Max(1, pair.Value.MaxFloodFill));
			placed++;
			_log.LogDebug("[LiquidTileWorldGen] {Id} distribution started (byte {WorldByte}, amount {Amount}, maxFill {MaxFill}).",
				pair.Key, worldByte, pair.Value.SpawnAmount, pair.Value.MaxFloodFill);
		}

		if (placed > 0 || skipped > 0)
		{
			_log.LogInformation(
				"[LiquidTileWorldGen] distribution complete on depth {Depth}: {Placed} liquid tile(s) placed, {Skipped} skipped.",
				world.biomeDepth, placed, skipped);
		}
	}
}
