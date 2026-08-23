namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModEntitySpawner"/> in the Runtime-only composition:
/// tests and non-game hosts do not have a Game Adapter seam wired, so a spawn
/// request is refused instead of touching a missing adapter implementation.
/// The production plugin replaces this registration with the Game Adapter's
/// real spawner through <c>extraRegistrations</c>.
/// </summary>
internal sealed class DisabledModEntitySpawner : IModEntitySpawner
{
	public bool TrySpawnEntity(string prefabId, float x, float y, float rotation) => false;
}
