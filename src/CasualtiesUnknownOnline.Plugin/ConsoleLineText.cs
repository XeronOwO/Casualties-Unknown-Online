using System;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Session.Commands;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The console's own prose in the client's language. A line that carries a message key is resolved here, where every key is a
/// literal `T("console.…")` call, so the catalogue's "every declared key is read by product source" gate sees the call site
/// and the console follows the same language setting as the window. A line without a key — an echoed command, a diagnostic,
/// anything the console prints verbatim — keeps its own text.
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

		var template = line.Key switch
		{
			"console.ready" => lookup("console.ready"),
			"console.session_ended" => lookup("console.session_ended"),
			"console.command.host_only" => lookup("console.command.host_only"),
			"console.command.empty" => lookup("console.command.empty"),
			"console.command.unknown" => lookup("console.command.unknown"),
			"console.chat.not_sent" => lookup("console.chat.not_sent"),
			_ => line.Text,
		};

		return line.Args is { Count: > 0 } && template != line.Text
			? string.Format(CultureInfo.InvariantCulture, template, [.. line.Args])
			: template;
	}
}
