namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The result of one command execution, delivered to the callback passed to
/// <see cref="IModCommands.TryExecute"/>. Host-local calls complete
/// synchronously; a guest's call completes when the host's directed result
/// arrives (or with <see cref="Success"/> false when the session ends first).
/// </summary>
public interface IModCommandResult
{
	/// <summary>The framework-assigned request id (correlates request and result).</summary>
	uint RequestId { get; }

	/// <summary>The command name that executed.</summary>
	string Name { get; }

	/// <summary>The member that requested the command (the host's own SteamId for a host-local call).</summary>
	ulong RequesterSteamId { get; }

	/// <summary>True when the handler returned normally; false when it threw or the request was refused.</summary>
	bool Success { get; }

	/// <summary>The handler's returned text, null when it returned null.</summary>
	string? Output { get; }

	/// <summary>The failure reason (exception message / refusal), null on success.</summary>
	string? Error { get; }
}
