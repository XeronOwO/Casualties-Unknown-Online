using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.ContractTool.Cli;

/// <summary>
/// The tool's argument grammar: one command, then <c>--name value</c> (or
/// <c>--name=value</c>) options and the <c>--fail-on-broken</c> flag. Unknown
/// options are refused rather than ignored — a mistyped flag that silently does
/// nothing would make a runbook step look like it ran.
/// </summary>
internal sealed record CommandLine(string Command, IReadOnlyDictionary<string, string> Options, bool FailOnBroken)
{
	private static readonly string[] FlagOptions = ["fail-on-broken"];

	private static readonly string[] ValueOptions = ["assembly", "adapter", "out", "json", "previous", "current"];

	/// <summary>Parses the command line; throws <see cref="UsageException"/> on anything it cannot honour.</summary>
	internal static CommandLine Parse(string[] args)
	{
		if (args.Length == 0)
		{
			throw new UsageException("no command given");
		}

		var command = args[0];
		if (command is "--help" or "-h" or "help")
		{
			return new CommandLine("help", new Dictionary<string, string>(StringComparer.Ordinal), false);
		}

		var options = new Dictionary<string, string>(StringComparer.Ordinal);
		var failOnBroken = false;
		for (var index = 1; index < args.Length; index++)
		{
			var argument = args[index];
			if (!argument.StartsWith("--", StringComparison.Ordinal))
			{
				throw new UsageException($"unexpected argument '{argument}'");
			}

			var name = argument.Substring(2);
			string? value = null;
			var equals = name.IndexOf('=');
			if (equals >= 0)
			{
				value = name.Substring(equals + 1);
				name = name.Substring(0, equals);
			}

			if (FlagOptions.Contains(name, StringComparer.Ordinal))
			{
				if (value is not null)
				{
					throw new UsageException($"--{name} takes no value");
				}

				failOnBroken = true;
				options[name] = "true";
				continue;
			}

			if (!ValueOptions.Contains(name, StringComparer.Ordinal))
			{
				throw new UsageException($"unknown option --{name}");
			}

			if (value is null)
			{
				if (index + 1 >= args.Length)
				{
					throw new UsageException($"--{name} needs a value");
				}

				value = args[++index];
			}

			options[name] = value;
		}

		return new CommandLine(command, options, failOnBroken);
	}

	/// <summary>The value of a required option.</summary>
	internal string Require(string name) => Options.TryGetValue(name, out var value)
		? value
		: throw new UsageException($"missing required option --{name}");

	/// <summary>The value of an optional option, or null.</summary>
	internal string? Value(string name) => Options.TryGetValue(name, out var value) ? value : null;
}
