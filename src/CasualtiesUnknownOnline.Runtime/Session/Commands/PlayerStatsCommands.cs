using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/deaths</c> verb: how many times a character has died in this run. The count is a durable fact of
/// the character — it rides the character snapshot across a save, a continue and a reconnect — so the answer
/// is a reading rather than a live computation: a player asks its own captured count, and the host may ask
/// about any member by reading the row that member's own client reported.
///
/// <para>
/// The console is the diagnostic surface for a run statistic (docs/design/cross-cutting/observability.md:
/// a log is never the place a player's answer lives, and an administrative readout is a console verb); a HUD
/// readout would be a separate product decision and stays a residual on the ticket. The verb takes no
/// cheat gate: reading a death count changes nothing.
/// </para>
/// </summary>
internal sealed class PlayerStatsCommands(
	ISessionControl session,
	CharacterDataStore characterData,
	ILocalCharacterCapture localCapture) : IConsoleCommandGroup
{
	private readonly ISessionControl _session = session;
	private readonly CharacterDataStore _characterData = characterData;
	private readonly ILocalCharacterCapture _localCapture = localCapture;

	[ConsoleCommand("deaths", "How many times a character has died in this run (your own, or another player's on the host).", CommandPermission.Anyone, "/deaths [<steamId|displayName>]", CommandArgumentKind.PlayerOrSteamId)]
	internal ConsoleAnswer Deaths(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			var own = _localCapture.CaptureLocal()?.Deaths ?? 0;
			return ConsoleAnswer.Ok(
				$"You have died {own} time(s) in this run.",
				"console.answer.deaths.self",
				own.ToString(CultureInfo.InvariantCulture));
		}

		if (_session.Role != SessionRole.Host)
		{
			return ConsoleAnswer.Refused(
				"Only the host may ask about another player's deaths.",
				"console.answer.deaths.host_only");
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return ConsoleAnswer.Refused($"Unknown member '{args[1]}'.", "admin.answer.unknown_member", args[1]);
		}

		var displayName = _session.Members.FirstOrDefault(member => member.SteamId == steamId)?.DisplayName ?? args[1];
		if (!_characterData.TryGetDeaths(steamId, out var deaths))
		{
			return ConsoleAnswer.Refused(
				$"{displayName} has not reported a character yet.",
				"console.answer.deaths.no_report",
				displayName);
		}

		return ConsoleAnswer.Ok(
			$"{displayName} has died {deaths} time(s) in this run.",
			"console.answer.deaths.other",
			displayName,
			deaths.ToString(CultureInfo.InvariantCulture));
	}
}
