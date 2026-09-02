using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Content;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Local rendering projection for mod-bound liquid tiles. The vanilla
/// <c>FluidManager.RenderFluids</c> creates one particle buffer per world-fluid
/// byte, so a custom byte outside the vanilla prefab array would be unsafe.
/// Instead of duplicating the game's particle-prefab arrays, this renderer
/// reuses the existing vanilla particle systems and maps each custom byte to an
/// authored base byte, applying per-particle tint/colour. It is a local
/// presentation-only projection: the authoritative grid still rides the CUO
/// fluid stream unchanged.
/// </summary>
internal sealed class LiquidTileRender(
	GameAdapterLiquidTileContentProvider liquidTileContent,
	ILogger<LiquidTileRender> log)
{
	private readonly GameAdapterLiquidTileContentProvider _liquidTileContent = liquidTileContent;
	private readonly ILogger<LiquidTileRender> _log = log;
	private bool _warnedNoParticles;

	/// <summary>
	/// Render when custom liquid tiles exist. Returns true when the caller must
	/// skip the original <c>RenderFluids</c> (custom content is present);
	/// returns false for an all-vanilla world so the original path stays intact.
	/// </summary>
	internal bool TryRender(FluidManager manager)
	{
		if (manager is null) // Unity object — ==
		{
			return false;
		}

		if (!_liquidTileContent.HasAny())
		{
			return false;
		}

		var world = WorldGeneration.world;
		if (world is null) // Unity object — ==
		{
			return false;
		}

		var particles = AccessTools.Field(typeof(FluidManager), "liquidParticles")?.GetValue(manager) as List<ParticleSystem>;
		if (particles is null || particles.Count == 0)
		{
			if (!_warnedNoParticles)
			{
				_warnedNoParticles = true;
				_log.LogWarning("[LiquidTileRender] FluidManager.liquidParticles is unavailable — custom liquid tiles will not be rendered.");
			}

			return true; // custom bytes exist; do not let the original array indexing throw
		}

		var range = manager.SimulationRange();
		var byPrefab = new List<ParticleSystem.Particle>[particles.Count];
		for (var i = 0; i < byPrefab.Length; i++)
		{
			byPrefab[i] = [];
		}

		for (var x = range.Item1.min; x < range.Item1.max; x++)
		{
			for (var y = range.Item2.min; y < range.Item2.max; y++)
			{
				var worldByte = manager.GetLiquid(x, y);
				if (worldByte == 0)
				{
					continue;
				}

				var prefabIndex = worldByte - 1;
				if (_liquidTileContent.TryGetVisualIndex(worldByte, out var customIndex))
				{
					prefabIndex = customIndex;
				}

				if (prefabIndex < 0 || prefabIndex >= byPrefab.Length)
				{
					continue;
				}

				var openTop = manager.GetLiquid(x, y + 1) == 0
					&& (manager.GetLiquid(x + 1, y) == 0 || manager.GetLiquid(x - 1, y) == 0);
				var color = _liquidTileContent.TryGetDisplayColor(worldByte, out var customColor)
					? customColor
					: Color.white;

				byPrefab[prefabIndex].Add(new ParticleSystem.Particle
				{
					position = world.BlockToWorldPos(new Vector2Int(x, y)) + (openTop ? new Vector2(0f, -0.3125f) : Vector2.zero),
					startLifetime = 999f,
					remainingLifetime = 999f,
					startColor = color,
					startSize3D = new Vector2(1.25f, openTop ? 0.625f : 1.25f)
				});
			}
		}

		for (var i = 0; i < particles.Count; i++)
		{
			particles[i].SetParticles(byPrefab[i].ToArray());
		}

		return true;
	}
}
