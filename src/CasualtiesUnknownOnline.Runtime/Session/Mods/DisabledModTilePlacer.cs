namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModTilePlacer"/> in the Runtime-only composition:
/// tests and non-game hosts do not have a Game Adapter seam wired, so a
/// placement request is refused instead of touching a missing adapter
/// implementation. The production plugin replaces this registration with the
/// Game Adapter's real tile placer through <c>extraRegistrations</c>.
/// </summary>
internal sealed class DisabledModTilePlacer : IModTilePlacer
{
	public bool TryPlaceBlock(string tileId, int x, int y) => false;
}
