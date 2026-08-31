using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The local console command registry. Built-in commands are discovered once
/// from <see cref="ConsoleCommandAttribute"/>-marked methods; mod commands are
/// added at mod discovery through the Abstractions adapter. Reads are exposed
/// as read-only collections, while the private side keeps a dictionary for
/// O(1) command lookup and duplicate rejection.
/// </summary>
public sealed class ConsoleCommandRegistry
{
	private readonly List<CommandDefinition> _commands = [];
	private readonly Dictionary<string, CommandDefinition> _commandsByName = [with(StringComparer.OrdinalIgnoreCase)];

	/// <summary>All registered commands, in registration order.</summary>
	internal IReadOnlyList<CommandDefinition> All => _commands;

	internal CommandDefinition? Find(string name) =>
		_commandsByName.TryGetValue(name, out var command) ? command : null;

	internal IReadOnlyList<CommandSpec> ToSpecs() => [.. _commands.Select(c => c.ToSpec())];

	/// <summary>
	/// Scans <paramref name="owner"/>'s type for <see cref="ConsoleCommandAttribute"/>
	/// methods and registers them as immutable built-in entries. Validation is
	/// fail-fast: a malformed built-in attribute is an internal programming error.
	/// </summary>
	internal void AddBuiltIns(object owner)
	{
		var methods = owner.GetType()
			.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.OrderBy(m => m.MetadataToken);

		foreach (var method in methods)
		{
			var attribute = method.GetCustomAttribute<ConsoleCommandAttribute>(inherit: false);
			if (attribute is null)
			{
				continue;
			}

			if (method.ReturnType != typeof(string))
			{
				throw new InvalidOperationException(
					$"Console command handler {method.Name} must return string; got {method.ReturnType}.");
			}

			var parameters = method.GetParameters();
			if (parameters.Length != 1 || parameters[0].ParameterType != typeof(IReadOnlyList<string>))
			{
				throw new InvalidOperationException(
					$"Console command handler {method.Name} must accept a single IReadOnlyList<string> argument.");
			}

			var handler = (Func<IReadOnlyList<string>, string?>)method.CreateDelegate(
				typeof(Func<IReadOnlyList<string>, string?>), owner);
			var definition = new CommandDefinition(
				attribute.Name,
				attribute.Description,
				attribute.Permission,
				attribute.Usage,
				attribute.ArgumentKinds,
				handler);

			if (!TryAdd(definition, out var error))
			{
				throw new InvalidOperationException($"Built-in console command registration failed: {error}");
			}
		}
	}

	internal bool TryAdd(CommandDefinition definition, out string? error)
	{
		if (string.IsNullOrWhiteSpace(definition.Name))
		{
			error = "command name must not be empty";
			return false;
		}

		if (_commandsByName.ContainsKey(definition.Name))
		{
			error = $"command '/{definition.Name}' is already registered";
			return false;
		}

		_commands.Add(definition);
		_commandsByName.Add(definition.Name, definition);
		error = null;
		return true;
	}

	internal bool TryRemove(string name)
	{
		if (!_commandsByName.TryGetValue(name, out var definition))
		{
			return false;
		}

		_commandsByName.Remove(name);
		_commands.Remove(definition);
		return true;
	}
}
