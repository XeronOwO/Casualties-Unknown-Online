using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// The GameAdapter liquid-tile runtime placement domain. It resolves a
/// mod-authored liquid-tile content id to its deterministic custom world-fluid
/// byte and calls the vanilla <c>FluidManager.SetLiquid</c> /
/// <c>StartFill</c> path. The world fluid grid is host-authoritative, so this
/// domain refuses guest calls; the host's existing fluid stream replicates the
/// grid write to every member viewport. No new wire message.
/// </summary>
internal sealed class LiquidTilePlacement(
	GameAdapterLiquidTileContentProvider liquidTileContent,
	ISessionControl session,
	ILogger<LiquidTilePlacement> log)
{
	private readonly GameAdapterLiquidTileContentProvider _liquidTileContent = liquidTileContent;
	private readonly ISessionControl _session = session;
	private readonly ILogger<LiquidTilePlacement> _log = log;

	internal bool TryPlaceLiquid(string liquidTileId, int x, int y)
	{
		var world = WorldGeneration.world;
		var fluid = FluidManager.main;
		if (world == null || fluid == null) // Unity objects — ==
		{
			_log.LogWarning("[LiquidPlacement] mod-requested liquid tile {Id} was refused because no world/fluid manager is active.", liquidTileId);
			return false;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("[LiquidPlacement] mod-requested liquid tile {Id} was refused on a guest — fluid placement is host-authoritative; use a host command.", liquidTileId);
			return false;
		}

		if (!_liquidTileContent.TryPrepareForWorldGen(liquidTileId, out var worldByte))
		{
			_log.LogWarning("[LiquidPlacement] mod-requested liquid tile {Id} is not a bound custom liquid tile — refused.", liquidTileId);
			return false;
		}

		var width = (int)world.width;
		var height = (int)world.height;
		if (x < 0 || y < 0 || x >= width || y >= height)
		{
			_log.LogWarning("[LiquidPlacement] mod-requested liquid tile {Id} at ({X},{Y}) is outside the world — refused.", liquidTileId, x, y);
			return false;
		}

		var blockPos = new Vector2Int(x, y);
		if (world.GetBlock(blockPos) != 0)
		{
			_log.LogWarning("[LiquidPlacement] mod-requested liquid tile {Id} at block ({X},{Y}) is not air — refused.", liquidTileId, x, y);
			return false;
		}

		fluid.SetLiquid(x, y, worldByte);
		_log.LogInformation("[LiquidPlacement] mod-requested liquid tile {Id} placed at block ({X},{Y}) — the host fluid stream will replicate it.", liquidTileId, x, y);
		return true;
	}

	internal bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill)
	{
		var world = WorldGeneration.world;
		var fluid = FluidManager.main;
		if (world == null || fluid == null) // Unity objects — ==
		{
			_log.LogWarning("[LiquidPlacement] mod-requested flood fill {Id} was refused because no world/fluid manager is active.", liquidTileId);
			return false;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_log.LogWarning("[LiquidPlacement] mod-requested flood fill {Id} was refused on a guest — fluid placement is host-authoritative; use a host command.", liquidTileId);
			return false;
		}

		if (!_liquidTileContent.TryPrepareForWorldGen(liquidTileId, out var worldByte))
		{
			_log.LogWarning("[LiquidPlacement] mod-requested flood fill {Id} is not a bound custom liquid tile — refused.", liquidTileId);
			return false;
		}

		var width = (int)world.width;
		var height = (int)world.height;
		if (startX < 0 || startY < 0 || startX >= width || startY >= height)
		{
			_log.LogWarning("[LiquidPlacement] mod-requested flood fill {Id} from ({X},{Y}) is outside the world — refused.", liquidTileId, startX, startY);
			return false;
		}

		var effectiveMaxFill = maxFill;
		if (effectiveMaxFill <= 0 && _liquidTileContent.TryGetDefinition(liquidTileId, out var definition))
		{
			effectiveMaxFill = definition.MaxFloodFill;
		}

		if (effectiveMaxFill <= 0)
		{
			effectiveMaxFill = 1;
		}

		fluid.StartFill(new Vector2Int(startX, startY), worldByte, effectiveMaxFill);
		_log.LogInformation("[LiquidPlacement] mod-requested flood fill {Id} started at block ({X},{Y}) maxFill {MaxFill} — the host fluid stream will replicate it.", liquidTileId, startX, startY, effectiveMaxFill);
		return true;
	}
}
