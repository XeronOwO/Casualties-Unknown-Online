using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The <c>/give</c> verb: create ONE item in one player's carried inventory with its
/// properties already set.
///
/// <para>
/// The command is the control plane only — the cheat gate, the selector verdict and
/// the grammar. The mechanics are <see cref="PlayerItemGiveService"/>: the host
/// creates and delivers, a member's verb is a request whose answer arrives on the
/// wire. The gate runs before the target is resolved, the family's order: a closed
/// permission is answered before anything is looked up.
/// </para>
///
/// <para>
/// A member's command returns no line: its answer is the host's, and it will print
/// when it arrives. The host's own command returns the host's answer, which the
/// console prints at once.
/// </para>
/// </summary>
internal sealed class ItemGiveCommands(
	IPlayerInteractionControl playerInteraction,
	CheatVerbGate cheatVerbs,
	ItemDataTargets targets,
	ILogger log)
{
	private readonly IPlayerInteractionControl _playerInteraction = playerInteraction;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ItemDataTargets _targets = targets;
	private readonly ILogger _log = log;

	[ConsoleCommand("give", "Give one item to a player's inventory, its state already set (a cheat verb).", CommandPermission.Anyone, ItemGiveParser.Usage, CommandArgumentKind.Selector, CommandArgumentKind.ResourceLocation, Gate = ConsoleCommandGate.Cheat)]
	internal ConsoleAnswer Give(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused($"Usage: {ItemGiveParser.Usage}", "give.answer.usage", ItemGiveParser.Usage);
		}

		var tokens = new List<string>(args.Count - 1);
		for (var i = 1; i < args.Count; i++)
		{
			tokens.Add(args[i]);
		}

		if (!ItemGiveParser.TryParse(tokens, out var request, out var error))
		{
			return ConsoleAnswer.Refused(error!);
		}

		if (_cheatVerbs.TryRefuse("give", out var refusal))
		{
			return refusal;
		}

		var owners = _targets.ResolveOwners(request.Selector, out var ownerRefusal);
		if (ownerRefusal is not null)
		{
			return ConsoleAnswer.Refused(ownerRefusal);
		}

		if (owners.Count > 1)
		{
			_log.LogWarning("[Command] /give refused: the selector '{Selector}' matched {Count} bodies ({Owners}).",
				request.Selector, owners.Count, ItemDataTargets.Owners(owners));
			return ConsoleAnswer.Refused($"That selector matched {owners.Count} bodies of {ItemDataTargets.Owners(owners)} — /give names ONE body, so name one player: @p, a selector with [limit=1], or the player's id.", "give.answer.refused_many_bodies", owners.Count.ToString(CultureInfo.InvariantCulture), ItemDataTargets.Owners(owners));
		}

		return ConsoleAnswer.Ok(_playerInteraction.GiveItem(owners[0], request.DefinitionId, request.Slot, request.StateText));
	}
}
