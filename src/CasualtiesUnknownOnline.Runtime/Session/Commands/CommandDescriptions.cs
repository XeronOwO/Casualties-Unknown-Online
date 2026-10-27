using System;
using CasualtiesUnknownOnline.Runtime.Localization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// A command's one-line description in the reader's language — the hint row under a completion and the console's <c>/help &lt;command&gt;</c> answer
/// say the SAME thing, so both ask here instead of each keeping its own table.
///
/// <para>
/// The keys are handed to <see cref="ILocalizationService.T"/> as LITERALS because the catalogue census reads exactly that shape: it proves every
/// declared entry has a reader, and a concatenated <c>console.cmd.&lt;name&gt;.desc</c> gives it nothing to read (its key-space rule understands a hole
/// only at the END of a literal). The table is the price of that guarantee; <c>CommandDescriptionCatalogueTests</c> holds the other half — a registered
/// command without an entry in both catalogues fails there instead of printing English.
/// </para>
///
/// <para>
/// It lives in its own type for a measured reason: the console service crossed its 600-line architecture limit when this table was inside it.
/// </para>
/// </summary>
internal static class CommandDescriptions
{
	/// <summary>
	/// The description of <paramref name="name"/> in the reader's language, or <paramref name="ownDescription"/> when the catalogue has no entry for
	/// it (a missing entry must never print a key, and an English fallback is what the player already reads for an unlisted command).
	/// </summary>
	internal static string Of(ILocalizationService? localization, string name, string? ownDescription)
	{
		if (localization is null)
		{
			return ownDescription ?? string.Empty;
		}

		var key = $"console.cmd.{name}.desc";
		var localized = name switch
		{
			"traderreport" => localization.T("console.cmd.traderreport.desc"),
			"op" => localization.T("console.cmd.op.desc"),
			"deop" => localization.T("console.cmd.deop.desc"),
			"help" => localization.T("console.cmd.help.desc"),
			"clear" => localization.T("console.cmd.clear.desc"),
			"players" => localization.T("console.cmd.players.desc"),
			"rtt" => localization.T("console.cmd.rtt.desc"),
			"whoami" => localization.T("console.cmd.whoami.desc"),
			"gamemode" => localization.T("console.cmd.gamemode.desc"),
			"heal" => localization.T("console.cmd.heal.desc"),
			"kick" => localization.T("console.cmd.kick.desc"),
			"ban" => localization.T("console.cmd.ban.desc"),
			"unban" => localization.T("console.cmd.unban.desc"),
			"gamerule" => localization.T("console.cmd.gamerule.desc"),
			"data" => localization.T("console.cmd.data.desc"),
			"give" => localization.T("console.cmd.give.desc"),
			"spectate" => localization.T("console.cmd.spectate.desc"),
			"save" => localization.T("console.cmd.save.desc"),
			"summon" => localization.T("console.cmd.summon.desc"),
			_ => null,
		};

		// The catalogue answers with the key itself when it has no entry: keep the command's own sentence then, never the key.
		if (string.IsNullOrEmpty(localized) || string.Equals(localized, key, StringComparison.Ordinal))
		{
			return ownDescription ?? string.Empty;
		}

		return localized ?? ownDescription ?? string.Empty;
	}
}
