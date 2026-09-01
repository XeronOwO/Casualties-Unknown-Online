using System;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Parses the body of a bracketed selector into <see cref="CommandSelectorFilter"/>.
/// Values may be quoted or unquoted; keys are case-insensitive.
/// </summary>
internal static class CommandSelectorFilterParser
{
	public static bool TryParse(string? text, out CommandSelectorFilter filter)
	{
		filter = CommandSelectorFilter.None;
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}

		foreach (var rawPair in text!.Split(','))
		{
			var pair = rawPair.Trim();
			if (pair.Length == 0)
			{
				continue;
			}

			var equals = pair.IndexOf('=');
			if (equals <= 0)
			{
				return false;
			}

			var key = pair.Substring(0, equals).Trim().ToLowerInvariant();
			var value = Unquote(pair.Substring(equals + 1).Trim());
			switch (key)
			{
				case "type":
					filter = filter with { Type = value };
					break;
				case "name":
					filter = filter with { Name = value };
					break;
				case "distance":
					if (!TryParseDistance(value, out var min, out var max))
					{
						return false;
					}

					filter = filter with { DistanceMin = min, DistanceMax = max };
					break;
				case "limit":
					if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0)
					{
						return false;
					}

					filter = filter with { Limit = limit };
					break;
				case "sort":
					if (!TryParseSort(value, out var sort))
					{
						return false;
					}

					filter = filter with { Sort = sort };
					break;
				default:
					return false;
			}
		}

		return true;
	}

	private static bool TryParseDistance(string value, out float? min, out float? max)
	{
		min = null;
		max = null;
		var range = value.Replace(" ", string.Empty);
		var separator = range.IndexOf("..", StringComparison.Ordinal);
		if (separator < 0)
		{
			if (!float.TryParse(range, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) || exact < 0f)
			{
				return false;
			}

			min = exact;
			max = exact;
			return true;
		}

		var minText = range.Substring(0, separator);
		var maxText = range.Substring(separator + 2);
		if (minText.Length > 0 && (!float.TryParse(minText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMin) || parsedMin < 0f))
		{
			return false;
		}

		if (maxText.Length > 0 && (!float.TryParse(maxText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMax) || parsedMax < 0f))
		{
			return false;
		}

		min = minText.Length == 0 ? 0f : float.Parse(minText, CultureInfo.InvariantCulture);
		max = maxText.Length == 0 ? null : float.Parse(maxText, CultureInfo.InvariantCulture);
		return true;
	}

	private static bool TryParseSort(string value, out SelectorSort sort)
	{
		switch (value.ToLowerInvariant())
		{
			case "nearest":
				sort = SelectorSort.Nearest;
				return true;
			case "furthest":
				sort = SelectorSort.Furthest;
				return true;
			case "random":
				sort = SelectorSort.Random;
				return true;
			case "arbitrary":
				sort = SelectorSort.Arbitrary;
				return true;
			default:
				sort = SelectorSort.None;
				return false;
		}
	}

	private static string Unquote(string value)
	{
		if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
		{
			return value.Substring(1, value.Length - 2);
		}

		return value;
	}
}
