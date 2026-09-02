namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModItemSpawner"/> in the Runtime-only composition
/// (no Game Adapter). It is replaced by the real Game Adapter implementation
/// when the plugin registers its adapter services.
/// </summary>
internal sealed class DisabledModItemSpawner : IModItemSpawner
{
	public bool TrySpawnItem(string itemId, float x, float y, float rotation) => false;
}
