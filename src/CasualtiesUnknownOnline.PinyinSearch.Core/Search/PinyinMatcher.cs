using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.PinyinSearch.Core.Search;

/// <summary>
/// The pinyin search matcher: a C# port of PinIn's NFA/backtracking algorithm
/// (via the standalone JustUnknownCharacters mod). It answers the one question
/// a search box asks — does this query match somewhere inside this name? — and
/// understands full pinyin, initials, mixed Chinese/pinyin input, polyphonic
/// characters, fuzzy sounds and a missing tone digit.
///
/// The matcher is pure: it reads <see cref="PinyinDictionary"/> and nothing
/// else, so both the native crafting search box and the console's resource
/// completion can share the exact same matching rules.
/// </summary>
internal static class PinyinMatcher
{
	/// <summary>
	/// True when <paramref name="filter"/> matches <paramref name="name"/> by
	/// pinyin, trying every start position (Contains semantics). An empty filter
	/// matches everything, mirroring the native substring rule it extends.
	/// </summary>
	public static bool Contains(string name, string filter)
	{
		if (string.IsNullOrEmpty(filter))
		{
			return true;
		}

		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		for (var start = 0; start < name.Length; start++)
		{
			if (MatchFrom(name, start, filter, 0))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// The recursive NFA step (PinIn's <c>Matcher.check()</c>): consume the
	/// filter from <paramref name="filterPos"/> starting at
	/// <paramref name="namePos"/>, trying every consumption length the current
	/// character allows.
	/// </summary>
	private static bool MatchFrom(string name, int namePos, string filter, int filterPos)
	{
		// The filter is exhausted — every position it reached is a match, which
		// is what makes a partial query ("sheng" inside a longer name) work.
		if (filterPos >= filter.Length)
		{
			return true;
		}

		if (namePos >= name.Length)
		{
			return false;
		}

		var character = name[namePos];
		var consumed = MatchCharacter(character, PinyinDictionary.Syllables(character), filter, filterPos);

		if (namePos == name.Length - 1)
		{
			// The last character must consume exactly what is left: a partial
			// final would otherwise let a half-typed syllable match.
			return consumed.Contains(filter.Length - filterPos);
		}

		foreach (var length in consumed)
		{
			if (length > 0 && MatchFrom(name, namePos + 1, filter, filterPos + length))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Every filter length one character can consume (PinIn's IndexSet, reduced
	/// to the lengths this matcher needs): the literal character, each reading's
	/// initial and final phonemes, its tone, and the single-letter initial
	/// abbreviation.
	/// </summary>
	private static HashSet<int> MatchCharacter(char character, PinyinSyllable[] syllables, string filter, int filterPos)
	{
		var consumed = new HashSet<int>();

		// The literal character — this also carries plain ASCII and digits, so a
		// name that is not Chinese at all still matches by the ordinary rule.
		if (filterPos < filter.Length && CharsEqual(filter[filterPos], character))
		{
			consumed.Add(1);
		}

		if (syllables.Length == 0)
		{
			return consumed;
		}

		foreach (var syllable in syllables)
		{
			var states = new HashSet<int> { 0 };

			if (syllable.Initials.Length > 0)
			{
				states = MatchPhonemeSet(syllable.Initials, filter, filterPos, states);
				if (states.Count == 0)
				{
					continue; // a present initial must match
				}
			}

			// The states reached after the initial are kept: they are the
			// tone-tolerant readings ("xian" also matches "xian1").
			foreach (var state in states)
			{
				consumed.Add(state);
			}

			if (syllable.Finals.Length > 0)
			{
				states = MatchPhonemeSet(syllable.Finals, filter, filterPos, states);
				if (states.Count == 0)
				{
					continue; // a present final must match
				}
			}

			foreach (var state in states)
			{
				consumed.Add(state);
			}

			// The tone is optional: failing it leaves the states above intact.
			if (syllable.Tone.Length > 0)
			{
				foreach (var state in MatchPhoneme(syllable.Tone, filter, filterPos, states))
				{
					consumed.Add(state);
				}
			}

			// Initial abbreviation ("xws" for xian-wei-sheng): consuming one
			// letter is enough when it is the initial's first letter.
			if (filterPos < filter.Length)
			{
				foreach (var initial in syllable.Initials)
				{
					if (initial.Length > 0 && CharsEqual(initial[0], filter[filterPos]))
					{
						consumed.Add(1);
						break;
					}
				}
			}
		}

		return consumed;
	}

	/// <summary>
	/// One phoneme's alternates against the filter (PinIn's
	/// <c>Phoneme.match(String, IndexSet, int, boolean)</c>): each incoming
	/// offset may advance by a full alternate, or by the partial alternate that
	/// ends exactly at the filter's end.
	/// </summary>
	private static HashSet<int> MatchPhonemeSet(string[] phonemes, string filter, int start, HashSet<int> offsets)
	{
		var result = new HashSet<int>();

		foreach (var offset in offsets)
		{
			foreach (var phoneme in phonemes)
			{
				if (phoneme.Length == 0)
				{
					result.Add(offset); // an empty alternate is a pass-through
					continue;
				}

				var position = start + offset;
				if (position + phoneme.Length <= filter.Length && MatchesAt(phoneme, filter, position))
				{
					result.Add(offset + phoneme.Length);
				}

				if (position < filter.Length)
				{
					var partial = PartialMatchLength(phoneme, filter, position);
					if (position + partial == filter.Length)
					{
						result.Add(offset + partial);
					}
				}
			}
		}

		return result;
	}

	/// <summary>The tone variant: a full alternate only, with no partial tail (PinIn's <c>Phoneme.match(String, int, boolean)</c>).</summary>
	private static HashSet<int> MatchPhoneme(string phoneme, string filter, int start, HashSet<int> offsets)
	{
		var result = new HashSet<int>();

		foreach (var offset in offsets)
		{
			var position = start + offset;
			if (position + phoneme.Length <= filter.Length && MatchesAt(phoneme, filter, position))
			{
				result.Add(offset + phoneme.Length);
			}
		}

		return result;
	}

	private static bool MatchesAt(string phoneme, string filter, int position)
	{
		for (var i = 0; i < phoneme.Length; i++)
		{
			if (!CharsEqual(phoneme[i], filter[position + i]))
			{
				return false;
			}
		}

		return true;
	}

	private static int PartialMatchLength(string phoneme, string filter, int position)
	{
		var length = 0;
		var max = Math.Min(phoneme.Length, filter.Length - position);
		while (length < max && CharsEqual(phoneme[length], filter[position + length]))
		{
			length++;
		}

		return length;
	}

	/// <summary>Matching is case-insensitive; the query is never rewritten, so the caller's text is preserved.</summary>
	private static bool CharsEqual(char left, char right) =>
		char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
}
