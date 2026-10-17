using CasualtiesUnknownOnline.Runtime.Session.Permissions;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The control surface used by the cheat-roster announcement handler: the session role the announcement is valid under, and the member-side store it writes.</summary>
public interface ICheatPermissionHandlerContext
{
	ISessionControl Session { get; }

	/// <summary>The host's cheat-permission roster as this client last received it.</summary>
	AnnouncedCheatPermissions CheatPermissions { get; }
}
