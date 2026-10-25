using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The player rows a console command's selector expands against: the local body
/// plus every remote player the entity table knows, with the display names the
/// session carries. One expansion serves every command group that takes a
/// selector, so a new group cannot grow its own second vocabulary.
/// </summary>
internal static class CommandPlayerTargets
{
	/// <summary>Expands a selector token; an empty list means no player matched (the caller reports it).</summary>
	internal static IReadOnlyList<ulong> Resolve(ISessionControl session, IEntitySyncControl entities, string? selector)
	{
		var targets = new List<CommandSelectorResolver.Target>
		{
			new(session.LocalSteamId, true, entities.LocalPlayer.Position, DisplayName(session, session.LocalSteamId)),
		};

		var remotePlayers = entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			targets.Add(new(remote.SteamId, false, remote.Position, DisplayName(session, remote.SteamId)));
		}

		return CommandSelectorResolver.Resolve(selector, targets);
	}

	private static string? DisplayName(ISessionControl session, ulong steamId)
	{
		foreach (var member in session.Members)
		{
			if (member.SteamId == steamId)
			{
				// A member whose persona has not arrived yet is still NAMEABLE: the steam id is the one identifier the
				// session always holds, so `@a[name=7656…]` answers the same on every client instead of matching nothing
				// (and it is what an operator reads in the roster line when the persona is missing).
				return string.IsNullOrWhiteSpace(member.DisplayName)
					? steamId.ToString(CultureInfo.InvariantCulture)
					: member.DisplayName;
			}
		}

		return steamId.ToString(CultureInfo.InvariantCulture);
	}
}
