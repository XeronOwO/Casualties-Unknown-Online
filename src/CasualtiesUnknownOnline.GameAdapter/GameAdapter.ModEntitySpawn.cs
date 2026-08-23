using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The mod entity-spawn half of <see cref="GameAdapter"/> (Phase 4 Mod API
/// remainder). This is the thin Runtime boundary: it forwards a
/// permission/session-validated mod spawn request to
/// <see cref="EntitySpawnSync.TrySpawnFromMod"/>.
/// </summary>
public sealed partial class GameAdapter : IModEntitySpawner
{
	bool IModEntitySpawner.TrySpawnEntity(string prefabId, float x, float y, float rotation) =>
		_entitySpawnSync.TrySpawnFromMod(prefabId, x, y, rotation);
}
