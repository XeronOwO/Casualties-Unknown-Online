using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The console <c>/give</c> verb's mechanics: create ONE item for a player's
/// carried inventory, its properties already set, and hand it to the custody path
/// a cross-player transfer already takes (<see cref="PlayerInventoryTakeService.DeliverCreatedItem"/>).
///
/// <para>
/// The HOST is the creation authority. It is the side that can resolve a
/// definition to a prefab (<see cref="IItemGrantSource"/>), the side whose item
/// domain owns the instance id the created fact carries, and the side whose
/// character snapshots are the record the delivery writes. A member's verb is
/// therefore a REQUEST: the permission (the host's cheat switch and roster), the
/// definition and every property are judged HERE, against the host's own capture
/// — never on the member's word.
/// </para>
///
/// <para>
/// The member's console has nothing to print until the host answers, and a refusal
/// has to reach it: a closed permission would otherwise look exactly like a line
/// that did nothing. The answer is ONE sentence — the host's own — carried by
/// <see cref="PlayerInventoryGiveResultMsg"/> and printed where the operator typed
/// the verb.
/// </para>
/// </summary>
internal sealed class PlayerItemGiveService(
	ISessionControl session,
	PacketSender sender,
	PlayerInventoryTakeService take,
	IItemGrantSource creation,
	IMemberCommandRules memberRules,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerInventoryTakeService _take = take;
	private readonly IItemGrantSource _creation = creation;
	private readonly IMemberCommandRules _memberRules = memberRules;
	private readonly ILogger _log = log;

	/// <summary>The host's answer to a member's give request arrived — the console prints it.</summary>
	public event Action<PlayerInventoryGiveResultMsg>? Answered;

	/// <summary>
	/// A give answer arrived from the wire. Only a member has one: the host's own give
	/// returns its answer to its own console directly, so an answer here at any other
	/// role is an invariant made observable rather than the boundary itself (the frame
	/// router already enforces the direction).
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
	/// One <c>/give</c> invocation on this client. The host creates and delivers and
	/// returns the answer line; a member sends the request and returns null, because
	/// its answer arrives later through <see cref="Answered"/>.
	/// </summary>
	public string? Give(ulong targetSteamId, string definitionId, int slot, IReadOnlyList<string> propertyTokens)
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
				Properties = [.. propertyTokens],
			});
			_log.LogInformation("[Give] {Actor} requested {Definition} for {Target} (slot {Slot}, {Tokens} property token(s)) from the host.",
				_session.LocalSteamId, definitionId, targetSteamId, slot, propertyTokens.Count);
			return null;
		}

		return Apply(_session.LocalSteamId, targetSteamId, definitionId, slot, propertyTokens).Answer;
	}

	/// <summary>
	/// Host only: a member's give request arrived. The two cheat rulings are judged
	/// here, in the family's order (the switch first, then the roster), and every
	/// answer — refusal or applied — travels back as ONE sentence.
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

		if (msg.Properties.Count > ItemGiveParser.MaxPropertyTokens)
		{
			RefuseRequest(sender, msg, $"the request carries {msg.Properties.Count} property token(s); at most {ItemGiveParser.MaxPropertyTokens} are accepted.");
			return;
		}

		SendAnswer(sender, msg, Apply(sender, msg.TargetSteamId, msg.DefinitionId, msg.SlotIndex, msg.Properties));
	}

	/// <summary>
	/// The host's own operation, shared by its console and a member's request: parse
	/// the operator's property words, create the definition, apply the properties to
	/// the created capture BEFORE it is delivered, then let the custody path place
	/// it. A property that does not apply refuses the whole give — nothing is
	/// delivered, exactly as a merge that cannot apply one of its values writes none.
	/// </summary>
	private GiveOutcome Apply(ulong actor, ulong targetSteamId, string definitionId, int slot, IReadOnlyList<string> propertyTokens)
	{
		if (!ItemGiveParser.TryParseProperties(propertyTokens, out var writes, out var parseError))
		{
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, parseError);
			return Refused(parseError!);
		}

		if (!_creation.TryCreate(definitionId, out var item, out var createRefusal) || item is null)
		{
			// The port's contract names the ground; the fallback exists so a broken
			// implementation cannot answer with an empty "Refused:" line.
			var reason = createRefusal ?? $"the definition '{definitionId}' could not be created by this build.";
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, reason);
			return Refused(reason);
		}

		var changes = new List<string>(writes.Count);
		foreach (var write in writes)
		{
			if (!ItemDataWriteValues.TryApply(item, write, out var before, out var after, out var valueError))
			{
				_log.LogWarning("[Give] {Actor} -> {Target} refused on {Definition} (#{InstanceId}) {Property}: {Reason}",
					actor, targetSteamId, definitionId, item.InstanceId, write.Property, valueError);
				return Refused(valueError!);
			}

			changes.Add($"{ItemDataRenderer.PropertyName(write)} {before} -> {after}");
		}

		var deliveryRefusal = _take.DeliverCreatedItem(targetSteamId, item, slot);
		if (deliveryRefusal is not null)
		{
			_log.LogWarning("[Give] {Actor} -> {Target} refused: {Reason}", actor, targetSteamId, deliveryRefusal);
			return Refused(deliveryRefusal);
		}

		_log.LogInformation("[Give] {Actor} gave {Definition} (#{InstanceId}) to {Target} in slot {Slot}{Changes}.",
			actor, item.ItemId, item.InstanceId, targetSteamId, item.SlotIndex,
			changes.Count == 0 ? "" : " — " + string.Join("; ", changes));
		return new GiveOutcome(true, Describe(item, targetSteamId, changes), item.InstanceId, item.SlotIndex);
	}

	/// <summary>
	/// The one sentence both ends print: the item it created, the id its record
	/// carries, where it landed, and which properties changed on the way in.
	/// </summary>
	private static string Describe(CharacterItemMsg item, ulong targetSteamId, IReadOnlyList<string> changes)
	{
		var builder = new StringBuilder();
		builder.Append("Gave ").Append(item.ItemId)
			.Append(" (#").Append(item.InstanceId.ToString(CultureInfo.InvariantCulture)).Append(") to ")
			.Append(targetSteamId.ToString(CultureInfo.InvariantCulture))
			.Append(" in slot ").Append(item.SlotIndex.ToString(CultureInfo.InvariantCulture));
		if (changes.Count > 0)
		{
			builder.Append(" — ").Append(string.Join("; ", changes));
		}

		builder.Append('.');
		return builder.ToString();
	}

	private static GiveOutcome Refused(string reason) => new(false, $"Refused: {reason}", 0, -1);

	/// <summary>
	/// A request refused BEFORE the item is created — the host's two cheat rulings and
	/// the token bound. Logged here once, with the request's own context, and sent back
	/// as the same sentence the member reads; <see cref="Apply"/> logs the refusals that
	/// happen below this layer.
	/// </summary>
	private void RefuseRequest(ulong requester, PlayerInventoryGiveRequestMsg request, string reason)
	{
		_log.LogWarning("[Give] request from {Requester} for {Target} refused: {Reason}", requester, request.TargetSteamId, reason);
		SendAnswer(requester, request, Refused(reason));
	}

	/// <summary>
	/// Send the answer to a member's request. The answer shares the host log's REASON
	/// wording — the same reason reads identically on both ends — but it is its own
	/// sentence: the log line names the actor and carries the verb prefix, the answer
	/// carries the verdict and the reason. The APPLIED case is logged where the item was
	/// created (this is only the transport).
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
		});
	}

	/// <summary>What one give produced: the answer both ends print, and the identity only the host and its tests need.</summary>
	private readonly record struct GiveOutcome(bool Applied, string Answer, ulong ItemInstanceId, int SlotIndex);
}
