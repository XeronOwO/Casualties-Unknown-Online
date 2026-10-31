using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// What a command's handler is told when it runs: the registered name, the alias the player actually typed (empty for the name itself), the arguments after
/// the command word, and the caller's own identity and session snapshot.
/// </summary>
public interface ICommandInvocation
{
	/// <summary>The registered command name.</summary>
	string Name { get; }

	/// <summary>The alias that reached the command, or <c>null</c> when the player typed the name itself.</summary>
	string? Alias { get; }

	/// <summary>The arguments after the command word, unquoted, in order.</summary>
	IReadOnlyList<string> Arguments { get; }

	/// <summary>The local SteamId of the process that runs the handler.</summary>
	ulong LocalSteamId { get; }

	/// <summary>
	/// The SteamId of the player who ASKED for this run. It equals <see cref="LocalSteamId"/> for a command this process ran for its own player, and is the
	/// member's id when the host runs a <see cref="CommandScope.HostRelayed"/> command on that member's behalf — so a handler that says "you" means the player
	/// who typed, not the machine that executed.
	/// </summary>
	ulong RequesterSteamId { get; }

	/// <summary>The session state at execution time (not the bind-time snapshot).</summary>
	ISessionInfo Session { get; }
}
