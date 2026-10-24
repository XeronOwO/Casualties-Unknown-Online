using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Localization;

/// <summary>
/// Reads the flat JSON catalogues that hold the built-in UI text (`en.json`, `zh.json`, embedded in this assembly). The format
/// is deliberately one level deep — a plain object of key → sentence — because the keys are already stable identifiers and the
/// text is a data file a translator edits; a nested shape would only buy ceremony. The reader is strict: a malformed file, a
/// duplicate key or an unterminated string is refused BY NAME at load time, so a broken translation is a startup error with a
/// reason rather than a UI full of key names.
/// </summary>
internal static class LocalizationJson
{
	/// <summary>Loads one embedded catalogue file beside <paramref name="anchor"/>'s assembly.</summary>
	internal static IReadOnlyDictionary<string, string> LoadEmbedded(Type anchor, string fileName)
	{
		var assembly = anchor.Assembly;
		var suffix = "." + fileName;
		string? resource = null;
		foreach (var name in assembly.GetManifestResourceNames())
		{
			if (name.EndsWith(suffix, StringComparison.Ordinal))
			{
				resource = name;
				break;
			}
		}

		if (resource is null)
		{
			throw new InvalidOperationException($"{anchor.Name}: embedded catalogue '{fileName}' was not found in {assembly.GetName().Name}");
		}

		using var stream = assembly.GetManifestResourceStream(resource)
			?? throw new InvalidOperationException($"{anchor.Name}: embedded catalogue '{resource}' could not be opened");
		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
		return Parse(reader.ReadToEnd(), fileName);
	}

	/// <summary>Parses the flat object form. Case-sensitive keys, values are JSON strings only.</summary>
	internal static IReadOnlyDictionary<string, string> Parse(string json, string source)
	{
		var table = new Dictionary<string, string>(StringComparer.Ordinal);
		var i = 0;
		SkipWhitespace(json, ref i);
		Expect(json, ref i, '{', source);
		SkipWhitespace(json, ref i);
		if (i < json.Length && json[i] == '}')
		{
			i++;
			SkipWhitespace(json, ref i);
			EndOfFile(json, i, source);
			return table;
		}

		while (true)
		{
			SkipWhitespace(json, ref i);
			var key = ReadString(json, ref i, source);
			SkipWhitespace(json, ref i);
			Expect(json, ref i, ':', source);
			SkipWhitespace(json, ref i);
			var value = ReadString(json, ref i, source);
			if (table.ContainsKey(key))
			{
				throw new InvalidDataException($"{source}: duplicate key '{key}'");
			}

			table[key] = value;

			SkipWhitespace(json, ref i);
			if (i < json.Length && json[i] == ',')
			{
				i++;
				continue;
			}

			Expect(json, ref i, '}', source);
			SkipWhitespace(json, ref i);
			EndOfFile(json, i, source);
			return table;
		}
	}

	private static void EndOfFile(string json, int i, string source)
	{
		if (i != json.Length)
		{
			throw new InvalidDataException($"{source}: trailing content at offset {i}");
		}
	}

	private static void Expect(string json, ref int i, char expected, string source)
	{
		if (i >= json.Length || json[i] != expected)
		{
			throw new InvalidDataException($"{source}: expected '{expected}' at offset {i}");
		}

		i++;
	}

	private static void SkipWhitespace(string json, ref int i)
	{
		while (i < json.Length && char.IsWhiteSpace(json[i]))
		{
			i++;
		}
	}

	private static string ReadString(string json, ref int i, string source)
	{
		Expect(json, ref i, '"', source);
		var builder = new StringBuilder();
		while (i < json.Length)
		{
			var c = json[i++];
			if (c == '"')
			{
				return builder.ToString();
			}

			if (c != '\\')
			{
				builder.Append(c);
				continue;
			}

			if (i >= json.Length)
			{
				break;
			}

			var escape = json[i++];
			switch (escape)
			{
				case '"': builder.Append('"'); break;
				case '\\': builder.Append('\\'); break;
				case '/': builder.Append('/'); break;
				case 'n': builder.Append('\n'); break;
				case 'r': builder.Append('\r'); break;
				case 't': builder.Append('\t'); break;
				case 'b': builder.Append('\b'); break;
				case 'f': builder.Append('\f'); break;
				case 'u':
					if (i + 4 > json.Length)
					{
						throw new InvalidDataException($"{source}: truncated \\u escape at offset {i}");
					}

					builder.Append((char)Convert.ToInt32(json.Substring(i, 4), 16));
					i += 4;
					break;
				default:
					throw new InvalidDataException($"{source}: unknown escape '\\{escape}' at offset {i}");
			}
		}

		throw new InvalidDataException($"{source}: unterminated string at offset {i}");
	}
}
