using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The text form of one host rule's value: what the console's <c>/gamerule</c> verb answers for a
/// query and accepts for a write, and the names its refusal lists.
///
/// <para>
/// It exists because the console speaks the player's spelling while the config carries typed values.
/// One place owns both directions, so a value the verb prints is a value it accepts back, and a
/// refusal names exactly the set the parser reads. The two three-way sets' choices are DERIVED from
/// the text forms the config itself uses (<see cref="RemoteBackpackAccessText"/>,
/// <see cref="NativeBindingParityText"/>), not restated, so a level added to either closed set cannot
/// leave the console behind; a flag's two names are its own and need no such derivation.
/// </para>
///
/// <para>
/// Matching is ordinal: the console's arguments follow Minecraft's case-sensitive grammar, so an
/// accepted text is always the canonical spelling and no two spellings of one value exist.
/// </para>
/// </summary>
public static class HostRuleValueText
{
	private static readonly IReadOnlyList<string> FlagChoices = ["true", "false"];

	private static readonly IReadOnlyDictionary<NativeBindingParity, string> ParityText = Enum
		.GetValues(typeof(NativeBindingParity))
		.Cast<NativeBindingParity>()
		.ToDictionary(value => value, NativeBindingParityText.Format);

	private static readonly IReadOnlyList<string> ParityChoices = [.. ParityText.OrderBy(pair => (int)pair.Key).Select(pair => pair.Value)];

	private static readonly IReadOnlyDictionary<RemoteBackpackAccess, string> AccessText = Enum
		.GetValues(typeof(RemoteBackpackAccess))
		.Cast<RemoteBackpackAccess>()
		.ToDictionary(value => value, RemoteBackpackAccessText.Format);

	private static readonly IReadOnlyList<string> AccessChoices = [.. AccessText.OrderBy(pair => (int)pair.Key).Select(pair => pair.Value)];

	/// <summary>
	/// The value names a rule of this row's kind accepts, in the order a refusal lists them. A numeric
	/// rule has no names — its accepted set is the row's range, which the refusal states instead.
	/// </summary>
	public static IReadOnlyList<string> Choices(HostRuleCensus.Row row) => row.Kind switch
	{
		HostRuleKind.Boolean => FlagChoices,
		HostRuleKind.Parity => ParityChoices,
		HostRuleKind.Access => AccessChoices,
		_ => [],
	};

	/// <summary>
	/// Parses <paramref name="text"/> as the value of the rule <paramref name="row"/> describes.
	/// <paramref name="value"/> is the canonical text a write takes (and a query prints); a refusal
	/// names the accepted set — the value names, or the numeric rule's range.
	/// </summary>
	public static bool TryParse(HostRuleCensus.Row row, string text, out string value, out string? error)
	{
		var choices = Choices(row);
		if (choices.Count > 0)
		{
			if (choices.Contains(text, StringComparer.Ordinal))
			{
				value = text;
				error = null;
				return true;
			}

			value = "";
			error = $"'{text}' is not one of {string.Join(", ", choices)}.";
			return false;
		}

		if (row.Kind == HostRuleKind.Number)
		{
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
				&& row.Range is { } range
				&& number >= range.Minimum
				&& number <= range.Maximum)
			{
				value = FormatNumber(number);
				error = null;
				return true;
			}

			value = "";
			error = row.Range is { } accepted
				? $"'{text}' is not a number between {FormatNumber(accepted.Minimum)} and {FormatNumber(accepted.Maximum)}."
				: $"'{text}' is not a number.";
			return false;
		}

		value = "";
		error = $"Host rule '{row.Name}' has no value shape.";
		return false;
	}

	/// <summary>The value in force, in the form a write of the same value would accept.</summary>
	public static string Describe(HostRuleSet rules, HostRuleCensus.Row row) => row.Kind switch
	{
		HostRuleKind.Boolean => rules.Boolean(row.Id) ? "true" : "false",
		HostRuleKind.Number => FormatNumber(rules.Number(row.Id)),
		HostRuleKind.Parity => ParityText[rules.Parity(row.Id)],
		HostRuleKind.Access => AccessText[rules.Access(row.Id)],
		_ => throw new ArgumentOutOfRangeException(nameof(row), row.Kind, "host rule has no value shape"),
	};

	/// <summary>The invariant round-trip form: what the parser reads back as the same number.</summary>
	private static string FormatNumber(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
