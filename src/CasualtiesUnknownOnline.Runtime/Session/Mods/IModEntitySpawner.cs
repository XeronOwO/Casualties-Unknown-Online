namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for mod entity spawning. The Runtime
/// defines the contract (plus permission/session policy); the Game Adapter
/// knows how to call <c>Utils.Create</c> and verify the resulting
/// <c>BuildingEntity</c>. The actual replication still rides the existing
/// runtime-entity-channel (<c>EntitySpawned</c>) through the normal
/// <c>BuildingEntity.Start</c> report path — this seam only creates the local
/// copy on behalf of the mod.
/// </summary>
public interface IModEntitySpawner
{
	/// <summary>
	/// Create a game world-entity prefab at the given position/rotation.
	/// Returns true only when the prefab was created and carries a
	/// <c>BuildingEntity</c> (the entity-domain sync surface). The caller
	/// (ModService) is responsible for permission/session/policy gating.
	/// </summary>
	bool TrySpawnEntity(string prefabId, float x, float y, float rotation);
}
