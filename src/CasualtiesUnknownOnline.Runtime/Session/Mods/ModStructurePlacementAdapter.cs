using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod structure placement adapter. Permission, session/world state and
/// request-shape checks happen here; the actual multi-block world writes happen
/// on the other side of <see cref="IModStructurePlacer"/>. This is a top-level
/// collaborator so <see cref="ModContext"/> stays under the architecture
/// line-count gate.
/// </summary>
internal sealed class ModStructurePlacementAdapter(
	ModManifest manifest,
	SessionService session,
	IModStructurePlacer structurePlacer,
	ILogger log) : IModStructurePlacement
{
	public bool CanPlace => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

	public bool TryPlaceStructure(string structureId, int originX, int originY)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
		{
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPrefabId(structureId))
		{
			log.LogWarning("[Mods] {ModId} tried to place a structure with invalid id {StructureId} — refused.",
				manifest.Id, structureId);
			return false;
		}

		if (!session.SessionActive || !session.LocalInWorld)
		{
			log.LogWarning("[Mods] {ModId} tried to place a structure outside an active in-world session — refused.",
				manifest.Id);
			return false;
		}

		if (!structurePlacer.TryPlaceStructure(structureId, originX, originY))
		{
			log.LogWarning("[Mods] {ModId} could not place structure {StructureId} at ({X},{Y}) — the Game Adapter did not write the full structure.",
				manifest.Id, structureId, originX, originY);
			return false;
		}

		log.LogInformation("[Mods] {ModId} placed structure {StructureId} at block ({X},{Y}).",
			manifest.Id, structureId, originX, originY);
		return true;
	}
}
