using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// ONE parser for the "state attached to a prefab id" argument the console's creation verbs take — <c>/give</c>
/// and <c>/summon</c> — so the two cannot drift about what a state text is: a compound, parsed by the family's one
/// SNBT parser, canonicalised by the one writer, and bounded by one length before it travels.
///
/// <para>
/// The bound applies to what TRAVELS as well as to what was typed: canonicalisation can grow the text (a plain
/// decimal gains its <c>d</c>, a single-quoted string gains escapes), so a line accepted under the typed length
/// must not be refused later by the host's canonical length. Empty or null means "the prefab's own captured
/// state", which is a legitimate answer rather than an error.
/// </para>
/// </summary>
internal static class ItemStateText
{
	/// <summary>The wire requests' own bound: the state text is re-parsed on the host, so its length is stated rather than unbounded.</summary>
	internal const int MaxLength = 4096;

	/// <summary>
	/// Parses one state argument. Null or empty means "the prefab's own captured state"; anything else must be a
	/// compound. <paramref name="canonical"/> is the text that travels, and it is what a lenient reader compares.
	/// </summary>
	internal static bool TryParse(string? text, out SnbtCompound? state, out string canonical, out string? error)
	{
		state = null;
		canonical = "";
		error = null;
		if (text is null || string.IsNullOrWhiteSpace(text))
		{
			return true;
		}

		if (text.Length > MaxLength)
		{
			error = $"the state is {text.Length} character(s); at most {MaxLength} are accepted.";
			return false;
		}

		if (!SnbtParser.TryParse(text, out var value, out var parseError))
		{
			error = $"The state is not SNBT: {parseError}";
			return false;
		}

		if (value is not SnbtCompound compound)
		{
			error = $"The state is a compound, e.g. {{<key>:<value>}} — got {value.Kind}.";
			return false;
		}

		state = compound;
		canonical = SnbtWriter.Write(compound);

		if (canonical.Length > MaxLength)
		{
			error = $"the state is {canonical.Length} character(s) in canonical form; at most {MaxLength} are accepted.";
			return false;
		}

		return true;
	}
}
