namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod item-spawn surface. It lets a synchronized or authoritative mod ask
/// the framework to create one world-item prefab at a runtime position. CUO
/// reuses the existing item-domain channel (<c>ItemSpawned</c>) so every side
/// creates the same item at the same place; the mod never touches Unity or
/// game-assembly types.
///
/// The surface uses the same <see cref="ModPermission.SpawnEntity"/> gate as
/// <see cref="IModEntitySpawn"/>. It supports both vanilla item prefabs and any
/// item loaded through the custom item content provider.
/// </summary>
public interface IModItemSpawn
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.SpawnEntity"/>.
	/// Every spawn method also checks and logs this before acting.
	/// </summary>
	bool CanSpawn { get; }

	/// <summary>
	/// Try to spawn one world-item prefab at the given world position and
	/// rotation. Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.SpawnEntity"/>, the session is not active or the
	/// local player is not in a world, the item id / position / rotation fails
	/// the policy rails, or the Game Adapter cannot create the requested item.
	/// </summary>
	bool TrySpawn(string itemId, float x, float y, float rotation);
}
