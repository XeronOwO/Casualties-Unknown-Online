using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The built-in resource-location candidate source for console completion.
/// CUO uses namespaced identifiers (<c>cuo:player</c>, <c>cuo:bandage</c>) as the
/// console-facing resource vocabulary; mods can declare a
/// <see cref="Abstractions.CommandArgumentKind.ResourceLocation"/> argument and
/// receive these suggestions without depending on Runtime internals.
/// </summary>
internal static class ConsoleResourceLocationCatalog
{
	private static readonly CommandSuggestion[] Candidates =
	[
		new("cuo:player", "A player entity"),
		new("cuo:bandage", "Bandage item"),
		new("cuo:dynamite", "Dynamite item"),
		new("cuo:keypad", "Keypad item"),
		new("cuo:world", "World resource"),
	];

	public static IReadOnlyList<CommandSuggestion> Suggest(string prefix) =>
		[.. Candidates.Where(c => c.Text.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))];
}
