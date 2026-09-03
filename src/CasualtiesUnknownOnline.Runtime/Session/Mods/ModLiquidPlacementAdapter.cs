using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod liquid placement adapter. Permission, session/world state and
/// request-shape checks happen here; the actual vanilla fluid writes happen on
/// the other side of <see cref="IModLiquidPlacer"/>. This is a top-level
/// collaborator so <see cref="ModContext"/> stays under the architecture
/// line-count gate.
/// </summary>
internal sealed class ModLiquidPlacementAdapter(
	ModManifest manifest,
	SessionService session,
	IModLiquidPlacer liquidPlacer,
	ILogger log) : IModLiquidPlacement
{
	public bool CanPlace => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

	public bool TryPlaceLiquid(string liquidTileId, int x, int y)
	{
		if (!TryCheckCommon(liquidTileId, "liquid tile"))
		{
			return false;
		}

		if (!liquidPlacer.TryPlaceLiquid(liquidTileId, x, y))
		{
			log.LogWarning("[Mods] {ModId} could not place liquid tile {TileId} at ({X},{Y}) — the Game Adapter did not write a custom fluid cell.",
				manifest.Id, liquidTileId, x, y);
			return false;
		}

		log.LogInformation("[Mods] {ModId} placed liquid tile {TileId} at block ({X},{Y}).",
			manifest.Id, liquidTileId, x, y);
		return true;
	}

	public bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill)
	{
		if (!TryCheckCommon(liquidTileId, "flood fill"))
		{
			return false;
		}

		if (!liquidPlacer.TryFloodFill(liquidTileId, startX, startY, maxFill))
		{
			log.LogWarning("[Mods] {ModId} could not flood-fill liquid tile {TileId} from ({X},{Y}) maxFill {MaxFill} — the Game Adapter did not start the fill.",
				manifest.Id, liquidTileId, startX, startY, maxFill);
			return false;
		}

		log.LogInformation("[Mods] {ModId} flood-filled liquid tile {TileId} from block ({X},{Y}) maxFill {MaxFill}.",
			manifest.Id, liquidTileId, startX, startY, maxFill);
		return true;
	}

	private bool TryCheckCommon(string liquidTileId, string operation)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
		{
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPrefabId(liquidTileId))
		{
			log.LogWarning("[Mods] {ModId} tried to {Operation} with invalid id {TileId} — refused.",
				manifest.Id, operation, liquidTileId);
			return false;
		}

		if (!session.SessionActive || !session.LocalInWorld)
		{
			log.LogWarning("[Mods] {ModId} tried to {Operation} outside an active in-world session — refused.",
				manifest.Id, operation);
			return false;
		}

		return true;
	}
}
