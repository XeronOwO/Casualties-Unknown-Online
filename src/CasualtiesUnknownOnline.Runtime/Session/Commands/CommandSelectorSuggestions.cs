using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Bracket-aware selector completion. The console's completion engine replaces
/// the full current token, so every returned suggestion is a full selector
/// prefix (for example <c>@a[type=player</c>) rather than a bare key/value.
/// </summary>
internal static class CommandSelectorSuggestions
{
	private static readonly CommandSuggestion[] BaseSelectors =
	[
		new("@a", "All players"),
		new("@p", "Nearest player"),
		new("@s", "Self"),
		new("@e", "All entities"),
		new("@r", "Random player"),
	];

	private static readonly string[] FilterKeys = ["type", "name", "distance", "limit", "sort"];

	public static IReadOnlyList<CommandSuggestion> Suggest(string prefix)
	{
		if (string.IsNullOrWhiteSpace(prefix) || prefix[0] != '@')
		{
			return [];
		}

		var open = prefix.IndexOf('[');
		if (open < 0)
		{
			return [.. BaseSelectors.Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
		}

		var basePrefix = prefix.Substring(0, open + 1);
		var inner = prefix.Substring(open + 1);
		var segment = inner;
		var comma = segment.LastIndexOf(',');
		if (comma >= 0)
		{
			segment = segment.Substring(comma + 1);
		}

		var equals = segment.IndexOf('=');
		if (equals < 0)
		{
			return SuggestKeys(basePrefix, prefix, segment);
		}

		var key = segment.Substring(0, equals).Trim().ToLowerInvariant();
		var value = segment.Substring(equals + 1);
		return SuggestValues(basePrefix, prefix, key, value);
	}

	private static IReadOnlyList<CommandSuggestion> SuggestKeys(string basePrefix, string prefix, string segment)
	{
		var result = new List<CommandSuggestion>();
		foreach (var key in FilterKeys)
		{
			var candidate = basePrefix + key + "=";
			if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(candidate, $"Filter by {key}"));
			}
		}

		return result;
	}

	private static IReadOnlyList<CommandSuggestion> SuggestValues(string basePrefix, string prefix, string key, string value)
	{
		string[] values = key switch
		{
			"type" => ["player", "cuo:player"],
			"sort" => ["nearest", "furthest", "random", "arbitrary"],
			_ => [],
		};

		var result = new List<CommandSuggestion>();
		foreach (var candidateValue in values)
		{
			var candidate = basePrefix + key + "=" + candidateValue;
			if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(candidate, $"{key} value {candidateValue}"));
			}
		}

		return result;
	}
}
