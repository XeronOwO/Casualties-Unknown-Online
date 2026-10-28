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
/// Reach, stated rather than implied: this reads SOURCE text under <c>src/</c> and looks at every
/// <c>ConsoleAnswer.Refused/Ok</c> call whose FIRST argument is an interpolated string, then asks whether the call also
/// carries a dotted key literal. A keyed answer is NOT an offender even though its sentence is interpolated — the
/// sentence stays for the log and the fallback, and only the key makes the console translate. That distinction is the
/// whole point of the gate: an earlier version counted the call alone and could therefore never report progress (it read
/// 42 before and after thirteen conversions).
/// </para>
///
/// <para>
/// What stays outside: an answer assembled into a local first and passed by name, one built with <c>string.Format</c>,
/// and one whose sentence comes from a service (<c>ConsoleAnswer.Refused(refusal)</c>) are not matched; those are the
/// conversions the localisation ticket's own census lists by hand. The gate proves the shape is not reintroduced, never
/// that a converted sentence reads well.
/// </para>
/// </summary>
public class ConsoleAnswerLocalizationGateTests
{
	/// <summary>The tree the census reads.</summary>
	internal const string SourceRoot = "src";

	/// <summary>
	/// Raw interpolated answers still standing at 2026-10-28, with the keyed ones excluded. This is a ceiling: convert
	/// one, lower it by the number converted, never raise it.
	/// </summary>
	internal const int RawAnswerBudget = 7;

	/// <summary>One console answer's argument text, one level of nested parentheses tolerated (a sentence may call <c>string.Join(…)</c>).</summary>
	private static readonly Regex AnswerCall = new(
		@"ConsoleAnswer\.(?:Refused|Ok)\(\$""(?:[^()]|\([^()]*\))*\)",
		RegexOptions.Compiled);

	/// <summary>A dotted catalogue key, which no English sentence is.</summary>
	private static readonly Regex KeyLiteral = new("""[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+""", RegexOptions.Compiled);

	[Fact]
	public void EveryConsoleAnswerCarriesItsKeyOrStaysWithinTheBudget()
	{
		var offenders = new List<string>();
		var raw = 0;

		foreach (var file in Directory.EnumerateFiles(RepositoryPaths.File(SourceRoot), "*.cs", SearchOption.AllDirectories))
		{
			var name = file.Replace('\\', '/');
			if (name.Contains("/obj/", StringComparison.Ordinal) || name.Contains("/bin/", StringComparison.Ordinal))
			{
				continue;
			}

			var text = File.ReadAllText(file);
			foreach (Match call in AnswerCall.Matches(text))
			{
				// The call's own text, key included when it has one: `ConsoleAnswer.Refused($"…", "key", args…)`.
				if (KeyLiteral.IsMatch(call.Value))
				{
					continue;
				}

				raw++;
				var line = LineOf(text, call.Index);
				offenders.Add($"{name}:{line}");
			}
		}

		Assert.True(
			raw <= RawAnswerBudget,
			$"{raw} console answers build a raw English sentence (budget {RawAnswerBudget}). Give each one a catalogue key and its arguments, add the "
			+ "sentence to en.json and zh.json, and lower the budget in the same change. " + string.Join(", ", offenders));
	}

	/// <summary>The 1-based line a character offset falls on, so the failure names a place a reader can open.</summary>
	private static int LineOf(string text, int offset)
	{
		var line = 1;
		for (var i = 0; i < offset && i < text.Length; i++)
		{
			if (text[i] == '\n')
			{
				line++;
			}
		}

		return line;
	}
}
