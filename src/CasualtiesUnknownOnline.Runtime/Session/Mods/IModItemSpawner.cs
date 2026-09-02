namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for mod item spawning. The Runtime
/// defines the contract (plus permission/session policy); the Game Adapter
/// knows how to call <c>Utils.Create</c> and verify the resulting
/// <c>Item</c>. The actual replication rides the existing item-domain
/// <c>ItemSpawned</c> channel through the normal <c>Item.Start</c> report path —
/// this seam only creates the local copy on behalf of the mod.
/// </summary>
public interface IModItemSpawner
{
	/// <summary>
	/// Create one world-item prefab at the given position/rotation. Returns true
	/// only when the prefab was created and carries an <c>Item</c>. The caller
	/// (ModService) is responsible for permission/session/policy gating.
	/// </summary>
	bool TrySpawnItem(string itemId, float x, float y, float rotation);
}
