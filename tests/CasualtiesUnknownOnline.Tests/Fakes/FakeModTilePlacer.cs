using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter tile placement seam. It lets
/// mod tile placement tests verify the permission/session/policy layer without
/// loading the game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModTilePlacer : IModTilePlacer
{
	private readonly List<(string TileId, int X, int Y)> _calls = [];

	public bool Result { get; set; } = true;

	public IReadOnlyList<(string TileId, int X, int Y)> Calls => _calls;

	public bool TryPlaceBlock(string tileId, int x, int y)
	{
		_calls.Add((tileId, x, y));
		return Result;
	}
}
