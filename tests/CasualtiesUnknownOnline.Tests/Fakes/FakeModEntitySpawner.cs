using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter entity-spawn seam. It lets
/// mod entity-spawn tests verify the permission/session/policy layer without
/// loading the game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModEntitySpawner : IModEntitySpawner
{
	private readonly List<(string PrefabId, float X, float Y, float Rotation)> _calls = [];

	public bool Result { get; set; } = true;

	public IReadOnlyList<(string PrefabId, float X, float Y, float Rotation)> Calls => _calls;

	public bool TrySpawnEntity(string prefabId, float x, float y, float rotation)
	{
		_calls.Add((prefabId, x, y, rotation));
		return Result;
	}
}
