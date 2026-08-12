using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The replay-file parser — the phase-4 format's decision side, pure text → steps.
/// One step per line: "@&lt;ms&gt; &lt;action&gt; &lt;args...&gt;" on a monotonically
/// increasing timeline ("@" = the virtual clock at which the step runs; equal
/// timestamps run in file order — the same frame). Lines starting with '#' are
/// provenance comments (e.g. the OperationTrace line of the original bug, so
/// the archive is traceable back to a real session). Structural validation
/// lives here: unknown actions, wrong argument counts, decreasing timestamps
/// and unknown node aliases fail with the file:line of the offending line —
/// a replay file that cannot be understood is a test failure, never a skip.
/// Semantic validation (fault specs, assertion kinds, numeric arguments) lives
/// in the runner, which reports the same file:line shape.
/// </summary>
internal static class ReplayParser
{
	private static readonly string[] Actions =
		["spawn", "pickup", "drop", "use", "slot", "destroy", "fault", "clearfault", "expect", "expect_no_reject"];

	// Minimum argument counts per action (node alias excluded — the parser
	// validates aliases, the runner converts them). Expect lines have at least
	// the assertion kind; their full shape is semantic.
	private static readonly Dictionary<string, int> MinArgs = new()
	{
		["spawn"] = 4, // node itemId type condition
		["pickup"] = 4,
		["drop"] = 4,
		["use"] = 4,
		["slot"] = 5, // node itemId index type condition
		["destroy"] = 2, // node itemId
		["fault"] = 3, // from to spec...
		["clearfault"] = 2,
		["expect"] = 2, // kind ...
		["expect_no_reject"] = 1,
	};

	private static readonly string[] NodeAliases = ["host", "g1", "g2"];

	internal static ReplayStep[] Parse(string text, string sourceName)
	{
		var steps = new List<ReplayStep>();
		var lines = text.Split('\n');
		var lastMs = -1;

		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i].Trim();
			if (line.Length == 0 || line.StartsWith("#"))
			{
				continue;
			}

			var lineNumber = i + 1;
			if (!line.StartsWith("@"))
			{
				throw new FormatException($"{sourceName}:{lineNumber}: expected '@<ms> <action>', got '{line}'");
			}

			var parts = line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 1 || !int.TryParse(parts[0], out var ms))
			{
				throw new FormatException($"{sourceName}:{lineNumber}: invalid timestamp in '{line}'");
			}

			if (ms < lastMs)
			{
				throw new FormatException($"{sourceName}:{lineNumber}: timestamp {ms} precedes the previous {lastMs} — the timeline must not go backwards");
			}

			lastMs = ms;
			if (parts.Length < 2)
			{
				throw new FormatException($"{sourceName}:{lineNumber}: missing action in '{line}'");
			}

			var action = parts[1];
			var args = parts.Skip(2).ToArray();
			if (!Actions.Contains(action))
			{
				throw new FormatException($"{sourceName}:{lineNumber}: unknown action '{action}' (expected one of {string.Join(" / ", Actions)})");
			}

			if (args.Length < MinArgs[action])
			{
				throw new FormatException($"{sourceName}:{lineNumber}: '{action}' needs at least {MinArgs[action]} argument(s), got {args.Length}");
			}

			ValidateAliases(action, args, sourceName, lineNumber);
			steps.Add(new ReplayStep(ms, action, args, lineNumber));
		}

		if (steps.Count == 0)
		{
			throw new FormatException($"{sourceName}: no replay steps (empty file?)");
		}

		return [.. steps];
	}

	private static void ValidateAliases(string action, string[] args, string sourceName, int lineNumber)
	{
		// Node aliases occupy the first argument of every action except fault/
		// clearfault (which name a link) and expect (whose kind-dependent shape
		// the runner validates).
		if (action == "fault" || action == "clearfault" || action == "expect")
		{
			return;
		}

		if (!NodeAliases.Contains(args[0]))
		{
			throw new FormatException($"{sourceName}:{lineNumber}: '{action}' — unknown node '{args[0]}' (host/g1/g2)");
		}
	}
}
