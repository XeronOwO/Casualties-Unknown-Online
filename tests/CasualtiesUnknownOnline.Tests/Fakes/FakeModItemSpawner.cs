using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter item-spawn seam. It lets
/// mod item-spawn tests verify the permission/session/policy layer without
/// loading the game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModItemSpawner : IModItemSpawner
{
	private readonly List<(string ItemId, float X, float Y, float Rotation)> _calls = [];

	public bool Result { get; set; } = true;

	public IReadOnlyList<(string ItemId, float X, float Y, float Rotation)> Calls => _calls;

	public bool TrySpawnItem(string itemId, float x, float y, float rotation)
	{
		_calls.Add((itemId, x, y, rotation));
		return Result;
	}
}
