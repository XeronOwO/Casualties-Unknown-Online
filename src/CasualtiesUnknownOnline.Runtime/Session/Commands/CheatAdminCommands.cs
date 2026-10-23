using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The cheat-permission administration group: <c>/op</c> and <c>/deop</c>. It is its
/// own owner because it is a separate responsibility from membership administration
/// (<c>/kick</c>, <c>/ban</c>) and from the rules (<c>/gamerule</c>): it edits WHO
/// may run the cheat family, which is the roster a member's own console reads.
///
/// <para>
/// Both verbs are <see cref="CommandPermission.HostOnly"/> — a member cannot op
/// itself or another member — and the roster refuses the remaining standings by name
/// (the host itself, a non-member, an IP-direct session). Every successful edit is
/// announced to every member at that moment: the list governs verbs members type in
/// the world they are already in, so waiting for the next world entry would leave a
/// stale local answer.
/// </para>
/// </summary>
internal sealed class CheatAdminCommands(
	ISessionControl session,
	CheatPermissions permissions,
	ICheatPermissionAnnouncement announcement,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly CheatPermissions _permissions = permissions;
	private readonly ICheatPermissionAnnouncement _announcement = announcement;
	private readonly ILogger _log = log;

	[ConsoleCommand("op", "Host only: grant another player the cheat verbs (/data's write verbs, /give, /summon, /heal, /gamerule).", CommandPermission.HostOnly, "/op <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal string Op(IReadOnlyList<string> args) => Edit(args, grant: true);

	[ConsoleCommand("deop", "Host only: revoke another player's cheat verbs.", CommandPermission.HostOnly, "/deop <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal string Deop(IReadOnlyList<string> args) => Edit(args, grant: false);

	private string Edit(IReadOnlyList<string> args, bool grant)
	{
		var verb = grant ? "op" : "deop";
		if (args.Count < 2)
		{
			return $"Usage: /{verb} <steamId|displayName>";
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return $"Unknown member '{args[1]}'.";
		}

		if (grant)
		{
			if (!_permissions.Grant(steamId, out var grantRefusal))
			{
				// The one refusal that still re-announces: "already holds" is exactly the
				// case a MEMBER may be holding a stale CLOSED list (the announcement that
				// carried the earlier grant was swallowed, and a closed local list refuses
				// the verb without ever reaching this host). Re-sending the roster is the
				// remedy the member's own refusal names ("The host grants it with /op"),
				// and it is free because applying an identical roster is a no-op.
				if (steamId != 0 && _permissions.IsGranted(steamId))
				{
					_announcement.AnnounceAll();
					_log.LogInformation("[Command] /{Verb} repeated for {SteamId}: the roster already names it — re-announced so a member holding a stale closed list converges.",
						verb, steamId);
					return $"{steamId} already holds the cheat verbs; the roster is re-announced to every member so a stale local list converges.";
				}

				_log.LogWarning("[Command] /{Verb} refused for {SteamId}: {Reason}", verb, steamId, grantRefusal);
				return grantRefusal;
			}
		}
		else if (!_permissions.Revoke(steamId, out var revokeRefusal))
		{
			_log.LogWarning("[Command] /{Verb} refused for {SteamId}: {Reason}", verb, steamId, revokeRefusal);
			return revokeRefusal;
		}

		_announcement.AnnounceAll();
		var label = CommandMemberTargets.Label(_session, steamId);
		_log.LogInformation("[Command] /{Verb} changed the cheat roster: {Member} {Action}; the roster now names {Count} member(s).",
			verb, label, grant ? "granted" : "revoked", _permissions.Granted.Count);
		return grant
			? $"Granted the cheat verbs to {label}; the roster is announced to every member."
			: $"Revoked the cheat verbs from {label}; the roster is announced to every member.";
	}
}
