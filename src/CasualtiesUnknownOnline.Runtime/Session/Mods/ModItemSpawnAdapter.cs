using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod item-spawn adapter. Permission, session/world state and
/// request-shape checks happen here; the actual item creation happens on the
/// other side of <see cref="IModItemSpawner"/>.
/// </summary>
internal sealed class ModItemSpawnAdapter(ModManifest manifest, SessionService session, IModItemSpawner itemSpawner, ILogger log) : IModItemSpawn
{
	public bool CanSpawn => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

	public bool TrySpawn(string itemId, float x, float y, float rotation)
	{
		if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
		{
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPrefabId(itemId))
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an item with an invalid id {ItemId} — refused.",
				manifest.Id, itemId);
			return false;
		}

		if (!ModEntitySpawnPolicy.IsValidPosition(x, y) || !ModEntitySpawnPolicy.IsValidRotation(rotation))
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an item with a non-finite position/rotation — refused.",
				manifest.Id);
			return false;
		}

		if (!session.SessionActive || !session.LocalInWorld)
		{
			log.LogWarning("[Mods] {ModId} tried to spawn an item outside an active in-world session — refused.",
				manifest.Id);
			return false;
		}

		if (!itemSpawner.TrySpawnItem(itemId, x, y, rotation))
		{
			log.LogWarning("[Mods] {ModId} could not spawn item {ItemId} at ({X:F1},{Y:F1}) — the Game Adapter did not create an Item.",
				manifest.Id, itemId, x, y);
			return false;
		}

		log.LogInformation("[Mods] {ModId} spawned item {ItemId} at ({X:F1},{Y:F1}) rotation {Rotation:F1}.",
			manifest.Id, itemId, x, y, rotation);
		return true;
	}
}
