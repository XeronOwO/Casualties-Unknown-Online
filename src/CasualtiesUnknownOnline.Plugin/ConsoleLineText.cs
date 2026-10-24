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
	internal static string Resolve(OnlineUiContext ctx, ConsoleLine line)
	{
		if (line.Key is null)
		{
			return line.Text;
		}

		var template = line.Key switch
		{
			"console.ready" => ctx.T("console.ready"),
			"console.session_ended" => ctx.T("console.session_ended"),
			"console.command.host_only" => ctx.T("console.command.host_only"),
			_ => line.Text,
		};

		return line.Args is { Count: > 0 } && template != line.Text
			? string.Format(CultureInfo.InvariantCulture, template, [.. line.Args])
			: template;
	}
}
