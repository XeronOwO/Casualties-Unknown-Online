using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The acceptance-only console verbs: commands that exist so a run can DRIVE a case its own UI cannot
/// reach, never so a player has a shortcut. They live in their own owner object because the console
/// service sits at the class-size cap and because a verb that exists for a run should be findable as a
/// group instead of scattered through the player-facing console.
///
/// <para>
/// <c>/traderreport</c> publishes ONE give report as this client, with the credit the caller names. The
/// trader give's credit is judged against the item's own row on the host, and proving the refusal and the
/// clamp needs a claim the game's own gesture cannot produce (the native method computes the credit from
/// the live item). Nothing is trusted through it: the host judges the report exactly as it judges the
/// game's own, which is the point — and the trader is addressed the same way the real path addresses it
/// (its position), so the run passes the position the trade menu's own report shows.
/// </para>
///
/// <para>
/// PERMISSION is the cheat family's, decided here rather than left implicit: a member-side verb that can
/// move a trader's credit is a cheat verb, so it goes through <see cref="CheatVerbGate"/> like
/// <c>/give</c>, <c>/summon</c> and <c>/heal</c> — the host's cheat switch must be open and its roster
/// must name the client, and each refusal names its own ground. A run therefore enables the switch
/// (<c>/gamerule allow_member_cheat_verbs true</c>) and grants the acting member (<c>/op</c>) before the
/// lever can fire. The verb is
/// member-only and refuses the host, which never reports its own trade actions.
/// </para>
/// </summary>
internal sealed class AcceptanceCommands(ISessionControl session, IWorldControl world, CheatVerbGate cheatVerbs)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;

	[ConsoleCommand("traderreport", "Acceptance only: publish one give report (item instance + claimed credit) at a trader position.", CommandPermission.Anyone, "/traderreport <x> <y> <itemInstanceId> <value>", Gate = ConsoleCommandGate.Cheat)]
	private ConsoleAnswer TraderReport(IReadOnlyList<string> args)
	{
		if (args.Count < 5
			|| !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
			|| !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
			|| !ulong.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemInstanceId)
			|| !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
		{
			return ConsoleAnswer.Refused("Usage: /traderreport <x> <y> <itemInstanceId> <value>");
		}

		if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
		{
			return ConsoleAnswer.Refused("Refused: the trader position must be finite — nothing was sent.");
		}

		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return ConsoleAnswer.Refused("Refused: a trader action report is a MEMBER's report — this verb runs on a guest in an active session.");
		}

		if (_cheatVerbs.TryRefuse("traderreport", out var refusal))
		{
			return ConsoleAnswer.Refused(refusal);
		}

		_world.SendTraderAction(new TraderActionMsg
		{
			Action = TraderActionKind.GiveItem,
			Position = new NetVector2Msg(x, y),
			ItemInstanceId = itemInstanceId,
			ItemValue = value,
		});
		return ConsoleAnswer.Ok($"Reported give of item #{itemInstanceId} worth {value} at ({x:0.###},{y:0.###}) as {_session.LocalSteamId}.");
	}
}
