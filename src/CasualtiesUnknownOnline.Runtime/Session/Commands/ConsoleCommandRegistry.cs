using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;

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

			if (method.ReturnType != typeof(ConsoleAnswer))
			{
				throw new InvalidOperationException(
					$"Console command handler {method.Name} must return ConsoleAnswer; got {method.ReturnType}.");
			}

			var parameters = method.GetParameters();
			if (parameters.Length != 1 || parameters[0].ParameterType != typeof(IReadOnlyList<string>))
			{
				throw new InvalidOperationException(
					$"Console command handler {method.Name} must accept a single IReadOnlyList<string> argument.");
			}

			var handler = (Func<IReadOnlyList<string>, ConsoleAnswer>)method.CreateDelegate(
				typeof(Func<IReadOnlyList<string>, ConsoleAnswer>), owner);
			var definition = new CommandDefinition(
				attribute.Name,
				attribute.Description,
				attribute.Permission,
				attribute.Gate,
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

		// Every word that reaches the command claims a slot: the name and each alias, so a second command cannot shadow one and a removal takes them all.
		var words = new List<string> { definition.Name };
		foreach (var alias in definition.Aliases)
		{
			if (string.IsNullOrWhiteSpace(alias))
			{
				error = $"command '/{definition.Name}' declares an empty alias";
				return false;
			}

			if (string.Equals(alias, definition.Name, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			words.Add(alias);
		}

		foreach (var word in words)
		{
			if (_commandsByName.ContainsKey(word))
			{
				error = $"command '/{word}' is already registered";
				return false;
			}
		}

		_commands.Add(definition);
		foreach (var word in words)
		{
			_commandsByName.Add(word, definition);
		}

		error = null;
		return true;
	}

	internal bool TryRemove(string name)
	{
		if (!_commandsByName.TryGetValue(name, out var definition))
		{
			return false;
		}

		foreach (var word in _commandsByName.Where(pair => ReferenceEquals(pair.Value, definition)).Select(pair => pair.Key).ToList())
		{
			_commandsByName.Remove(word);
		}

		_commands.Remove(definition);
		return true;
	}
}
