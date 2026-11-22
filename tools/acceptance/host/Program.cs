using System.Globalization;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The command line of the acceptance host: one C# entry for the orchestration the PowerShell scripts
/// carried (environment check, window capture, staged input, in-process driving, preflight, client
/// lifecycle, and the suite run). Each command is a direct port of its script; the script stays until the
/// port answers the same and is deleted in the same change.
/// </summary>
internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		if (args.Length == 0)
		{
			PrintHelp();
			return 64;
		}

		var command = args[0];
		var rest = args[1..];
		try
		{
			return command switch
			{
				"-h" or "--help" or "help" => PrintHelpAndSucceed(),
				"env" => SessionEnvironmentCommand.Run(rest),
				"capture" => WindowCaptureCommand.Run(rest),
				"input" => InputCommand.Run(rest),
				"launch" => LaunchCommand.Run(rest),
				"run" => RunCommand.Run(rest),
				"preflight" => PreflightCommand.Run(rest),
				"replay" => ReplayCommand.Run(rest),
				_ => Unknown(command),
			};
		}
		catch (CommandFailure failure)
		{
			Console.Error.WriteLine(failure.Message);
			return failure.ExitCode;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
	}

	private static int PrintHelpAndSucceed()
	{
		PrintHelp();
		return 0;
	}

	private static int Unknown(string command)
	{
		Console.Error.WriteLine($"unknown command '{command}'; run the host with --help");
		return 64;
	}

	private static void PrintHelp()
	{
		Console.WriteLine("usage: CasualtiesUnknownOnline.AcceptanceHost <command> [options]");
		Console.WriteLine();
		Console.WriteLine("  env      --game <install> [--mode status|ensure-cuo] [--play-marker <dll>] [--parked-tree <name>]");
		Console.WriteLine("  capture  --out <png> [--process <name>] [--pid <id>] [--attempts <n>] [--delay-ms <n>]");
		Console.WriteLine("  input    --action click|key|drag|rdrag|move|rpress|rrelease [--process <name>] [--pid <id>] ...");
		Console.WriteLine("  launch   [--game-dir <install>] [--only host|guest|alt] [--all] [--restart] ...");
		Console.WriteLine("  run      --game-dir <install> [--out <dir>] [--suite <name>] [--fresh] [--no-launch] ...");
		Console.WriteLine("  preflight [--facts <md>] [--json <path>] [--timeout-ms <ms>]");
		Console.WriteLine("  replay   --baseline <report.json> --candidate <report.json> [--case-wall-ms <n>] [--case-wall-percent <n>] ...");
		Console.WriteLine();
		Console.WriteLine("Every command prints one JSON object on success and a message on failure.");
	}
}

/// <summary>A refusal with the script's own exit code convention (2 machine not free, 64 usage).</summary>
internal sealed class CommandFailure(int exitCode, string message) : Exception(message)
{
	public int ExitCode { get; } = exitCode;
}

/// <summary>Minimal option reader: <c>--name value</c> and <c>--flag</c>, with an explicit required set.</summary>
internal sealed class Arguments(string[] args, string usage)
{
	private readonly Dictionary<string, string> _values = Read(args);

	public string Usage { get; } = usage;

	public string Required(string name)
	{
		if (!_values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
		{
			throw new CommandFailure(64, $"missing --{name}; usage: {Usage}");
		}

		return value;
	}

	public string? Optional(string name) => _values.TryGetValue(name, out var value) ? value : null;

	public int OptionalNumber(string name, int fallback)
	{
		var text = Optional(name);
		if (text is null)
		{
			return fallback;
		}

		if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
		{
			throw new CommandFailure(64, $"--{name} must be a whole number; usage: {Usage}");
		}

		return value;
	}

	private static Dictionary<string, string> Read(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (var index = 0; index < args.Length; index++)
		{
			var text = args[index];
			if (!text.StartsWith("--", StringComparison.Ordinal))
			{
				continue;
			}

			var name = text[2..];
			var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
				? args[++index]
				: "true";
			values[name] = value;
		}

		return values;
	}
}
