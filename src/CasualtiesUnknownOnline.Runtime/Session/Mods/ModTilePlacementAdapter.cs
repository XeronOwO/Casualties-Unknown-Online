using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod tile placement adapter. Permission, session/world state and
/// request-shape checks happen here; the actual vanilla block write happens on
/// the other side of <see cref="IModTilePlacer"/>. This is a top-level
/// collaborator so <see cref="ModContext"/> stays under the architecture
/// line-count gate.
/// </summary>
internal sealed class ModTilePlacementAdapter(
	ModManifest manifest,
	SessionService session,
	IModTilePlacer tilePlacer,
	ILogger log) : IModTilePlacement
{
	public bool CanPlace => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

	public bool TryPlaceBlock(string tileId, int x, int y)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
		{
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPrefabId(tileId))
		{
			log.LogWarning("[Mods] {ModId} tried to place a tile with invalid id {TileId} — refused.",
				manifest.Id, tileId);
			return false;
		}

		if (!session.SessionActive || !session.LocalInWorld)
		{
			log.LogWarning("[Mods] {ModId} tried to place a tile outside an active in-world session — refused.",
				manifest.Id);
			return false;
		}

		if (!tilePlacer.TryPlaceBlock(tileId, x, y))
		{
			log.LogWarning("[Mods] {ModId} could not place tile {TileId} at ({X},{Y}) — the Game Adapter did not write a custom block.",
				manifest.Id, tileId, x, y);
			return false;
		}

		log.LogInformation("[Mods] {ModId} placed tile {TileId} at block ({X},{Y}).",
			manifest.Id, tileId, x, y);
		return true;
	}
}
