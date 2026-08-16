using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure permission judge for the mod domain: the declared
/// <see cref="ModPermission"/> flags must only contain defined bits, and
/// host/state permissions are refused on local-only network modes
/// (architecture.md §5.3: client-only mods must not register sync objects;
/// commands execute on the host, so ClientOnly/Cosmetic cannot own them).
/// Discovery validates the local manifest with this; the handshake validates
/// the guest's declared flags with the same policy before admitting it.
/// </summary>
public static class ModPermissionPolicy
{
	private const ModPermission HostOrStatePermissions =
		ModPermission.WriteGameState | ModPermission.SpawnEntity | ModPermission.RegisterContent
		| ModPermission.RegisterCommand | ModPermission.ExecuteHostAction;

	/// <summary>True when every bit is part of <see cref="ModPermission.All"/> (None is valid).</summary>
	public static bool IsDefined(ModPermission permissions) =>
		(permissions & ~ModPermission.All) == 0;

	/// <summary>
	/// True when the permission set is valid for the network mode. Unknown bits
	/// are always invalid; ClientOnly/Cosmetic additionally reject host/state
	/// permissions. <see cref="NetworkMode.Unspecified"/> is rejected by the
	/// caller (the mode itself is invalid), not here.
	/// </summary>
	public static bool IsValidFor(NetworkMode mode, ModPermission permissions)
	{
		if (!IsDefined(permissions))
		{
			return false;
		}

		return mode is not (NetworkMode.ClientOnly or NetworkMode.Cosmetic)
			|| (permissions & HostOrStatePermissions) == 0;
	}
}
