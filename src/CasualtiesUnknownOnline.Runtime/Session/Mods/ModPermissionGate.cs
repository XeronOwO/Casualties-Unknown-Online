using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The shared permission check used by every per-mod API adapter. The actual
/// permission bits live on the mod manifest; this class keeps the bit test and
/// the "missing permission" log in one place instead of duplicating them in
/// each adapter.
/// </summary>
internal static class ModPermissionGate
{
	public static bool HasPermission(LoadedMod mod, ModPermission permission) =>
		(mod.Manifest.Permissions & permission) == permission;

	public static bool HasPermission(ModManifest manifest, ModPermission permission) =>
		(manifest.Permissions & permission) == permission;

	public static bool Try(ILogger log, ModManifest manifest, ModPermission permission)
	{
		if (HasPermission(manifest, permission))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId} does not declare {Permission} — the call is refused.", manifest.Id, permission);
		return false;
	}
}
