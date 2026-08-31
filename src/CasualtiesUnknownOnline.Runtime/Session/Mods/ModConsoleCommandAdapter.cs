using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod local console command surface. A command registered here is
/// local-only (no host relay), appears in the in-game console registry, and is
/// scoped to one mod id so Unregister can never remove another mod's command.
/// Registration is gated by <see cref="ModPermission.RegisterCommand"/> and, for
/// HostOnly commands, <see cref="ModPermission.ExecuteHostAction"/>.
/// </summary>
internal sealed class ModConsoleCommandAdapter(
	ConsoleCommandRegistry registry,
	ModManifest manifest,
	SessionService session,
	ILogger log) : IModConsoleCommands
{
	private readonly HashSet<string> _registered = [with(StringComparer.Ordinal)];

	public bool Register(ModConsoleCommand command)
	{
		if (command is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null console command — refused.", manifest.Id);
			return false;
		}

		if (command.Handler is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a console command without a handler — refused.", manifest.Id);
			return false;
		}

		if (!ModCommandPolicy.IsValidName(command.Name))
		{
			log.LogWarning("[Mods] {ModId} tried to register an invalid console command name — refused.", manifest.Id);
			return false;
		}

		if (!ModPermissionGate.Try(log, manifest, ModPermission.RegisterCommand))
		{
			return false;
		}

		if (command.Permission == CommandPermission.HostOnly
			&& !ModPermissionGate.Try(log, manifest, ModPermission.ExecuteHostAction))
		{
			return false;
		}

		var definition = new CommandDefinition(
			command.Name,
			command.Description,
			command.Permission,
			command.Usage,
			command.ArgumentKinds,
			args => Execute(command, args));

		if (!registry.TryAdd(definition, out var error))
		{
			log.LogWarning("[Mods] {ModId} console command /{Name} was refused: {Error}.",
				manifest.Id, command.Name, error);
			return false;
		}

		_registered.Add(command.Name);
		log.LogInformation("[Mods] {ModId} registered local console command /{Name}.", manifest.Id, command.Name);
		return true;
	}

	public bool IsRegistered(string name) => _registered.Contains(name);

	public bool Unregister(string name)
	{
		if (!_registered.Contains(name))
		{
			return false;
		}

		if (!registry.TryRemove(name))
		{
			return false;
		}

		_registered.Remove(name);
		log.LogInformation("[Mods] {ModId} unregistered local console command /{Name}.", manifest.Id, name);
		return true;
	}

	private string? Execute(ModConsoleCommand command, IReadOnlyList<string> args)
	{
		var context = new ModConsoleCommandContext(
			command.Name,
			[.. args.Skip(1)],
			session.LocalSteamId,
			ModSessionSnapshot.Capture(session));
		return command.Handler(context);
	}

	private sealed class ModConsoleCommandContext(
		string name,
		IReadOnlyList<string> arguments,
		ulong localSteamId,
		ISessionInfo session) : IModConsoleCommandContext
	{
		public string Name { get; } = name;

		public IReadOnlyList<string> Arguments { get; } = arguments;

		public ulong LocalSteamId { get; } = localSteamId;

		public ISessionInfo Session { get; } = session;
	}
}
