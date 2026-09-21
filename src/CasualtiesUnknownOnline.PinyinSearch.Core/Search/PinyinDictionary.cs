using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CasualtiesUnknownOnline.PinyinSearch.Core.Search;

/// <summary>
/// The hanzi → pinyin reading table, loaded once from the embedded
/// <c>pinyin_data.txt</c> (one character per line, <c>char: reading, reading</c>).
/// The fuzzy-sound alternates are generated while loading, so matching a
/// mistyped <c>zh/z</c>, <c>ch/c</c>, <c>sh/s</c> or a dropped trailing <c>g</c>
/// costs no extra work per query.
///
/// The table is vendored from PinIn (itself the table behind the game's
/// JustUnknownCharacters mod): 26k+ characters, including the polyphonic ones.
/// A missing or unknown character simply has no readings, which degrades the
/// match to the plain substring rule instead of failing.
/// </summary>
internal static class PinyinDictionary
{
	/// <summary>The embedded resource suffix the loader looks for (the file's logical name is namespace-qualified).</summary>
	private const string ResourceSuffix = ".pinyin_data.txt";

	/// <summary>The reading separator inside one line of the table.</summary>
	private static readonly string[] ReadingSeparator = [", "];

	private static readonly PinyinSyllable[] NoSyllables = [];
	private static Dictionary<char, PinyinSyllable[]>? _map;

	/// <summary>Number of characters that carry at least one reading — 0 when the embedded table is missing.</summary>
	public static int Count => Load().Count;

	/// <summary>
	/// The readings of one character (empty for anything that is not a
	/// character in the table, including plain ASCII and digits).
	/// </summary>
	public static PinyinSyllable[] Syllables(char c) =>
		Load().TryGetValue(c, out var found) ? found : NoSyllables;

	private static Dictionary<char, PinyinSyllable[]> Load()
	{
		if (_map is not null)
		{
			return _map;
		}

		var map = new Dictionary<char, PinyinSyllable[]>();
		var assembly = typeof(PinyinDictionary).Assembly;
		var resource = FindResource(assembly);
		if (resource is not null)
		{
			using var stream = assembly.GetManifestResourceStream(resource);
			if (stream is not null)
			{
				using var reader = new StreamReader(stream);
				string? line;
				while ((line = reader.ReadLine()) is not null)
				{
					AddLine(map, line);
				}
			}
		}

		_map = map;
		return map;
	}

	/// <summary>
	/// The logical resource name is a build detail (root namespace + folder), so
	/// the loader matches by suffix instead of hard-coding it: a namespace
	/// rename must not silently disable pinyin search.
	/// </summary>
	private static string? FindResource(Assembly assembly)
	{
		foreach (var name in assembly.GetManifestResourceNames())
		{
			if (name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
			{
				return name;
			}
		}

		return null;
	}

	private static void AddLine(Dictionary<char, PinyinSyllable[]> map, string line)
	{
		// "<char>: <reading>, <reading>" — anything shorter than four characters
		// cannot carry both a character and one reading.
		if (line.Length < 4)
		{
			return;
		}

		var character = line[0];
		var readings = line.Substring(2).TrimStart(' ', ':');
		var parts = readings.Split(ReadingSeparator, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return;
		}

		var syllables = new List<PinyinSyllable>(parts.Length);
		foreach (var part in parts)
		{
			if (ParseSyllable(part) is { } syllable && !ContainsReading(syllables, syllable))
			{
				syllables.Add(syllable);
			}
		}

		if (syllables.Count > 0)
		{
			map[character] = [.. syllables];
		}
	}

	/// <summary>
	/// Splits one reading into initial/final/tone and expands the fuzzy
	/// alternates. The reading format is <c>"zhong1"</c>, <c>"ce4"</c>,
	/// <c>"an1"</c>, <c>"nv3"</c> — a non-digit tail is not a reading and is
	/// skipped rather than crashing the load.
	/// </summary>
	private static PinyinSyllable? ParseSyllable(string reading)
	{
		if (reading.Length < 2)
		{
			return null;
		}

		var tone = reading.Substring(reading.Length - 1);
		if (tone[0] < '0' || tone[0] > '9')
		{
			return null;
		}

		var body = reading.Substring(0, reading.Length - 1);
		if (body.Length == 0)
		{
			return null;
		}

		// A vowel-initial reading ("an", "en", "ou", and the "v" spelling of ü)
		// has no initial; everything else starts with one, and zh/ch/sh are the
		// only two-letter initials.
		string initial;
		string final;
		var first = body[0];
		var hasInitial = first != 'a' && first != 'e' && first != 'i' && first != 'o' && first != 'u' && first != 'v';
		if (hasInitial)
		{
			var initialLength = body.Length > 1 && body[1] == 'h' ? 2 : 1;
			initial = body.Substring(0, initialLength);
			final = body.Substring(initialLength);
		}
		else
		{
			initial = string.Empty;
			final = body;
		}

		return new PinyinSyllable(InitialAlternates(initial), FinalAlternates(final), tone);
	}

	/// <summary>
	/// The fuzzy initials: c/ch, s/sh and z/zh are the same sound to a player who
	/// types them interchangeably. The reading's own initial stays first — the
	/// deduplication below and the matcher both read the leading alternate.
	/// </summary>
	private static string[] InitialAlternates(string initial)
	{
		if (initial.Length == 0)
		{
			return [];
		}

		var alternates = new List<string> { initial };
		// zh/ch/sh and their single-letter spellings are the same sound to a
		// player who types them interchangeably, so each side expands to the
		// other; the reading's own initial always stays first.
		var fuzzy = initial[0] switch
		{
			'c' => initial.Length > 1 ? "c" : "ch",
			's' => initial.Length > 1 ? "s" : "sh",
			'z' => initial.Length > 1 ? "z" : "zh",
			_ => null,
		};
		if (fuzzy is not null)
		{
			alternates.Add(fuzzy);
		}

		return [.. alternates];
	}

	/// <summary>The fuzzy finals: the trailing "g" of ang/eng/ing (and of the an/en/in they are confused with) is dropped or added freely.</summary>
	private static string[] FinalAlternates(string final)
	{
		if (final.Length == 0)
		{
			return [];
		}

		var alternates = new List<string> { final };
		if (final.EndsWith("ang", StringComparison.Ordinal)
			|| final.EndsWith("eng", StringComparison.Ordinal)
			|| final.EndsWith("ing", StringComparison.Ordinal))
		{
			alternates.Add(final.Substring(0, final.Length - 1));
		}
		else if (final.EndsWith("an", StringComparison.Ordinal)
			|| final.EndsWith("en", StringComparison.Ordinal)
			|| final.EndsWith("in", StringComparison.Ordinal))
		{
			alternates.Add(final + "g");
		}

		return [.. alternates];
	}

	/// <summary>
	/// The same reading repeated by two table entries must not double the
	/// matcher's state sets; only the leading (non-fuzzy) alternates and the
	/// tone identify a reading.
	/// </summary>
	private static bool ContainsReading(List<PinyinSyllable> readings, PinyinSyllable candidate)
	{
		foreach (var existing in readings)
		{
			if (existing.Initials.Length > 0 && candidate.Initials.Length > 0
				&& existing.Initials[0] == candidate.Initials[0]
				&& existing.Finals.Length > 0 && candidate.Finals.Length > 0
				&& existing.Finals[0] == candidate.Finals[0]
				&& existing.Tone == candidate.Tone)
			{
				return true;
			}
		}

		return false;
	}
}
