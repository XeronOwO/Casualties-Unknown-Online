using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/summon</c> verb: create ONE world object — a world entity, an animal, or (with the <c>item</c> type
/// word) a loose item entity — at a position, through the game's own creation call.
///
/// <para>
/// The command is the control plane only — the cheat gate and the grammar. The mechanics are
/// <see cref="WorldSummonService"/>: the host creates and answers, a member's verb is a request whose answer
/// arrives on the wire. The gate runs before any position is resolved or anything is sent, the family's order: a
/// closed permission costs no round trip and no scene work.
/// </para>
///
/// <para>
/// A member's command returns no line: its answer is the host's, and it will print when it arrives. The host's
/// own command returns the host's answer, which the console prints at once.
/// </para>
/// </summary>
internal sealed class WorldSummonCommands(IWorldSummonControl summon, CheatVerbGate cheatVerbs)
{
	private readonly IWorldSummonControl _summon = summon;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;

	[ConsoleCommand("summon", "Create one world entity, animal or loose item entity at a position (a cheat verb).", CommandPermission.Anyone, WorldSummonParser.Usage, CommandArgumentKind.ResourceLocation, CommandArgumentKind.Text, Gate = ConsoleCommandGate.Cheat)]
	internal ConsoleAnswer Summon(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused($"Usage: {WorldSummonParser.Usage}");
		}

		var tokens = new List<string>(args.Count - 1);
		for (var i = 1; i < args.Count; i++)
		{
			tokens.Add(args[i]);
		}

		if (!WorldSummonParser.TryParse(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		if (_cheatVerbs.TryRefuse("summon", out var refusal))
		{
			return refusal;
		}

		return ConsoleAnswer.Ok(_summon.Summon(request.PrefabId, request.Kind, request.Position, request.StateText));
	}
}
