namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod entity-spawn surface (Phase 4 Mod API remainder). A synchronized or
/// authoritative mod can ask the framework to spawn a world entity prefab at a
/// runtime position; CUO then reuses the existing runtime-entity channel
/// (EntitySpawned) so every side creates the same entity at the same place.
/// The mod never touches Unity or game-assembly types.
///
/// Spawning requires <see cref="ModPermission.SpawnEntity"/>: nothing is
/// implicit, and the permission policy already refuses that flag on local-only
/// network modes. The surface supports existing game <c>BuildingEntity</c>
/// prefabs (identified by the game's prefab id), and a shared-content mod can
/// additionally register a custom building definition through
/// <see cref="IModContent"/> so the same prefab id materializes a runtime
/// building template. It is not a generic custom-component/payload injection
/// surface.
/// </summary>
public interface IModEntitySpawn
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.SpawnEntity"/>.
	/// Every spawn method also checks and logs this before acting.
	/// </summary>
	bool CanSpawn { get; }

	/// <summary>
	/// Try to spawn a world entity prefab at the given world position and
	/// rotation. Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.SpawnEntity"/>, the session is not active or
	/// the local player is not in a world, the prefab id / position fails the
	/// spawn policy rails, or the Game Adapter cannot create the requested
	/// building-entity prefab.
	/// </summary>
	bool TrySpawn(string prefabId, float x, float y, float rotation);
}
