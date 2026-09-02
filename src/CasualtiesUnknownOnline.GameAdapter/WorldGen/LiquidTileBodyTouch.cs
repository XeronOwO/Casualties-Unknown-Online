using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Local per-frame body projection for mod-bound liquid tiles. Each player
/// simulates its own body effects on its own client (local compute, remote
/// verify/sync): the effect writes only the local body and uses the same
/// per-second authored rates as the CUCoreLib surface, but without game
/// delegates crossing Abstractions.
/// </summary>
internal sealed class LiquidTileBodyTouch(
	GameAdapterLiquidTileContentProvider liquidTileContent,
	ILogger<LiquidTileBodyTouch> log)
{
	private readonly GameAdapterLiquidTileContentProvider _liquidTileContent = liquidTileContent;
	private readonly ILogger<LiquidTileBodyTouch> _log = log;

	internal void Apply(Body body)
	{
		if (body is null || WorldGeneration.world is null || FluidManager.main is null) // Unity objects — ==
		{
			return;
		}

		if (PlayerCamera.main is null || body != PlayerCamera.main.body) // Unity objects — ==
		{
			return;
		}

		if (body.limbs is null || body.limbs.Length == 0 || body.limbs[0] is null)
		{
			return;
		}

		var pos = WorldGeneration.world.WorldToBlockPos(body.limbs[0].transform.position);
		var worldByte = FluidManager.main.GetLiquid(pos.x, pos.y);
		if (!_liquidTileContent.TryGetDefinitionByWorldByte(worldByte, out var definition))
		{
			return;
		}

		var dt = Mathf.Max(0f, Time.deltaTime);
		ApplyRates(body, definition, dt);
		_log.LogDebug("[LiquidTileTouch] liquid tile touch at=({X},{Y}) dt={Dt:F3}.", pos.x, pos.y, dt);
	}

	private static void ApplyRates(Body body, ModLiquidTileDefinition definition, float dt)
	{
		body.wetness = Mathf.Clamp(body.wetness + definition.WetnessPerSecond * dt, 0f, 100f);
		body.temperature += definition.TemperaturePerSecond * dt;
		body.sicknessAmount += definition.SicknessPerSecond * dt;
		body.dirtyness += definition.DirtynessPerSecond * dt;
		body.liquidSlipTime = Mathf.Clamp01(body.liquidSlipTime + definition.SlipPerSecond * dt);
		body.liquidRagdollBar = Mathf.Clamp01(body.liquidRagdollBar - definition.RagdollBarDrainPerSecond * dt);

		if (definition.DisinfectPerSecond != 0f && body.limbs is not null)
		{
			foreach (var limb in body.limbs)
			{
				if (limb is not null) // Unity object — ==
				{
					limb.SetDisinfect(Mathf.Max(limb.disinfectionTime, definition.DisinfectPerSecond * dt));
				}
			}
		}

		if (definition.PushBodies)
		{
			body.inWater = true;
		}
	}
}
