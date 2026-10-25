using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The completion vocabulary the command console offers beyond the argument kinds the
/// registry knows by itself: the session's members (a display name, a decimal id or a
/// hex id) and the host's ban list, plus the command names a bare <c>/</c> prefix
/// completes to. Split out of <see cref="CommandConsoleService"/> at the aggregate-size
/// gate, not to decorate it: completing a token asks what the SESSION holds, while the
/// console scripts input, gates permissions and renders answers.
/// </summary>
internal static class CommandSuggestions
{
	/// <summary>Every way an operator may name a session member, suggested with the form they can type back.</summary>
	internal static IReadOnlyList<CommandSuggestion> Members(ISessionControl session, string prefix)
	{
		var result = new List<CommandSuggestion>();
		foreach (var member in session.Members)
		{
			if (!string.IsNullOrWhiteSpace(member.DisplayName)
				&& member.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(member.DisplayName, CommandSuggestionHint.MemberName));
			}

			var decimalId = member.SteamId.ToString(CultureInfo.InvariantCulture);
			if (decimalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(decimalId, CommandSuggestionHint.SteamId));
			}

			var hexId = "0x" + member.SteamId.ToString("X", CultureInfo.InvariantCulture);
			if (hexId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(hexId, CommandSuggestionHint.SteamIdHex));
			}
		}

		return result;
	}

	/// <summary>The host's ban list, in the two forms <c>/unban</c> accepts.</summary>
	internal static IReadOnlyList<CommandSuggestion> BannedSteamIds(IHostBanService bans, string prefix)
	{
		var result = new List<CommandSuggestion>();
		foreach (var steamId in bans.BannedSteamIds)
		{
			var decimalId = steamId.ToString(CultureInfo.InvariantCulture);
			if (decimalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(decimalId, CommandSuggestionHint.BannedSteamId));
			}

			var hexId = "0x" + steamId.ToString("X", CultureInfo.InvariantCulture);
			if (hexId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(hexId, CommandSuggestionHint.BannedSteamIdHex));
			}
		}

		return result;
	}

	/// <summary>The command names a bare <c>/</c> prefix completes to, ordered as the help list renders them.</summary>
	internal static IReadOnlyList<CommandSuggestion> CommandNames(IReadOnlyList<CommandDefinition> commands, string prefix) =>
		[.. commands
			.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.Select(c => new CommandSuggestion(c.Name, CommandSuggestionHint.Command, Args: [c.Name]))
			.OrderBy(c => c.Text, StringComparer.OrdinalIgnoreCase)];
}
