using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/kill</c> verb: put a player's body at zero brain health, which is exactly what the GAME'S
/// own console verb does — <c>PlayerCamera.main.body.brainHealth = 0f</c> followed by a log line
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c>, the
/// <c>new Command("kill", "Instantly kills the player character by setting their brain health to 0.",
/// …)</c> registration) — and <c>Body.alive</c> is <c>brainHealth &gt; 0f</c>.
///
/// <para>
/// The command is the control plane only: the grammar, the cheat gate and the selector verdict, in
/// the family's order (the gate first, so a closed permission is answered before anything is looked
/// up). The body mutation travels the same route <c>/heal</c> uses — the target's own client applies
/// it, the host judges and answers — because a body exists on its owner's client and nowhere else.
/// The operation is the only difference, so it rides the family's message with
/// <see cref="BodyOperation.Kill"/> instead of a second protocol.
/// </para>
/// </summary>
internal sealed class KillCommands(
	IHealResetControl healReset,
	CheatVerbGate cheatVerbs,
	ISessionControl session,
	IEntitySyncControl entities)
{
	private readonly IHealResetControl _healReset = healReset;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;

	[ConsoleCommand("kill", "Instantly kill the selected player's character by setting brain health to 0 (a cheat verb).", CommandPermission.Anyone, "/kill [<selector>]", CommandArgumentKind.Selector, Gate = ConsoleCommandGate.Cheat)]
	internal ConsoleAnswer Kill(IReadOnlyList<string> args)
	{
		if (_cheatVerbs.TryRefuse("kill", out var refusal))
		{
			return refusal;
		}

		var requested = new List<ulong>();
		if (args.Count >= 2)
		{
			var targets = CommandPlayerTargets.Resolve(_session, _entities, args[1]);
			if (targets.Count == 0)
			{
				return ConsoleAnswer.Refused($"No players match selector '{args[1]}'.", "selector.answer.no_match", args[1]);
			}

			requested.AddRange(targets);
		}
		else
		{
			requested.Add(_session.LocalSteamId);
		}

		return _healReset.Apply(requested, BodyOperation.Kill);
	}
}
