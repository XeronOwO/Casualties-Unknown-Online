using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Host-command half of <see cref="ModService"/> (Phase 4b). Command
/// execution is host-authoritative: a guest request is validated against the
/// session membership and the host's loaded mod/permission/registration
/// state, then executed on the HOST's copy of the mod and answered with a
/// directed result; a host-local call executes synchronously and invokes its
/// callback in place. Pending guest callbacks are settled with a failure when
/// the session ends or the framework shuts down.
/// </summary>
public sealed partial class ModService
{
	private static bool HasPermission(LoadedMod mod, ModPermission permission) =>
		(mod.Manifest.Permissions & permission) == permission;

	private static bool HasPermission(ModManifest manifest, ModPermission permission) =>
		(manifest.Permissions & permission) == permission;

	private void LogMissingPermission(string modId, string permission) =>
		_log.LogWarning("[Mods] {ModId} does not declare {Permission} — the call is refused.", modId, permission);

	// ---- IModsControl: command frames (the handlers stay thin adapters) ----

	public void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			_log.LogWarning("[Mods] command request from {Sender} arrived outside a host session — dropped.", sender);
			return;
		}

		if (!ModCommandPolicy.IsValidRequest(msg))
		{
			_log.LogWarning("[Mods] command request from {Sender} failed the shape caps — dropped.", sender);
			return;
		}

		if (!TryConsumeCommandRequest(sender))
		{
			return;
		}

		if (!_session.Members.Any(m => m.SteamId == sender && m.Handshaken))
		{
			_log.LogWarning("[Mods] command request from {Sender} ignored: not a handshaken member.", sender);
			return;
		}

		var mod = _mods.FirstOrDefault(m => m.Manifest.Id == msg.ModId);
		if (mod is null)
		{
			_log.LogWarning("[Mods] command request from {Sender} for unknown mod {ModId} — failure returned.", sender, msg.ModId);
			SendCommandResult(sender, msg.RequestId, msg.ModId, msg.Name, false, null, "unknown mod id");
			return;
		}

		var command = mod.Context.CommandAdapter.Find(msg.Name);
		if (command is null)
		{
			_log.LogWarning("[Mods] command request from {Sender} for {ModId}/{Name} refused: not registered.", sender, msg.ModId, msg.Name);
			SendCommandResult(sender, msg.RequestId, msg.ModId, msg.Name, false, null, "command not registered");
			return;
		}

		if (!CanExecuteCommand(mod, command, out var refusal))
		{
			_log.LogWarning("[Mods] command request from {Sender} for {ModId}/{Name} refused: {Refusal}.", sender, msg.ModId, msg.Name, refusal);
			SendCommandResult(sender, msg.RequestId, msg.ModId, msg.Name, false, null, refusal);
			return;
		}

		var result = ExecuteCommand(mod, command, msg.Name, msg.Arguments, sender, msg.RequestId);
		SendCommandResult(sender, msg.RequestId, msg.ModId, msg.Name, result.Success, result.Output, result.Error);
	}

	public void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg)
	{
		if (!ModCommandPolicy.IsValidResult(msg))
		{
			_log.LogWarning("[Mods] command result from {Sender} failed the shape caps — dropped.", sender);
			return;
		}

		var mod = _mods.FirstOrDefault(m => m.Manifest.Id == msg.ModId);
		if (mod is null)
		{
			_log.LogWarning("[Mods] command result from {Sender} for unknown mod {ModId} — dropped.", sender, msg.ModId);
			return;
		}

		var result = new CommandResultData(
			msg.RequestId,
			msg.Name,
			_session.LocalSteamId,
			msg.Success,
			msg.Output,
			msg.Error);
		mod.Context.CommandAdapter.TryComplete(msg.RequestId, result);
	}

	// ---- Registration / execution helpers (called by the per-mod adapter) ----

	private bool RegisterCommand(ModManifest modManifest, ModCommand command)
	{
		if (command.Handler is null)
		{
			_log.LogWarning("[Mods] {Id} tried to register a command without a handler — refused.", modManifest.Id);
			return false;
		}

		if (!ModCommandPolicy.IsValidName(command.Name))
		{
			_log.LogWarning("[Mods] {Id} tried to register an invalid command name — refused.", modManifest.Id);
			return false;
		}

		if (!CanRegisterCommand(modManifest, command, out var refusal))
		{
			LogMissingPermission(modManifest.Id, refusal);
			return false;
		}

		return true;
	}

	private static bool CanRegisterCommand(ModManifest manifest, ModCommand command, out string refusal)
	{
		if (!HasPermission(manifest, ModPermission.RegisterCommand))
		{
			refusal = "RegisterCommand";
			return false;
		}

		if (command.IsHostAction && !HasPermission(manifest, ModPermission.ExecuteHostAction))
		{
			refusal = "ExecuteHostAction";
			return false;
		}

		refusal = string.Empty;
		return true;
	}

	private static bool CanExecuteCommand(LoadedMod mod, ModCommand command, out string refusal)
	{
		if (!HasPermission(mod, ModPermission.RegisterCommand))
		{
			refusal = "RegisterCommand";
			return false;
		}

		if (command.IsHostAction && !HasPermission(mod, ModPermission.ExecuteHostAction))
		{
			refusal = "ExecuteHostAction";
			return false;
		}

		refusal = string.Empty;
		return true;
	}

	private bool RunLocalCommand(LoadedMod mod, ModCommand command, IReadOnlyList<string> arguments,
		uint requestId, Action<IModCommandResult> callback)
	{
		var result = ExecuteCommand(mod, command, command.Name, arguments, _session.LocalSteamId, requestId);
		InvokeCallback(mod, callback, result);
		return true;
	}

	private CommandResultData ExecuteCommand(LoadedMod mod, ModCommand command, string name,
		IReadOnlyList<string> arguments, ulong requester, uint requestId)
	{
		try
		{
			var context = new ModCommandContext(name, arguments, requester, BuildSessionSnapshot(), mod.Context.Logger);
			var output = ModCommandPolicy.ClampOutput(command.Handler(context));
			return new CommandResultData(requestId, name, requester, true, output, null);
		}
		catch (Exception e)
		{
			_log.LogError(e, "[Mods] {Id}/{Name} command threw — the failure is returned to the requester.", mod.Manifest.Id, name);
			return new CommandResultData(requestId, name, requester, false, null, ModCommandPolicy.ClampError(e.Message));
		}
	}

	private void SendCommandResult(ulong target, uint requestId, string modId, string name,
		bool success, string? output, string? error)
	{
		_sender.Send(target, NetMsg.ModCommandResult, new ModCommandResultMsg
		{
			RequestId = requestId,
			ModId = modId,
			Name = name,
			Success = success,
			Output = ModCommandPolicy.ClampOutput(output),
			Error = ModCommandPolicy.ClampError(error),
		});
	}

	private void FailAllPendingCommands(string reason)
	{
		foreach (var mod in _mods)
		{
			mod.Context.CommandAdapter.FailPending(reason);
		}
	}

	private void InvokeCallback(LoadedMod mod, Action<IModCommandResult> callback, IModCommandResult result)
	{
		try
		{
			callback(result);
		}
		catch (Exception e)
		{
			_log.LogError(e, "[Mods] {Id} command-result callback threw — isolated, the pump continues.", mod.Manifest.Id);
		}
	}

	// ---- Nested types (private — part of ModService) ----

	/// <summary>The per-mod command surface: registration + request/result bookkeeping.</summary>
	private sealed class ModCommandAdapter(ModService owner, ModManifest manifest) : IModCommands
	{
		private readonly Dictionary<string, ModCommand> _commands = [];
		private readonly Dictionary<uint, PendingCommand> _pending = [];
		private uint _nextRequestId;

		public bool Register(ModCommand command)
		{
			if (!owner.RegisterCommand(manifest, command))
			{
				return false;
			}

			return Add(command);
		}

		public bool IsRegistered(string name) => _commands.ContainsKey(name);

		public bool TryExecute(string name, IReadOnlyList<string> arguments, Action<IModCommandResult> callback)
		{
			if (!ModCommandPolicy.IsValidName(name) || !ModCommandPolicy.AreArgumentsValid(arguments))
			{
				owner._log.LogWarning("[Mods] {Id}/{Name} rejected at the sender: invalid name or argument shape.", manifest.Id, name);
				return false;
			}

			if (!_commands.TryGetValue(name, out var command))
			{
				owner._log.LogWarning("[Mods] {Id}/{Name} is not registered — execution refused.", manifest.Id, name);
				return false;
			}

			var mod = owner._mods.First(m => m.Manifest.Id == manifest.Id);
			if (!CanExecuteCommand(mod, command, out var refusal))
			{
				owner.LogMissingPermission(manifest.Id, refusal);
				return false;
			}

			if (owner._session.Role == SessionRole.Host)
			{
				var requestId = NextRequestId();
				return owner.RunLocalCommand(mod, command, arguments, requestId, callback);
			}

			if (owner._session.Role != SessionRole.Guest || !owner._session.SessionActive)
			{
				return false; // outside a session a guest request cannot be delivered
			}

			var guestRequestId = NextRequestId();
			_pending.Add(guestRequestId, new PendingCommand(callback));
			owner._sender.Send(owner._session.HostSteamId, NetMsg.ModCommandRequest, new ModCommandRequestMsg
			{
				RequestId = guestRequestId,
				ModId = manifest.Id,
				Name = name,
				Arguments = [.. arguments],
			});
			return true;
		}

		internal bool Add(ModCommand command)
		{
			if (_commands.ContainsKey(command.Name))
			{
				owner._log.LogWarning("[Mods] {Id}/{Name} is already registered — the duplicate is refused.", manifest.Id, command.Name);
				return false;
			}

			_commands.Add(command.Name, command);
			return true;
		}

		internal ModCommand? Find(string name) =>
			_commands.TryGetValue(name, out var command) ? command : null;

		internal void TryComplete(uint requestId, CommandResultData result)
		{
			if (!_pending.TryGetValue(requestId, out var pending))
			{
				owner._log.LogWarning("[Mods] {Id} received a result for unknown request {RequestId} — dropped.", manifest.Id, requestId);
				return;
			}

			_pending.Remove(requestId);
			var mod = owner._mods.First(m => m.Manifest.Id == manifest.Id);
			owner.InvokeCallback(mod, pending.Callback, result);
		}

		internal void FailPending(string reason)
		{
			if (_pending.Count == 0)
			{
				return;
			}

			var pending = _pending.Values.ToArray();
			_pending.Clear();
			var mod = owner._mods.FirstOrDefault(m => m.Manifest.Id == manifest.Id);
			foreach (var entry in pending)
			{
				var result = new CommandResultData(0, string.Empty, owner._session.LocalSteamId, false, null, ModCommandPolicy.ClampError(reason));
				if (mod is not null)
				{
					owner.InvokeCallback(mod, entry.Callback, result);
				}
			}
		}

		private uint NextRequestId() => unchecked(_nextRequestId++);

		private sealed record PendingCommand(Action<IModCommandResult> Callback);
	}

	/// <summary>The execution-time command context (the bind-time session snapshot would be stale).</summary>
	private sealed class ModCommandContext : IModCommandContext
	{
		internal ModCommandContext(string name, IReadOnlyList<string> arguments, ulong requester,
			ISessionInfo session, ILogger logger)
		{
			Name = name;
			Arguments = arguments;
			RequesterSteamId = requester;
			Session = session;
			Logger = logger;
		}

		public string Name { get; }

		public IReadOnlyList<string> Arguments { get; }

		public ulong RequesterSteamId { get; }

		public ISessionInfo Session { get; }

		public ILogger Logger { get; }
	}

	/// <summary>The concrete callback result (the mod only sees <see cref="IModCommandResult"/>).</summary>
	private sealed class CommandResultData : IModCommandResult
	{
		internal CommandResultData(uint requestId, string name, ulong requester,
			bool success, string? output, string? error)
		{
			RequestId = requestId;
			Name = name;
			RequesterSteamId = requester;
			Success = success;
			Output = output;
			Error = error;
		}

		public uint RequestId { get; }

		public string Name { get; }

		public ulong RequesterSteamId { get; }

		public bool Success { get; }

		public string? Output { get; }

		public string? Error { get; }
	}
}
