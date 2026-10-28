using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/heal</c> verb: reset the selected players' state immediately — the FULL reset
/// (<see cref="PlayerHealResetService"/>), as a cheat verb, not the item-based remote-medical
/// treatment and not the game's own local <c>heal</c> command.
///
/// <para>
/// The command is the control plane only: the grammar, the cheat gate and the selector verdict.
/// The gate runs before the selector is resolved — the family's order, so a closed permission is
/// answered before anything is looked up. The mechanics live in <see cref="IHealResetControl"/>:
/// the host resets its own body and instructs the member targets, a member's verb is a request
/// whose answer arrives on the wire. A member's command returns no line: its answer is the host's,
/// and it prints when it arrives.
/// </para>
///
/// <para>
/// The verb has no Minecraft Java template to follow: that edition has no vanilla <c>/heal</c>
/// (fetched from <c>minecraft.wiki/w/Commands</c> on 2026-10-18), so the shape is the user's own
/// ruling — the command exists in CUO's console as a cheat reset, and its argument is the same
/// relative selector every other CUO player-targeting verb takes.
/// </para>
/// </summary>
internal sealed class HealResetCommands(
	IHealResetControl healReset,
	CheatVerbGate cheatVerbs,
	ISessionControl session,
	IEntitySyncControl entities)
{
	private readonly IHealResetControl _healReset = healReset;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;

	[ConsoleCommand("heal", "Immediately fully reset the selected player(s): health, limbs, severed limbs and the hollow debuff (a cheat verb).", CommandPermission.Anyone, "/heal <selector>", CommandArgumentKind.Selector, Gate = ConsoleCommandGate.Cheat)]
	internal ConsoleAnswer Heal(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused("Usage: /heal <selector>");
		}

		if (_cheatVerbs.TryRefuse("heal", out var refusal))
		{
			return refusal;
		}

		var targets = CommandPlayerTargets.Resolve(_session, _entities, args[1]);
		if (targets.Count == 0)
		{
			return ConsoleAnswer.Refused($"No players match selector '{args[1]}'.", "selector.answer.no_match", args[1]);
		}

		return _healReset.Reset(targets);
	}
}
