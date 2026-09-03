namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The default <see cref="IModLiquidPlacer"/> in the Runtime-only composition:
/// tests and non-game hosts do not have a Game Adapter seam wired, so a
/// liquid placement request is refused instead of touching a missing adapter
/// implementation. The production plugin replaces this registration with the
/// Game Adapter's real liquid placer through <c>extraRegistrations</c>.
/// </summary>
internal sealed class DisabledModLiquidPlacer : IModLiquidPlacer
{
	public bool TryPlaceLiquid(string liquidTileId, int x, int y) => false;

	public bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill) => false;
}
