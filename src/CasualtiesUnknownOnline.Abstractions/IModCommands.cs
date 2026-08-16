using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The command surface of <see cref="IModContext"/>. Commands are
/// host-authoritative: registration succeeds on every side, but the handler
/// EXECUTES ONLY on the host's copy of the mod. A guest's
/// <see cref="TryExecute"/> sends a request to the host; a host's call runs
/// locally and completes its callback synchronously. The framework checks the
/// declared permissions (<see cref="ModPermission.RegisterCommand"/> and, for
/// host actions, <see cref="ModPermission.ExecuteHostAction"/>) and the
/// request/result shape caps — the mod's handler remains the semantic
/// validator and can authorize per-guest behavior from the requester id.
/// </summary>
public interface IModCommands
{
	/// <summary>
	/// Register a command for this mod id. Returns false (with a framework log)
	/// when the mod lacks <see cref="ModPermission.RegisterCommand"/>, when
	/// <see cref="ModCommand.IsHostAction"/> demands a missing
	/// <see cref="ModPermission.ExecuteHostAction"/>, or when the definition/name
	/// is invalid or duplicated. Register during <see cref="ICuoMod.Bind"/>.
	/// </summary>
	bool Register(ModCommand command);

	/// <summary>True when a command with this exact name is registered locally.</summary>
	bool IsRegistered(string name);

	/// <summary>
	/// Execute a command. Host: runs the local copy synchronously and invokes
	/// <paramref name="callback"/> before returning. Guest: sends a request to
	/// the host and invokes the callback when the directed result arrives (the
	/// request/result channel is reliable; delivery may be synchronous in tests
	/// or later on the next receive batch). Returns false immediately when the
	/// command/arguments are invalid or the request cannot be sent (outside a
	/// session, wrong role, over the shape caps) — in that case the callback is
	/// not invoked. A pending guest callback whose session ends is settled with
	/// a failure result.
	/// </summary>
	bool TryExecute(string name, IReadOnlyList<string> arguments, Action<IModCommandResult> callback);
}
