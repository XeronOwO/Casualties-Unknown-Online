namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModStructurePlacer"/> in the Runtime-only composition:
/// tests and non-game hosts do not have a Game Adapter seam wired, so a
/// structure placement request is refused instead of touching a missing adapter
/// implementation. The production plugin replaces this registration with the
/// Game Adapter's real structure placer through <c>extraRegistrations</c>.
/// </summary>
internal sealed class DisabledModStructurePlacer : IModStructurePlacer
{
	public bool TryPlaceStructure(string structureId, int originX, int originY) => false;
}
