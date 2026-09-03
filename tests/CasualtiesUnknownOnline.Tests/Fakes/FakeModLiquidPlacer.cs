using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter liquid placement seam. It
/// lets mod liquid-placement tests verify the permission/session/policy layer
/// without loading the game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModLiquidPlacer : IModLiquidPlacer
{
	private readonly List<(string LiquidTileId, int X, int Y)> _placeCalls = [];
	private readonly List<(string LiquidTileId, int StartX, int StartY, int MaxFill)> _floodFillCalls = [];

	public bool Result { get; set; } = true;

	public IReadOnlyList<(string LiquidTileId, int X, int Y)> PlaceCalls => _placeCalls;

	public IReadOnlyList<(string LiquidTileId, int StartX, int StartY, int MaxFill)> FloodFillCalls => _floodFillCalls;

	public bool TryPlaceLiquid(string liquidTileId, int x, int y)
	{
		_placeCalls.Add((liquidTileId, x, y));
		return Result;
	}

	public bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill)
	{
		_floodFillCalls.Add((liquidTileId, startX, startY, maxFill));
		return Result;
	}
}
