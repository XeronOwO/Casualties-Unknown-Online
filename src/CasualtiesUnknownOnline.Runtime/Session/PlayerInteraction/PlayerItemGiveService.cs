using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The console <c>/give</c> verb's mechanics: create ONE item for a player's carried
/// inventory, its state already set, and hand it to the custody path a cross-player transfer
/// already takes (<see cref="PlayerInventoryTakeService.DeliverCreatedItem"/>).
///
/// <para>
/// The HOST is the creation authority. It is the side that can resolve a definition to a
/// prefab (<see cref="IItemGrantSource"/>), the side whose item domain owns the instance id
/// the created fact carries, and the side whose character snapshots are the record the
/// delivery writes. A member's verb is therefore a REQUEST: the permission (the host's cheat
/// switch and roster), the definition and the state are judged HERE, against the host's own
/// capture — never on the member's word.
/// </para>
///
/// <para>
/// The state is the canonical SNBT text the member's own parser produced (empty means the
/// definition's own capture). The host re-parses it and hands it to the creation port, which
/// overlays it onto the prefab's capture and binds the result with the adapter's schema and
/// legality; a state the definition cannot carry refuses the whole give before the item
/// exists.
/// </para>
///
/// <para>
/// The member's console has nothing to print until the host answers, and a refusal has to
/// reach it: the answer is ONE sentence — the host's own — carried by
/// <see cref="PlayerInventoryGiveResultMsg"/> and printed where the operator typed the verb.
/// </para>
/// </summary>
internal sealed class PlayerItemGiveService(
	ISessionControl session,
	PacketSender sender,
	PlayerInventoryTakeService take,
	IItemGrantSource creation,
	IItemStateLanguage language,
	IMemberCommandRules memberRules,
	ILocalizationService localization,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerInventoryTakeService _take = take;
	private readonly IItemGrantSource _creation = creation;
	private readonly IItemStateLanguage _language = language;
	private readonly IMemberCommandRules _memberRules = memberRules;
	private readonly ILocalizationService _localization = localization;
	private readonly ILogger _log = log;

	/// <summary>
	/// One answer part: a catalogue key plus its values, and the English rendering of the same key at Debug so a log can answer
	/// "what did the member read". The <c>labelKey</c> parameter name is the localization census's convention: a literal passed
	/// here is a READ of the catalogue, which is what stops an orphan key from shipping.
	/// </summary>
	private ConsoleAnswerPartMsg AnswerPart(string labelKey, params string[] args)
	{
		_log.LogDebug("[Give] answer part: {Line}", _localization.Format(labelKey, args));
		var key = labelKey;
		return new ConsoleAnswerPartMsg { Key = key, Args = [.. args] };
	}

	/// <summary>The host's answer to a member's give request arrived — the console prints it.</summary>
	public event Action<PlayerInventoryGiveResultMsg>? Answered;

	/// <summary>
	/// A give answer arrived from the wire. Only a member has one: the host's own give
	/// returns its answer to its own console directly, so an answer here at any other role
	/// is an invariant made observable rather than the boundary itself (the frame router
	/// already enforces the direction).
	/// </summary>
	public void FireAnswered(PlayerInventoryGiveResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[Give] an answer for {Definition} arrived while this process is {Role} — dropped.",
				msg.DefinitionId, _session.Role);
			return;
		}

		Answered?.Invoke(msg);
	}

	/// <summary>
	/// One <c>/give</c> invocation on this client. The host creates and delivers and returns
	/// the answer line; a member sends the request and returns null, because its answer
	/// arrives later through <see cref="Answered"/>.
	/// </summary>
	public string? Give(ulong targetSteamId, string definitionId, int slot, string stateText)
	{
		if (!_session.SessionActive)
		{
			return "No active session — /give needs one: the host's own permission is the online switch, and a member's is the host's grant for the session.";
		}

		if (_session.Role == SessionRole.Guest)
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerInventoryGiveRequest, new PlayerInventoryGiveRequestMsg
			{
				TargetSteamId = targetSteamId,
				DefinitionId = definitionId,
				SlotIndex = slot,
				State = stateText,
			});
			_log.LogInformation("[Give] {Actor} requested {Definition} for {Target} (slot {Slot}, state {State}) from the host.",
				_session.LocalSteamId, definitionId, targetSteamId, slot, stateText.Length == 0 ? "(the definition's own)" : stateText);
			return null;
		}

		return Apply(_session.LocalSteamId, targetSteamId, definitionId, slot, stateText).Answer;
	}

	/// <summary>
	/// Host only: a member's give request arrived. The two cheat rulings are judged here, in
	/// the family's order (the switch first, then the roster), and every answer — refusal or
	/// applied — travels back as ONE sentence.
	/// </summary>
	public void HandleRequest(ulong sender, PlayerInventoryGiveRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_memberRules.CheatVerbsOpenToMembers)
		{
			RefuseRequest(sender, msg, "the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run /give.");
			return;
		}

		if (!_memberRules.AllowsCheatVerb(sender))
		{
			RefuseRequest(sender, msg, "the host's cheat roster does not name you, so /give cannot land. The host grants it with /op.");
			return;
		}

		if (msg.State.Length > ItemGiveParser.MaxStateLength)
		{
			RefuseRequest(sender, msg, $"the request carries {msg.State.Length} character(s) of state; at most {ItemGiveParser.MaxStateLength} are accepted.");
			return;
		}

		SendAnswer(sender, msg, Apply(sender, msg.TargetSteamId, msg.DefinitionId, msg.SlotIndex, msg.State));
	}

	/// <summary>
	/// The host's own operation, shared by its console and a member's request: re-parse the
	/// state, create the definition (the port overlays the state onto its own capture and
	/// binds it BEFORE an id exists), then let the custody path place it. A state that does
	/// not bind refuses the whole give — nothing is delivered.
	/// </summary>
	private GiveOutcome Apply(ulong actor, ulong targetSteamId, string definitionId, int slot, string stateText)
	{
		if (!ItemGiveParser.TryParseState(stateText, out var state, out var canonical, out var parseError))
		{
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, parseError);
			return Refused(parseError!);
		}

		if (!_creation.TryCreate(definitionId, state, out var item, out var createRefusal) || item is null)
		{
			// The port's contract names the ground; the fallback exists so a broken
			// implementation cannot answer with an empty "Refused:" line.
			var reason = createRefusal ?? $"the definition '{definitionId}' could not be created by this build.";
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, reason);
			return Refused(reason);
		}

		var delivery = _take.DeliverCreatedItem(targetSteamId, item, slot);
		if (!delivery.Delivered)
		{
			var reason = delivery.Refusal ?? $"the item could not be delivered to {targetSteamId}.";
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, reason);
			return Refused(reason);
		}

		// The answer names the state the item REALLY carries: the creation port binds the
		// operator's text against the prefab capture, and the bind may derive or drop a
		// value (a container's condition is its fill ratio; a zero stack is dropped). A give
		// with no state keeps the short answer — there is nothing the operator asked to set.
		var answered = canonical;
		if (canonical.Length > 0 && _language.TryDescribe(item, out var boundState))
		{
			answered = SnbtWriter.Write(boundState);
		}

		_log.LogInformation("[Give] {Actor} gave {Definition} (#{InstanceId}) to {Target}{Where}{State}.",
			actor, item.ItemId, item.InstanceId, targetSteamId, Where(delivery),
			answered.Length == 0 ? "" : " with state " + answered);
		return new GiveOutcome(true, Describe(item, targetSteamId, answered, delivery), item.InstanceId, delivery.SlotIndex, PartsOf(item, targetSteamId, answered, delivery));
	}

	/// <summary>
	/// The answer's clause for the rung that settled the item — always named, never implied: the slot, the
	/// carried or worn container (its definition and id), or the drop. One helper serves the answer and the
	/// log, so the two can never say different things.
	/// </summary>
	private static string Where(ItemDelivery delivery) => delivery.Destination switch
	{
		PlayerInventoryDestination.Container =>
			$" inside the {(delivery.SlotIndex < 0 ? "worn" : "carried")} container {delivery.ContainerDefinitionId ?? "container"} (#{delivery.ContainerId})",
		PlayerInventoryDestination.Drop => ", dropped at its position (the ladder's last rung)",
		_ => $" in slot {delivery.SlotIndex}",
	};

	/// <summary>
	/// The same answer as catalogue PARTS, one per line the member's console prints: the rung that settled the item (a slot, a
	/// worn or carried container, or the drop) and, when the operator gave the item a state, that state as its own line. The
	/// parts live beside <see cref="Describe"/> because they must say exactly the same thing — the text stays for the host's log
	/// and as the fallback a client without the entry reads.
	/// </summary>
	private List<ConsoleAnswerPartMsg> PartsOf(CharacterItemMsg item, ulong targetSteamId, string state, ItemDelivery delivery)
	{
		var target = targetSteamId.ToString(CultureInfo.InvariantCulture);
		var instance = item.InstanceId.ToString(CultureInfo.InvariantCulture);
		var parts = new List<ConsoleAnswerPartMsg>
		{
			delivery.Destination switch
			{
				PlayerInventoryDestination.Container => delivery.SlotIndex < 0
					? AnswerPart("give.answer.worn_container", item.ItemId, instance, target, delivery.ContainerDefinitionId ?? "unknown container", delivery.ContainerId.ToString(CultureInfo.InvariantCulture))
					: AnswerPart("give.answer.carried_container", item.ItemId, instance, target, delivery.ContainerDefinitionId ?? "unknown container", delivery.ContainerId.ToString(CultureInfo.InvariantCulture)),
				PlayerInventoryDestination.Drop => AnswerPart("give.answer.dropped", item.ItemId, instance, target),
				_ => AnswerPart("give.answer.slot", item.ItemId, instance, target, delivery.SlotIndex.ToString(CultureInfo.InvariantCulture)),
			},
		};

		if (state.Length > 0)
		{
			parts.Add(AnswerPart("give.answer.state", state));
		}

		return parts;
	}

	/// <summary>
	/// The one sentence both ends print: the item it created, the id its record carries, where
	/// it landed (the ladder rung that settled it), and the state it was given (the operator's own,
	/// canonical form).
	/// </summary>
	private static string Describe(CharacterItemMsg item, ulong targetSteamId, string state, ItemDelivery delivery)
	{
		var builder = new StringBuilder();
		builder.Append("Gave ").Append(item.ItemId)
			.Append(" (#").Append(item.InstanceId.ToString(CultureInfo.InvariantCulture)).Append(") to ")
			.Append(targetSteamId.ToString(CultureInfo.InvariantCulture))
			.Append(Where(delivery));

		if (state.Length > 0)
		{
			builder.Append(" with state ").Append(state);
		}

		builder.Append('.');
		return builder.ToString();
	}

	// A refusal keeps the text path (its reasons are a separate sweep): no parts, so the member's console prints the sentence
	// the host wrote rather than a half-keyed line.
	private static GiveOutcome Refused(string reason) => new(false, $"Refused: {reason}", 0, -1, []);

	/// <summary>
	/// A request refused BEFORE the item is created — the host's two cheat rulings and the
	/// state bound. Logged here once, with the request's own context, and sent back as the
	/// same sentence the member reads; <see cref="Apply"/> logs the refusals that happen below
	/// this layer.
	/// </summary>
	private void RefuseRequest(ulong requester, PlayerInventoryGiveRequestMsg request, string reason)
	{
		_log.LogWarning("[Give] request from {Requester} for {Target} refused: {Reason}", requester, request.TargetSteamId, reason);
		SendAnswer(requester, request, Refused(reason));
	}

	/// <summary>
	/// Send the answer to a member's request. The answer shares the host log's REASON wording —
	/// the same reason reads identically on both ends — but it is its own sentence: the log
	/// line names the actor and carries the verb prefix, the answer carries the verdict and the
	/// reason. The APPLIED case is logged where the item was created (this is only the
	/// transport).
	/// </summary>
	private void SendAnswer(ulong requester, PlayerInventoryGiveRequestMsg request, GiveOutcome outcome)
	{
		_sender.Send(requester, NetMsg.PlayerInventoryGiveResult, new PlayerInventoryGiveResultMsg
		{
			TargetSteamId = request.TargetSteamId,
			DefinitionId = request.DefinitionId,
			ItemInstanceId = outcome.ItemInstanceId,
			SlotIndex = outcome.SlotIndex,
			Applied = outcome.Applied,
			Answer = outcome.Answer,
			Parts = outcome.Parts,
		});
	}

	/// <summary>What one give produced: the answer both ends print, and the identity only the host and its tests need.
	/// <see cref="Parts"/> is the answer as catalogue keys, so the member's console prints it in the member's own language.</summary>
	private readonly record struct GiveOutcome(bool Applied, string Answer, ulong ItemInstanceId, int SlotIndex, List<ConsoleAnswerPartMsg> Parts);
}
