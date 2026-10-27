using System;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Session.Commands;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The console's own prose in the client's language. A line that carries a message key is resolved here through the
/// catalogue, so the console follows the same language setting as the window. A line without a key — an echoed command, a
/// diagnostic, anything the console prints verbatim — keeps its own text.
///
/// <para>
/// The resolution is GENERIC on purpose. It used to be a switch over six <c>console.*</c> literals, kept so the catalogue's
/// "every declared key is read by product source" gate had call sites to see; the effect was that every OTHER keyed answer —
/// the whole <c>/op</c>, <c>/kick</c>, <c>/ban</c>, <c>/gamerule</c> family, keyed at the command itself — printed its English
/// text on a Chinese client (measured 2026-10-27 in a captured frame: the raw English sentence appeared while the keyed
/// sentence never did). Any key now resolves; a key the catalogue does not answer falls back to the line's own English text,
/// so a missing entry can never blank a line.
/// </para>
/// </summary>
internal static class ConsoleLineText
{
	/// <summary>
	/// Resolves one line through <paramref name="lookup"/> (the localization service's own lookup, so the line follows the
	/// client's language). Kept as a pure function of the lookup so the behaviour is unit-testable without a running UI: a
	/// keyed line takes the catalogue's sentence and formats its arguments, an unkeyed line and a key the catalogue does not
	/// answer keep the English text — a missing entry must never blank a line.
	/// </summary>
	internal static string Resolve(Func<string, string> lookup, ConsoleLine line)
	{
		if (line.Key is null)
		{
			return line.Text;
		}

		var template = lookup(line.Key);
		if (string.Equals(template, line.Key, StringComparison.Ordinal))
		{
			// The catalogue answered with the key itself: it has no entry, so the line keeps the sentence it was built with.
			return line.Text;
		}

		return line.Args is { Count: > 0 }
			? string.Format(CultureInfo.InvariantCulture, template, [.. line.Args])
			: template;
	}
}
