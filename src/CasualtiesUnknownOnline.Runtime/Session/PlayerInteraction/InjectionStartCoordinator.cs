using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The injection family's START path, split out of
/// <see cref="MedicalOperationSessionService"/> when the target-body verdict put
/// that file against the architecture line gate. A start is its own sequence —
/// validate what the host owns, ask the TARGET's client whether its body allows
/// the injection, then re-check the host's own facts over the window the answer
/// took and open the session — while everything AFTER a session exists (updates,
/// end, cancel, idle timeout) stays with the service that owns the registry.
/// <para>
/// Nothing here is authoritative on its own: the item facts come from the host's
/// item authority, the claims from the shared claim book, and the body verdict
/// from the target's own client. This type sequences them and nothing else.
/// </para>
/// </summary>
internal sealed class InjectionStartCoordinator(
	PlayerCharacterAccess access,
	MedicalOperationClaims claims,
	MedicalOperationIdAllocator operationIds,
	MedicalOperationSessionPublisher publisher,
	MedicalTargetBodyGate bodyGate,
	ITimeSource time,
	Action<OperationSession> onSessionOpened,
	ILogger log)
{
	private readonly PlayerCharacterAccess _access = access;
	private readonly MedicalOperationClaims _claims = claims;
	private readonly MedicalOperationIdAllocator _operationIds = operationIds;
	private readonly MedicalOperationSessionPublisher _publisher = publisher;
	private readonly MedicalTargetBodyGate _bodyGate = bodyGate;
	private readonly ITimeSource _time = time;
	private readonly Action<OperationSession> _onSessionOpened = onSessionOpened;
	private readonly ILogger _log = log;

	internal void Handle(ulong sender, MedicalOperationStartRequestMsg msg)
	{
		var operation = sender;
		var target = msg.TargetSteamId;
		if (operation == target || operation == 0 || target == 0)
		{
			return;
		}

		if (!_access.IsInWorld(operation) || !_access.IsInWorld(target))
		{
			_log.LogWarning("[MedicalOps] refused start: {Operator} or {Target} is not in-world.", operation, target);
			_publisher.RejectStart(operation, target, msg, "Participants are not in-world.");
			return;
		}

		var userData = _access.GetCharacterData(operation);
		if (userData?.Health is not { } userHealth || !userHealth.Conscious || !userHealth.Alive)
		{
			_log.LogInformation("[MedicalOps] refused start: {Operator} is not conscious/alive.", operation);
			_publisher.RejectStart(operation, target, msg, "Operator is not conscious/alive.");
			return;
		}

		if (!InjectionStartValidator.TryValidateOperator(userData, msg.ItemInstanceId, out var itemIndex, out var itemReason))
		{
			_log.LogWarning(
				"[MedicalOps] refused start: {Operator} has no usable item (requested {ItemId}): {Reason}",
				operation, msg.ItemInstanceId, itemReason);
			_publisher.RejectStart(operation, target, msg, itemReason);
			return;
		}

		var originalItem = userData.Items[itemIndex];
		if (TryRejectClaimed(operation, target, msg))
		{
			return;
		}

		var availableMl = originalItem.Liquids.Sum(l => l.Amount);
		if (availableMl <= 0f)
		{
			_publisher.RejectStart(operation, target, msg, "Item is empty.");
			return;
		}

		_bodyGate.Begin(operation, target, msg.LimbIndex, msg.Kind, verdict =>
		{
			if (!verdict.Accepted)
			{
				_log.LogInformation("[MedicalOps] refused start: {Target}'s own body answered {Reason}", target, verdict.Reason);
				_publisher.RejectStart(operation, target, msg, verdict.Reason);
				return;
			}

			Commit(operation, target, msg, originalItem, availableMl);
		});
	}

	/// <summary>The shared-claim half of a start: the operator slot, the item instance and the target limb.</summary>
	private bool TryRejectClaimed(ulong operation, ulong target, MedicalOperationStartRequestMsg msg)
	{
		if (_claims.IsOperatorBusy(operation))
		{
			_log.LogWarning("[MedicalOps] refused start: {Operator} already has an active medical operation.", operation);
			_publisher.RejectStart(operation, target, msg, "Operator already has an active operation.");
			return true;
		}

		if (_claims.IsItemReserved(msg.ItemInstanceId))
		{
			_log.LogWarning("[MedicalOps] refused start: item {ItemId} is already reserved.", msg.ItemInstanceId);
			_publisher.RejectStart(operation, target, msg, "Item is already reserved.");
			return true;
		}

		if (msg.LimbIndex >= 0 && _claims.IsLimbReserved(target, msg.LimbIndex))
		{
			_log.LogWarning("[MedicalOps] refused start: target {Target} limb {Limb} is already reserved.", target, msg.LimbIndex);
			_publisher.RejectStart(operation, target, msg, "Target limb is already reserved.");
			return true;
		}

		return false;
	}

	/// <summary>
	/// Runs on the target's accept. The answer took a round trip, so the host's own
	/// facts are re-checked before the session opens: a departure or another
	/// operation's claim that landed in that window must not be overridden by a
	/// verdict that predates it.
	/// </summary>
	private void Commit(ulong operation, ulong target, MedicalOperationStartRequestMsg msg, CharacterItemMsg originalItem, float availableMl)
	{
		switch (MedicalStartRecheck.Run(_access, _claims, operation, target, msg, limbClaimApplies: true, out var rejectReason))
		{
			case MedicalStartRecheckOutcome.OperatorGone:
				_log.LogInformation("[MedicalOps] injection start dropped: {Operator} left while the target answered.", operation);
				return;
			case MedicalStartRecheckOutcome.Reject:
				_log.LogInformation("[MedicalOps] refused start after the target answered: {Reason}", rejectReason);
				_publisher.RejectStart(operation, target, msg, rejectReason);
				return;
		}

		var session = new OperationSession
		{
			OperationId = _operationIds.Next(),
			Operator = operation,
			Target = target,
			ItemInstanceId = originalItem.InstanceId,
			ItemId = originalItem.ItemId,
			LimbIndex = msg.LimbIndex,
			Kind = msg.Kind,
			AvailableMl = availableMl,
			OriginalLiquids = [.. originalItem.Liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })],
			LastUpdateMs = _time.NowMs,
		};

		_onSessionOpened(session);
		_claims.TryReserveItem(session.ItemInstanceId);
		_claims.TryReserveOperator(session.Operator);
		if (session.LimbIndex >= 0)
		{
			_claims.TryReserveLimb(session.Target, session.LimbIndex);
		}

		_log.LogInformation(
			"[MedicalOps] started operation {OperationId}: {Operator} -> {Target}, item {ItemId} (id {InstanceId}), limb {Limb}, available {Available:F2} ml.",
			session.OperationId, operation, target, session.ItemId, session.ItemInstanceId, session.LimbIndex, availableMl);

		_publisher.SendStartAck(new MedicalOperationStartAckMsg
		{
			OperationId = session.OperationId,
			Accepted = true,
			OperatorSteamId = operation,
			TargetSteamId = target,
			ItemInstanceId = session.ItemInstanceId,
			LimbIndex = session.LimbIndex,
			Kind = session.Kind,
		}, operation);
	}
}
