using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The invocation surface a mod's local console-command handler sees. The
/// command stays local, so the context carries the local member's own identity
/// and a current session snapshot — no host round-trip, no wire protocol.
/// </summary>
public interface IModConsoleCommandContext
{
	/// <summary>The command name (the registered spelling).</summary>
	string Name { get; }

	/// <summary>The command arguments, excluding the command name.</summary>
	IReadOnlyList<string> Arguments { get; }

	/// <summary>The local SteamId of the process executing the command.</summary>
	ulong LocalSteamId { get; }

	/// <summary>The session state at execution time (not the bind-time snapshot).</summary>
	ISessionInfo Session { get; }
}
