using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod entity-spawn adapter. Permission, session/world state and
/// request-shape checks happen here; the actual prefab creation happens on
/// the other side of <see cref="IModEntitySpawner"/>.
/// </summary>
internal sealed class ModEntitySpawnAdapter(ModManifest manifest, SessionService session, IModEntitySpawner entitySpawner, ILogger log) : IModEntitySpawn
{
	public bool CanSpawn => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

	public bool TrySpawn(string prefabId, float x, float y, float rotation)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
		{
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPrefabId(prefabId))
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an entity with an invalid prefab id {PrefabId} — refused.",
				manifest.Id, prefabId);
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPosition(x, y) || !ModEntitySpawnPolicy.IsValidRotation(rotation))
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an entity with a non-finite position/rotation — refused.",
				manifest.Id);
			return false;
		}

		if (!session.SessionActive || !session.LocalInWorld)
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an entity outside an active in-world session — refused.",
				manifest.Id);
			return false;
		}

		if (!entitySpawner.TrySpawnEntity(prefabId, x, y, rotation))
		{
			log.LogWarning("[Mods] {ModId} could not spawn entity {PrefabId} at ({X:F1},{Y:F1}) — the Game Adapter did not create a BuildingEntity.",
				manifest.Id, prefabId, x, y);
			return false;
		}

		log.LogInformation("[Mods] {ModId} spawned entity {PrefabId} at ({X:F1},{Y:F1}) rotation {Rotation:F1}.",
			manifest.Id, prefabId, x, y, rotation);
		return true;
	}
}
