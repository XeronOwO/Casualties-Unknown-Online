namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod liquid-tile placement surface. It lets a synchronized or
/// authoritative mod place one custom world-liquid cell or start a flood fill
/// for a custom liquid tile registered through <see cref="IModContent"/>
/// (<see cref="ModContentKind.LiquidTile"/>) at integer block coordinates. The
/// Game Adapter resolves the stable content id to its deterministic custom
/// world-fluid byte and calls the vanilla <c>FluidManager.SetLiquid</c> /
/// <c>StartFill</c> path; the mod never touches Unity or game-assembly types.
///
/// Placement requires <see cref="ModPermission.SpawnEntity"/> — the same
/// host/state permission family as entity, item, tile and structure placement.
/// The world fluid grid is host-authoritative in CUO: the host simulates the
/// grid alone and streams each guest's viewport through the existing
/// <c>FluidRegion</c> channel. This surface therefore writes only on the
/// host/solo copy; a guest call is refused with a framework log so the
/// streamed grid never has a divergent local write. Guest-initiated placement
/// should be requested through the mod-host command domain
/// (<see cref="IModCommands"/>), which already provides host-authoritative
/// execution.
/// </summary>
public interface IModLiquidPlacement
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.SpawnEntity"/>.
	/// Every placement call also checks and logs this before acting.
	/// </summary>
	bool CanPlace { get; }

	/// <summary>
	/// Try to place one custom liquid-tile cell at integer block coordinates.
	/// Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.SpawnEntity"/>, the session is not active or the
	/// local player is not in a world, the liquid-tile id fails the request
	/// rails, the definition/byte mapping is unknown/failed, the target cell is
	/// outside the world or on a solid block, or this process is a guest
	/// (fluid placement is host-authoritative).
	/// </summary>
	bool TryPlaceLiquid(string liquidTileId, int x, int y);

	/// <summary>
	/// Try to start a flood fill of one custom liquid-tile definition from an
	/// integer block seed. Returns false (with a framework log) for the same
	/// permission/session/id/guest failure set as
	/// <see cref="TryPlaceLiquid"/>, or when the Game Adapter cannot start the
	/// fill. A <paramref name="maxFill"/> value less than or equal to zero uses
	/// the definition's authored <c>MaxFloodFill</c> cap.
	/// </summary>
	bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill);
}
