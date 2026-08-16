using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The invocation surface a mod's command handler sees. Host-authoritative:
/// the handler runs ONLY on the host's copy of the mod — a guest's
/// <see cref="IModCommands.TryExecute"/> is a request that the host executes.
/// The handler validates its own semantic parameters here (the framework has
/// already validated the shape caps and the mod's permissions) and can
/// authorize per-guest behavior from <see cref="RequesterSteamId"/>.
/// </summary>
public interface IModCommandContext
{
	/// <summary>The command name (the registered spelling).</summary>
	string Name { get; }

	/// <summary>The command arguments, exactly as the requester supplied them.</summary>
	IReadOnlyList<string> Arguments { get; }

	/// <summary>The member that requested the command (the host's own SteamId for a host-local call).</summary>
	ulong RequesterSteamId { get; }

	/// <summary>The session state AT EXECUTION TIME (not the bind-time snapshot — a command may run in a later session).</summary>
	ISessionInfo Session { get; }

	/// <summary>The mod-scoped logger (the mod id source).</summary>
	ILogger Logger { get; }
}
