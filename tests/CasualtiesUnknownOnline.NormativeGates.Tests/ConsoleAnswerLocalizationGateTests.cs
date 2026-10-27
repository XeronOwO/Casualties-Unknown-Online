using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The console-answer localisation gate (ticket
/// <c>docs/backlog/review/player-facing-text-localized-per-receiver.md</c>): a
/// console answer a PLAYER reads carries its catalogue key, so the console renders
/// it in the reader's own language. An answer built as a raw interpolated English
/// sentence (<c>ConsoleAnswer.Refused($"Unknown member '{name}'.")</c>) reaches a
/// Chinese client in English — measured 2026-10-27 on a Chinese host whose console
/// read English while its panel read Chinese.
///
/// <para>
/// Why a gate: the answer struct already supports the key (<c>ConsoleAnswer.Refused(text, key, args…)</c>) and the
/// console already renders it, so nothing but habit stands between a new answer and the rule — and habit is what the
/// census catches. The count is a BUDGET that only falls: every conversion lowers it, and a new raw answer fails the
/// gate instead of quietly joining the pile. When the budget reaches zero the gate keeps holding the line for every
/// future answer.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: this reads SOURCE text under <c>src/</c> and matches the two construction shapes
/// <c>ConsoleAnswer.Refused($"…")</c> and <c>ConsoleAnswer.Ok($"…")</c> without a key argument. An answer assembled
/// into a local first and passed by name, one built with <c>string.Format</c>, and one whose sentence comes from a
/// service (<c>ConsoleAnswer.Refused(refusal)</c>) are outside the match; those are the conversions the ticket's own
/// census lists separately. The gate proves the shape is not reintroduced, never that a converted sentence reads well.
/// </para>
/// </summary>
public class ConsoleAnswerLocalizationGateTests
{
	/// <summary>The tree the census reads.</summary>
	internal const string SourceRoot = "src";

	/// <summary>
	/// Raw interpolated answers still standing at 2026-10-27, after the cheat, admin and rule families converted (42
	/// measured). This is a ceiling: convert one, lower it by the number converted, never raise it.
	/// </summary>
	internal const int RawAnswerBudget = 42;

	/// <summary>The two answer kinds a player reads; <c>Silent</c> carries no sentence and is out of the census.</summary>
	private static readonly Regex RawAnswer = new(@"ConsoleAnswer\.(Refused|Ok)\(\$""", RegexOptions.Compiled);

	[Fact]
	public void EveryConsoleAnswerCarriesItsKeyOrStaysWithinTheBudget()
	{
		var offenders = new List<string>();
		var count = 0;

		foreach (var file in Directory.EnumerateFiles(RepositoryPaths.File(SourceRoot), "*.cs", SearchOption.AllDirectories))
		{
			var name = file.Replace('\\', '/');
			if (name.Contains("/obj/", StringComparison.Ordinal) || name.Contains("/bin/", StringComparison.Ordinal))
			{
				continue;
			}

			var lines = File.ReadAllLines(file);
			for (var index = 0; index < lines.Length; index++)
			{
				if (RawAnswer.IsMatch(lines[index]))
				{
					count++;
					offenders.Add($"{name}:{index + 1}");
				}
			}
		}

		Assert.True(
			count <= RawAnswerBudget,
			$"{count} console answers build a raw English sentence (budget {RawAnswerBudget}). Give each one a catalogue key and its arguments, add the "
			+ "sentence to en.json and zh.json, and lower the budget in the same change. " + string.Join(", ", offenders));
	}
}
