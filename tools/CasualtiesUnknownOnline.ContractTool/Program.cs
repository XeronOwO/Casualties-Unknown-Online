using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CasualtiesUnknownOnline.ContractTool.Cli;
using CasualtiesUnknownOnline.ContractTool.Diff;
using CasualtiesUnknownOnline.ContractTool.Report;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool;

/// <summary>
/// The game-update contract toolchain's entry point — two commands, both
/// read-only with respect to the game:
///
///   snapshot — read ONE game-assembly build as metadata and write a canonical,
///              byte-reproducible JSON snapshot (optionally embedding the patch
///              contract rows read from the adapter assembly's metadata);
///   diff     — classify the differences between two snapshots into the
///              compatibility report the update-day runbook consumes.
///
/// Exit codes are part of the contract: 0 = ran, 1 = --fail-on-broken and a
/// contract verdict says a hook's target moved, 2 = usage, 3 = the input could
/// not be read.
/// </summary>
internal static class Program
{
	private const string Usage = """
		Casualties Unknown: Online — game-assembly contract toolchain (read-only)

		Usage:
		  CasualtiesUnknownOnline.ContractTool snapshot --assembly <game-assembly.dll>
		      [--adapter <CasualtiesUnknownOnline.GameAdapter.dll>] [--out <snapshot.json>]
		  CasualtiesUnknownOnline.ContractTool diff --previous <snapshot.json> --current <snapshot.json>
		      [--json <diff.json>] [--out <report.md>] [--fail-on-broken]

		snapshot  Reads one build's metadata (nothing is loaded or executed) and writes a
		          canonical snapshot. --adapter embeds the patch-target contract rows so a
		          report's rows and the standalone snapshot cannot drift apart.
		diff      Compares two snapshots of the same assembly and CLASSIFIES the differences;
		          writes the markdown compatibility report (stdout when --out is omitted)
		          and optionally the machine-readable diff.

		Both commands write only the requested outputs; neither ever writes to the game
		directory. Snapshot both builds with the same adapter build.
		""";

	private static int Main(string[] args)
	{
		try
		{
			return Run(CommandLine.Parse(args));
		}
		catch (UsageException exception)
		{
			Console.Error.WriteLine($"error: {exception.Message}");
			Console.Error.WriteLine();
			Console.Error.WriteLine(Usage);
			return 2;
		}
		catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or JsonException)
		{
			Console.Error.WriteLine($"error: {exception.Message}");
			return 3;
		}
	}

	private static int Run(CommandLine commandLine) => commandLine.Command switch
	{
		"snapshot" => Snapshot(commandLine),
		"diff" => Diff(commandLine),
		"help" => Help(),
		_ => throw new UsageException($"unknown command '{commandLine.Command}'"),
	};

	private static int Help()
	{
		Console.Out.WriteLine(Usage);
		return 0;
	}

	private static void WriteFile(string path, string contents) =>
		File.WriteAllText(EnsureDirectory(path), contents, new UTF8Encoding(false));

	/// <summary>
	/// Creates the output file's directory when it does not exist. The runbook writes
	/// into `artifacts/contract/`, which a clean checkout does not have yet — "write
	/// this file here" is the tool's job, not an instruction the operator has to
	/// satisfy first (and failing here would surface as the "input could not be read"
	/// exit code, which would be a lie about what went wrong).
	/// </summary>
	private static string EnsureDirectory(string path)
	{
		var fullPath = Path.GetFullPath(path);
		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		return fullPath;
	}

	private static int Snapshot(CommandLine commandLine)
	{
		var document = GameAssemblyReader.Read(commandLine.Require("assembly"), commandLine.Value("adapter"));
		var json = SnapshotWriter.ToJson(document);
		if (commandLine.Value("out") is { } output)
		{
			WriteFile(output, json);
		}
		else
		{
			Console.Out.Write(json);
		}

		Console.Error.WriteLine(
			$"snapshot: {document.Assembly.Name} {document.Assembly.Version} sha256={document.Assembly.Sha256} "
			+ $"types={document.Counts.Types} methods={document.Counts.Methods} contracts={document.Counts.Contracts}");
		return 0;
	}

	private static int Diff(CommandLine commandLine)
	{
		var previous = SnapshotReader.Read(commandLine.Require("previous"));
		var current = SnapshotReader.Read(commandLine.Require("current"));
		var result = SnapshotDiffer.Diff(previous, current);
		var report = CompatibilityReport.Render(result);
		if (commandLine.Value("out") is { } output)
		{
			WriteFile(output, report);
		}
		else
		{
			Console.Out.Write(report);
		}

		if (commandLine.Value("json") is { } json)
		{
			DiffWriter.Write(result, EnsureDirectory(json));
		}

		Console.Error.WriteLine($"diff: differences={result.Differences.Count} brokenContracts={result.HasBrokenContracts}");
		return commandLine.FailOnBroken && result.HasBrokenContracts ? 1 : 0;
	}
}
