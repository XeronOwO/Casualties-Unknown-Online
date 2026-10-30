namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The console command surface of <see cref="IModContext"/>: the mod registers definitions while the game runs, and the SAME definition shape is what a
/// built-in command produces. There is no second dialect.
/// </summary>
/// <remarks>
/// Registration requires <see cref="ModPermission.RegisterLocalCommand"/>; a <see cref="CommandPermission.HostOnly"/> command additionally requires
/// <see cref="ModPermission.ExecuteHostAction"/>, and a <see cref="CommandScope.HostRelayed"/> command is registered for the relay as well (the framework
/// carries the request; the host validates it against its own rules). The mod must be installed on both sides for a relaid command, because the host has to
/// know the definition to validate and run it.
/// </remarks>
public interface IModConsoleCommands
{
	/// <summary>
	/// Register a console command. Returns false (with a framework log) when the mod lacks the required permissions, the definition/name/alias is invalid, or
	/// a name/alias is already registered.
	/// </summary>
	bool Register(ICommandDefinition command);

	/// <summary>True when this mod has already registered a command with this name (or alias).</summary>
	bool IsRegistered(string name);

	/// <summary>
	/// Unregister a command this mod previously registered, by its name or one of its aliases. Returns false for unknown or foreign names; a foreign command
	/// is never removed.
	/// </summary>
	bool Unregister(string name);
}
