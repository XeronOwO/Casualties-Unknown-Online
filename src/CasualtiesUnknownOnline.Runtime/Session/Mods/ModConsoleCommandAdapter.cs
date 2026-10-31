using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod console command surface. Every command — built-in or mod — is an <see cref="ICommandDefinition"/>; this adapter turns one into the registry's
/// entry, keeping the definition itself so completion can ask its parameters' completers and the notion of "what the player may run" stays the declared
/// permission. A command registered here is scoped to one mod id, so Unregister can never remove another mod's command.
/// </summary>
internal sealed class ModConsoleCommandAdapter(
	ConsoleCommandRegistry registry,
	ModManifest manifest,
	SessionService session,
	ILogger log) : IModConsoleCommands
{
	private readonly HashSet<string> _registered = [with(StringComparer.OrdinalIgnoreCase)];

	public bool Register(ICommandDefinition command)
	{
		if (command is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null console command — refused.", manifest.Id);
			return false;
		}

		if (!ModCommandPolicy.IsValidName(command.Name))
		{
			log.LogWarning("[Mods] {ModId} tried to register an invalid console command name — refused.", manifest.Id);
			return false;
		}

		foreach (var alias in command.Aliases)
		{
			if (!ModCommandPolicy.IsValidName(alias))
			{
				log.LogWarning("[Mods] {ModId} tried to register /{Name} with the invalid alias '{Alias}' — refused.", manifest.Id, command.Name, alias);
				return false;
			}
		}

		if (!ModPermissionGate.Try(log, manifest, ModPermission.RegisterLocalCommand))
		{
			return false;
		}

		if (command.Permission == CommandPermission.HostOnly
			&& !ModPermissionGate.Try(log, manifest, ModPermission.ExecuteHostAction))
		{
			return false;
		}

		// The registry reads ONE kind per parameter: a definition's own completer is asked first (see CommandConsoleCompletion), and the parameter's declared
		// Kind is the framework source the console falls back to when it offers none.
		var kinds = command.Parameters.Select(parameter => parameter.Kind).ToList();

		var definition = new CommandDefinition(
			command.Name,
			command.Description,
			command.Permission,
			// A mod's own runtime gate is not knowable to the console, so only its declared permission
			// can be applied: a mod command is never classified as a cheat verb here.
			ConsoleCommandGate.Open,
			command.Usage,
			kinds,
			args => Execute(command, args),
			command.Aliases,
			command,
			manifest.Id,
			(requester, args) => Execute(command, args, requester));

		if (!registry.TryAdd(definition, out var error))
		{
			log.LogWarning("[Mods] {ModId} console command /{Name} was refused: {Error}.",
				manifest.Id, command.Name, error);
			return false;
		}

		_registered.Add(command.Name);
		foreach (var alias in command.Aliases)
		{
			_registered.Add(alias);
		}

		log.LogInformation("[Mods] {ModId} registered console command /{Name} ({Aliases} alias(es), {Parameters} parameter(s), {Scope}).",
			manifest.Id, command.Name, command.Aliases.Count, command.Parameters.Count, command.Scope);
		return true;
	}

	public bool IsRegistered(string name) => _registered.Contains(name);

	public bool Unregister(string name)
	{
		if (!_registered.Contains(name))
		{
			return false;
		}

		var definition = registry.Find(name);
		if (definition is null || !registry.TryRemove(definition.Name))
		{
			return false;
		}

		foreach (var word in definition.Aliases.Concat([definition.Name]).Where(_registered.Contains).ToList())
		{
			_registered.Remove(word);
		}

		log.LogInformation("[Mods] {ModId} unregistered console command /{Name}.", manifest.Id, definition.Name);
		return true;
	}

	private ConsoleAnswer Execute(ICommandDefinition command, IReadOnlyList<string> args, ulong? requester = null)
	{
		var typed = args.Count > 0 ? args[0] : "/" + command.Name;
		var typedWord = typed.StartsWith("/", StringComparison.Ordinal) ? typed.Substring(1) : typed;
		var alias = string.Equals(typedWord, command.Name, StringComparison.OrdinalIgnoreCase) ? null : typedWord;
		var local = requester ?? session.LocalSteamId;
		return command.Invoke(new CommandInvocation(
			command.Name,
			alias,
			[.. args.Skip(1)],
			local,
			ModSessionSnapshot.Capture(session)));
	}

	private sealed class CommandInvocation(
		string name,
		string? alias,
		IReadOnlyList<string> arguments,
		ulong localSteamId,
		ISessionInfo session) : ICommandInvocation
	{
		public string Name { get; } = name;

		public string? Alias { get; } = alias;

		public IReadOnlyList<string> Arguments { get; } = arguments;

		public ulong LocalSteamId { get; } = localSteamId;

		public ulong RequesterSteamId { get; } = localSteamId;

		public ISessionInfo Session { get; } = session;
	}
}
