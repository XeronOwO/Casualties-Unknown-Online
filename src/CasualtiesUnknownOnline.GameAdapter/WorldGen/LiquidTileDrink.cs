using CasualtiesUnknownOnline.GameAdapter.Content;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Local drink handling for custom liquid-tile world bytes. The acting client
/// applies the full drink effect through the logical <c>LiquidType</c>, clears
/// the cell when the definition asks for it, and reports through the existing
/// <c>FluidDrinkPatch</c> postfix — the CUO fluid interaction domain stays the
/// only sync path.
/// </summary>
internal sealed class LiquidTileDrink(
	GameAdapterLiquidTileContentProvider liquidTileContent,
	ILogger<LiquidTileDrink> log)
{
	private readonly GameAdapterLiquidTileContentProvider _liquidTileContent = liquidTileContent;
	private readonly ILogger<LiquidTileDrink> _log = log;

	internal bool TryDrink(FluidManager fluid, Vector2Int pos, Body body)
	{
		if (fluid is null || body is null || WorldGeneration.world is null) // Unity objects — ==
		{
			return false;
		}

		var worldByte = fluid.GetLiquid(pos.x, pos.y);
		if (!_liquidTileContent.TryGetDrinkLiquid(worldByte, out var liquidType))
		{
			return false;
		}

		if (_liquidTileContent.TryGetDefinitionByWorldByte(worldByte, out var definition)
			&& definition.ConsumeOnDrink)
		{
			fluid.fluid[pos.x, pos.y] = 0;
		}

		liquidType.onDrink(200f, body);
		Sound.Play("drink", body.transform.position, false, true, null, 1f, 1f, false, false);
		_log.LogInformation("[LiquidTileDrink] drank custom liquid at=({X},{Y}) type={Type}.", pos.x, pos.y, worldByte);
		return true;
	}
}
