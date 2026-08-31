using System;
using System.Collections.Generic;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Minimal flat JSON-object parser for the console's host-rule command. It
/// intentionally supports only the shape the host-rules editor needs: an
/// object of scalar <c>property: value</c> pairs. It is not a general JSON
/// parser; array/object nesting is accepted as an opaque scalar value so the
/// editor can reject it with a clear property-level error.
/// </summary>
public static class HostRulesJsonParser
{
	/// <summary>
	/// Parses one flat JSON object into an ordered dictionary of raw property
	/// values. Returns false and sets <paramref name="error"/> on malformed input.
	/// </summary>
	public static bool TryParse(string json, out IReadOnlyDictionary<string, string> values, out string? error)
	{
		var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		values = parsed;
		if (string.IsNullOrWhiteSpace(json))
		{
			error = "JSON object is empty.";
			return false;
		}

		var i = 0;
		SkipWhitespace(json, ref i);
		if (i >= json.Length || json[i] != '{')
		{
			error = "Expected '{' at the start of the JSON object.";
			return false;
		}

		i++;
		while (true)
		{
			SkipWhitespace(json, ref i);
			if (i >= json.Length)
			{
				error = "Unterminated JSON object.";
				return false;
			}

			if (json[i] == '}')
			{
				i++;
				SkipWhitespace(json, ref i);
				if (i < json.Length)
				{
					error = "Unexpected content after the JSON object.";
					return false;
				}

				values = parsed;
				error = null;
				return true;
			}

			if (!TryReadKey(json, ref i, out var key, out error))
			{
				return false;
			}

			SkipWhitespace(json, ref i);
			if (i >= json.Length || json[i] != ':')
			{
				error = $"Expected ':' after property '{key}'.";
				return false;
			}

			i++;
			SkipWhitespace(json, ref i);
			if (!TryReadValue(json, ref i, out var value, out error))
			{
				return false;
			}

			parsed[key] = value;

			SkipWhitespace(json, ref i);
			if (i >= json.Length)
			{
				error = "Unterminated JSON object.";
				return false;
			}

			if (json[i] == ',')
			{
				i++;
				continue;
			}

			if (json[i] == '}')
			{
				i++;
				SkipWhitespace(json, ref i);
				if (i < json.Length)
				{
					error = "Unexpected content after the JSON object.";
					return false;
				}

				values = parsed;
				error = null;
				return true;
			}

			error = "Expected ',' or '}' after a property value.";
			return false;
		}
	}

	private static bool TryReadKey(string json, ref int i, out string key, out string? error)
	{
		key = "";
		if (json[i] == '"')
		{
			i++;
			var builder = new StringBuilder();
			while (i < json.Length)
			{
				var c = json[i];
				if (c == '\\' && i + 1 < json.Length)
				{
					builder.Append(json[i + 1]);
					i += 2;
					continue;
				}

				if (c == '"')
				{
					i++;
					key = builder.ToString();
					error = null;
					return true;
				}

				builder.Append(c);
				i++;
			}

			error = "Unterminated quoted property name.";
			return false;
		}

		var start = i;
		while (i < json.Length && json[i] != ':' && !char.IsWhiteSpace(json[i]))
		{
			i++;
		}

		if (i == start)
		{
			error = "Expected a property name.";
			return false;
		}

		key = json.Substring(start, i - start);
		error = null;
		return true;
	}

	private static bool TryReadValue(string json, ref int i, out string value, out string? error)
	{
		value = "";
		if (json[i] == '"')
		{
			i++;
			var builder = new StringBuilder();
			while (i < json.Length)
			{
				var c = json[i];
				if (c == '\\' && i + 1 < json.Length)
				{
					builder.Append(json[i + 1]);
					i += 2;
					continue;
				}

				if (c == '"')
				{
					i++;
					value = builder.ToString();
					error = null;
					return true;
				}

				builder.Append(c);
				i++;
			}

			error = "Unterminated quoted value.";
			return false;
		}

		var start = i;
		while (i < json.Length && json[i] != ',' && json[i] != '}')
		{
			i++;
		}

		if (i == start)
		{
			error = "Expected a property value.";
			return false;
		}

		value = json.Substring(start, i - start).Trim();
		error = null;
		return true;
	}

	private static void SkipWhitespace(string text, ref int i)
	{
		while (i < text.Length && char.IsWhiteSpace(text[i]))
		{
			i++;
		}
	}
}
