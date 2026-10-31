using CasualtiesUnknownOnline.Runtime.Session.Commands;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The grey half of a completion row: what the candidate IS, in the reader's language. Every key is a literal handed straight to <c>ctx.T</c>/<c>ctx.F</c>, which
/// is the shape the localisation census and the console page pin read, so each catalogue entry is provably read; a hint the catalogue cannot hold answers null and
/// the row shows nothing rather than English.
/// </summary>
/// <remarks>
/// Its own type because the overlay is the drawing surface and was already at the aggregate line limit: a switch over the hint vocabulary is content, not layout,
/// and it changes for reasons the overlay's panel work does not (a mod adds a hint kind, a catalogue gains a key).
/// </remarks>
internal static class CommandSuggestionHintText
{
	internal static string? Of(OnlineUiContext ctx, CommandSuggestion suggestion) => suggestion.Hint switch
	{
		CommandSuggestionHint.None => null,
		CommandSuggestionHint.Command => Command(ctx, suggestion.Args[0] as string ?? string.Empty),
		CommandSuggestionHint.MemberName => ctx.T("console.suggest.member_name"),
		CommandSuggestionHint.SteamId => ctx.T("console.suggest.steamid"),
		CommandSuggestionHint.SteamIdHex => ctx.T("console.suggest.steamid_hex"),
		CommandSuggestionHint.BannedSteamId => ctx.T("console.suggest.banned_steamid"),
		CommandSuggestionHint.BannedSteamIdHex => ctx.T("console.suggest.banned_steamid_hex"),
		CommandSuggestionHint.FilterBy => ctx.F("console.suggest.filter_by", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.FilterValue => ctx.F("console.suggest.value", suggestion.Args[0] ?? string.Empty, suggestion.Args[1] ?? string.Empty),
		CommandSuggestionHint.JsonEmpty => ctx.T("console.suggest.json_empty"),
		CommandSuggestionHint.JsonTemplate => ctx.T("console.suggest.json_template"),
		CommandSuggestionHint.SummonPosition => ctx.T("console.suggest.summon_position"),
		CommandSuggestionHint.BodySlot => ctx.T("console.suggest.body_slot"),
		CommandSuggestionHint.GamemodeValue => ctx.T("console.suggest.gamemode_value"),
		CommandSuggestionHint.HostRuleCurrent => ctx.F("console.suggest.rule_current", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.HostRuleValue => ctx.F("console.suggest.rule_value", suggestion.Args[0] ?? string.Empty),
		CommandSuggestionHint.ResourceLocation => ResourceLocation(ctx, suggestion),
		CommandSuggestionHint.ItemState => ctx.T("console.suggest.item_state"),
		CommandSuggestionHint.KeyedText => Keyed(ctx, suggestion),
		_ => null,
	};

	/// <summary>
	/// A content-id candidate's row: the reader's own name for the content, and what kind it is (`cu:waterbottle` + "bottled water (item)"). The id itself is the
	/// button's text; this is the grey half that makes a transliterated match readable (user's 2026-10-30 report: a pinyin hit listed a bare id with nothing else).
	/// </summary>
	private static string? ResourceLocation(OnlineUiContext ctx, CommandSuggestion suggestion)
	{
		var kind = suggestion.Args.Length > 1 ? suggestion.Args[0] as string : null;
		var name = suggestion.Args.Length > 1 ? suggestion.Args[1] as string : null;
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		var kindLabel = KindLabel(ctx, kind);
		return string.IsNullOrWhiteSpace(kindLabel) ? name : ctx.F("console.suggest.resource", name, kindLabel);
	}

	/// <summary>
	/// One content kind in the reader's language. A kind a mod invented has no catalogue entry, and then the raw tag is shown — the row says what it can, and
	/// never prints a key.
	/// </summary>
	private static string KindLabel(OnlineUiContext ctx, string? kind) => kind switch
	{
		"item" => ctx.T("console.suggest.kind.item"),
		"recipe" => ctx.T("console.suggest.kind.recipe"),
		"liquid" => ctx.T("console.suggest.kind.liquid"),
		"liquidtile" => ctx.T("console.suggest.kind.liquidtile"),
		"tile" => ctx.T("console.suggest.kind.tile"),
		"building" => ctx.T("console.suggest.kind.building"),
		"structure" => ctx.T("console.suggest.kind.structure"),
		"status" => ctx.T("console.suggest.kind.status"),
		"moodle" => ctx.T("console.suggest.kind.moodle"),
		null or "" => string.Empty,
		_ => kind,
	};

	/// <summary>
	/// A mod's own sentence: <c>Args[0]</c> is its catalogue key and the rest are the values that sentence places. The row is rendered by the client that draws
	/// it, in that client's language, exactly like a framework key.
	/// </summary>
	private static string? Keyed(OnlineUiContext ctx, CommandSuggestion suggestion)
	{
		if (suggestion.Args.Length == 0 || suggestion.Args[0] is not string key || key.Length == 0)
		{
			return null;
		}

		var arguments = new object?[suggestion.Args.Length - 1];
		for (var index = 1; index < suggestion.Args.Length; index++)
		{
			arguments[index - 1] = suggestion.Args[index];
		}

		return ctx.F(key, arguments);
	}

	/// <summary>
	/// One command's one-line description in the reader's language, or null when the catalogue has no entry (a hint never shows a key).
	/// </summary>
	private static string? Command(OnlineUiContext ctx, string command) => command switch
	{
		"traderreport" => ctx.T("console.cmd.traderreport.desc"),
		"op" => ctx.T("console.cmd.op.desc"),
		"deop" => ctx.T("console.cmd.deop.desc"),
		"help" => ctx.T("console.cmd.help.desc"),
		"clear" => ctx.T("console.cmd.clear.desc"),
		"players" => ctx.T("console.cmd.players.desc"),
		"rtt" => ctx.T("console.cmd.rtt.desc"),
		"whoami" => ctx.T("console.cmd.whoami.desc"),
		"gamemode" => ctx.T("console.cmd.gamemode.desc"),
		"heal" => ctx.T("console.cmd.heal.desc"),
		"kick" => ctx.T("console.cmd.kick.desc"),
		"ban" => ctx.T("console.cmd.ban.desc"),
		"unban" => ctx.T("console.cmd.unban.desc"),
		"gamerule" => ctx.T("console.cmd.gamerule.desc"),
		"data" => ctx.T("console.cmd.data.desc"),
		"give" => ctx.T("console.cmd.give.desc"),
		"spectate" => ctx.T("console.cmd.spectate.desc"),
		"save" => ctx.T("console.cmd.save.desc"),
		"summon" => ctx.T("console.cmd.summon.desc"),
		_ => null,
	};
}
