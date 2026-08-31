namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The local in-game console command surface of <see cref="IModContext"/>.
/// These commands are registered and executed only on the local process — no
/// host relay and no wire message. Register during <see cref="ICuoMod.Bind"/>.
/// Registration requires <see cref="ModPermission.RegisterCommand"/>; a
/// <see cref="CommandPermission.HostOnly"/> command additionally requires
/// <see cref="ModPermission.ExecuteHostAction"/>.
/// </summary>
public interface IModConsoleCommands
{
	/// <summary>
	/// Register a local console command. Returns false (with a framework log)
	/// when the mod lacks the required permissions, the definition/name is
	/// invalid, or the name is already registered by another command.
	/// </summary>
	bool Register(ModConsoleCommand command);

	/// <summary>True when this mod has already registered a command with this name.</summary>
	bool IsRegistered(string name);

	/// <summary>
	/// Unregister a command this mod previously registered. Returns false for
	/// unknown or foreign names; a foreign command is never removed.
	/// </summary>
	bool Unregister(string name);
}
