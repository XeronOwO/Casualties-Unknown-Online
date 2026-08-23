using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Mod entity-spawn half of <see cref="ModService"/> (Phase 4 Mod API
/// remainder). Each mod gets a permission-gated spawn adapter: the framework
/// validates the request and forwards it to the Game Adapter's
/// <see cref="IModEntitySpawner"/> seam, which creates the local
/// <c>BuildingEntity</c> copy. The existing runtime-entity channel then
/// replicates the creation to the peers exactly like any other runtime spawn —
/// no new wire message is needed.
/// </summary>
public sealed partial class ModService
{
	/// <summary>
	/// The per-mod entity-spawn adapter. Permission, session/world state and
	/// request-shape checks happen here; the actual prefab creation happens on
	/// the other side of <see cref="IModEntitySpawner"/>.
	/// </summary>
	private sealed class ModEntitySpawnAdapter(ModService owner, ModManifest manifest) : IModEntitySpawn
	{
		public bool CanSpawn => HasPermission(manifest, ModPermission.SpawnEntity);

		public bool TrySpawn(string prefabId, float x, float y, float rotation)
		{
			if (!CanSpawn)
			{
				owner.LogMissingPermission(manifest.Id, "SpawnEntity");
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPrefabId(prefabId))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to spawn an entity with an invalid prefab id {PrefabId} — refused.",
					manifest.Id, prefabId);
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPosition(x, y) || !ModEntitySpawnPolicy.IsValidRotation(rotation))
			{
				owner._log.LogWarning("[Mods] {ModId} tried to spawn an entity with a non-finite position/rotation — refused.",
					manifest.Id);
				return false;
			}

			if (!owner._session.SessionActive || !owner._session.LocalInWorld)
			{
				owner._log.LogWarning("[Mods] {ModId} tried to spawn an entity outside an active in-world session — refused.",
					manifest.Id);
				return false;
			}

			if (!owner._entitySpawner.TrySpawnEntity(prefabId, x, y, rotation))
			{
				owner._log.LogWarning("[Mods] {ModId} could not spawn entity {PrefabId} at ({X:F1},{Y:F1}) — the Game Adapter did not create a BuildingEntity.",
					manifest.Id, prefabId, x, y);
				return false;
			}

			owner._log.LogInformation("[Mods] {ModId} spawned entity {PrefabId} at ({X:F1},{Y:F1}) rotation {Rotation:F1}.",
				manifest.Id, prefabId, x, y, rotation);
			return true;
		}
	}
}
