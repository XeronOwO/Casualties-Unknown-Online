using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative remote medical operation session domain. One session
/// owns one operation: start → active updates → one terminal EndCommitted
/// (Completed/Cancelled/TimedOut/Disconnected). Intermediate updates are state
/// deltas applied to the authoritative character/item snapshots and broadcast
/// as non-terminal progress; they are never independent final commits. The
/// service also owns item and target-limb reservations, timeout cleanup and
/// disconnect release so the later shrapnel/bandage stages reuse the same
/// lifecycle.
/// </summary>
internal sealed class MedicalOperationSessionService : IMedicalOperationControl, ICuoService
{
	private const float MinimumDeltaMl = 0.01f;
	private const int OperationTimeoutMs = 15000;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly IItemControl _items;
	private readonly IPlayerInteractionVisibility _visibility;
	private readonly ITimeSource _time;
	private readonly ILogger _log;
	private readonly PlayerCharacterAccess _access;
	private readonly MedicalOperationInjectionApplier _applier;
	private readonly ShrapnelOperationSessionService _shrapnel;

	private readonly Dictionary<ulong, OperationSession> _active = [];
	private readonly HashSet<ulong> _reservedItems = [];
	private readonly HashSet<(ulong Target, int Limb)> _reservedTargetLimbs = [];
	private ulong _nextOperationId = 1;
	private bool _disposed;

	public MedicalOperationSessionService(
		ISessionControl session,
		PacketSender sender,
		ICharacterDataControl characters,
		IItemControl items,
		IPlayerInteractionVisibility visibility,
		ITimeSource time,
		ItemKernelAuthority kernelAuthority,
		ILogger<MedicalOperationSessionService> log)
	{
		_session = session;
		_sender = sender;
		_items = items;
		_visibility = visibility;
		_time = time;
		_log = log;
		_access = new PlayerCharacterAccess(session, characters);
		_applier = new MedicalOperationInjectionApplier(_access, _items, kernelAuthority, session, log);
		_shrapnel = new ShrapnelOperationSessionService(
			session,
			sender,
			_access,
			_items,
			_visibility,
			_time,
			kernelAuthority,
			_reservedItems,
			operation => _active.Values.Any(s => s.Operator == operation),
			log);
		_shrapnel.StartAckReceived += FireStartAckReceived;
		_shrapnel.StateReceived += FireStateReceived;
		_shrapnel.EndCommittedReceived += FireEndCommittedReceived;

		_session.MemberRemoved += OnMemberRemoved;
		_session.SessionEnded += OnSessionEnded;
	}

	public event Action<MedicalOperationStartAckMsg>? StartAckReceived;
	public event Action<MedicalOperationStateMsg>? StateReceived;
	public event Action<MedicalOperationEndCommittedMsg>? EndCommittedReceived;

	// ---- IMedicalOperationControl: client send surface ----

	public void SendStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new MedicalOperationStartRequestMsg
		{
			TargetSteamId = targetSteamId,
			ItemInstanceId = itemInstanceId,
			LimbIndex = targetLimbIndex,
			Kind = MedicalOperationKind.Injection,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleStartRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationStartRequest, msg);
		}
	}

	public void SendUpdate(ulong operationId, float deltaMl)
	{
		if (deltaMl < MinimumDeltaMl)
		{
			return;
		}

		var msg = new MedicalOperationUpdateMsg
		{
			OperationId = operationId,
			DeltaMl = deltaMl,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUpdate(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, msg);
		}
	}

	public void SendEndRequest(ulong operationId, float totalMl)
	{
		var msg = new MedicalOperationEndRequestMsg
		{
			OperationId = operationId,
			TotalMl = totalMl,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleEndRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationEndRequest, msg);
		}
	}

	public void SendCancelRequest(ulong operationId)
	{
		var msg = new MedicalOperationCancelMsg { OperationId = operationId };
		if (_session.Role == SessionRole.Host)
		{
			HandleCancelRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationCancel, msg);
		}
	}

	// ---- IMedicalOperationControl: shrapnel session surface ----
	// Stage 2 shared shrapnel sessions use the same session envelope; the
	// shrapnel-specific multi-operator state lives in the shrapnel domain
	// service and is forwarded here through the same Start/State/End events.

	public void SendShrapnelStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex) =>
		_shrapnel.SendStartRequest(targetSteamId, itemInstanceId, targetLimbIndex);

	public void HandleShrapnelStartRequest(ulong sender, MedicalOperationStartRequestMsg msg) =>
		_shrapnel.HandleStartRequest(sender, msg);

	public void SendShrapnelUpdate(ulong operationId, ShrapnelPieceUpdate update) =>
		_shrapnel.SendUpdate(operationId, update);

	public void HandleShrapnelUpdate(ulong sender, MedicalOperationUpdateMsg msg) =>
		_shrapnel.HandleUpdate(sender, msg);

	public void SendShrapnelEndRequest(ulong operationId) =>
		_shrapnel.SendEndRequest(operationId);

	public void HandleShrapnelEndRequest(ulong sender, MedicalOperationEndRequestMsg msg) =>
		_shrapnel.HandleEndRequest(sender, msg);

	// ---- IMedicalOperationControl: host handlers ----

	public void HandleStartRequest(ulong sender, MedicalOperationStartRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (msg.Kind != MedicalOperationKind.Injection)
		{
			HandleShrapnelStartRequest(sender, msg);
			return;
		}

		var operation = sender;
		var target = msg.TargetSteamId;
		if (operation == target || operation == 0 || target == 0)
		{
			return;
		}

		if (!_access.IsInWorld(operation) || !_access.IsInWorld(target))
		{
			_log.LogWarning("[MedicalOps] refused start: {Operator} or {Target} is not in-world.", operation, target);
			RejectStart(operation, target, msg, "Participants are not in-world.");
			return;
		}

		if (!_visibility.HasLineOfSight(operation, target))
		{
			_log.LogInformation("[MedicalOps] refused start: {Operator} cannot see {Target}.", operation, target);
			RejectStart(operation, target, msg, "No line of sight.");
			return;
		}

		var userData = _access.GetCharacterData(operation);
		var targetData = _access.GetCharacterData(target);
		if (userData?.Health is not { } userHealth || !userHealth.Conscious || !userHealth.Alive)
		{
			_log.LogInformation("[MedicalOps] refused start: {Operator} is not conscious/alive.", operation);
			RejectStart(operation, target, msg, "Operator is not conscious/alive.");
			return;
		}

		if (targetData?.Health is not { } targetHealth || !targetHealth.Conscious || !targetHealth.Alive)
		{
			_log.LogInformation("[MedicalOps] refused start: {Target} is not conscious/alive and cannot receive an injection.", target);
			RejectStart(operation, target, msg, "Target is not conscious/alive.");
			return;
		}

		var itemIndex = FindUseItemIndex(userData, msg.ItemInstanceId);
		if (itemIndex < 0 || itemIndex >= userData.Items.Count)
		{
			_log.LogWarning("[MedicalOps] refused start: {Operator} has no usable item (requested {ItemId}).", operation, msg.ItemInstanceId);
			RejectStart(operation, target, msg, "Item not found.");
			return;
		}

		var originalItem = userData.Items[itemIndex];
		if (!RemoteMedicineCatalog.IsInjectableItem(originalItem.ItemId)
			|| !RemoteMedicineCatalog.TryCreatePlan(originalItem.Liquids, originalItem.ItemId, out _))
		{
			_log.LogWarning("[MedicalOps] refused start: {ItemId} (id {InstanceId}) is not an injectable medicine.", originalItem.ItemId, originalItem.InstanceId);
			RejectStart(operation, target, msg, "Item is not injectable.");
			return;
		}

		if (_active.Values.Any(s => s.Operator == operation))
		{
			_log.LogWarning("[MedicalOps] refused start: {Operator} already has an active medical operation.", operation);
			RejectStart(operation, target, msg, "Operator already has an active operation.");
			return;
		}

		if (_reservedItems.Contains(msg.ItemInstanceId))
		{
			_log.LogWarning("[MedicalOps] refused start: item {ItemId} is already reserved.", msg.ItemInstanceId);
			RejectStart(operation, target, msg, "Item is already reserved.");
			return;
		}

		if (msg.LimbIndex >= 0 && _reservedTargetLimbs.Contains((target, msg.LimbIndex)))
		{
			_log.LogWarning("[MedicalOps] refused start: target {Target} limb {Limb} is already reserved.", target, msg.LimbIndex);
			RejectStart(operation, target, msg, "Target limb is already reserved.");
			return;
		}

		var availableMl = originalItem.Liquids.Sum(l => l.Amount);
		if (availableMl <= 0f)
		{
			RejectStart(operation, target, msg, "Item is empty.");
			return;
		}

		var session = new OperationSession
		{
			OperationId = _nextOperationId++,
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

		_active.Add(session.OperationId, session);
		_reservedItems.Add(session.ItemInstanceId);
		if (session.LimbIndex >= 0)
		{
			_reservedTargetLimbs.Add((session.Target, session.LimbIndex));
		}

		_log.LogInformation(
			"[MedicalOps] started operation {OperationId}: {Operator} -> {Target}, item {ItemId} (id {InstanceId}), limb {Limb}, available {Available:F2} ml.",
			session.OperationId, operation, target, session.ItemId, session.ItemInstanceId, session.LimbIndex, availableMl);

		SendStartAck(new MedicalOperationStartAckMsg
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

	public void HandleUpdate(ulong sender, MedicalOperationUpdateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_active.TryGetValue(msg.OperationId, out var session) || session.Operator != sender)
		{
			if (_shrapnel.IsShrapnelOperation(msg.OperationId))
			{
				HandleShrapnelUpdate(sender, msg);
				return;
			}

			_log.LogWarning("[MedicalOps] update refused for unknown/wrong operation {OperationId} from {Sender}.", msg.OperationId, sender);
			return;
		}

		if (float.IsNaN(msg.DeltaMl) || float.IsInfinity(msg.DeltaMl) || msg.DeltaMl < MinimumDeltaMl)
		{
			_log.LogWarning("[MedicalOps] update {OperationId} ignored non-finite/zero delta {Delta:F3}.", msg.OperationId, msg.DeltaMl);
			return;
		}

		var before = session.CommittedMl;
		if (_applier.TryApplyDelta(session, msg.DeltaMl, out var state))
		{
			session.LastUpdateMs = _time.NowMs;
			var committed = session.CommittedMl - before;
			_log.LogInformation(
				"[MedicalOps] operation {OperationId} committed {Delta:F2} ml (total {Total:F2}, sequence {Sequence}).",
				session.OperationId, committed, session.CommittedMl, session.Sequence);
			PublishState(state!);
		}
	}

	public void HandleEndRequest(ulong sender, MedicalOperationEndRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_active.TryGetValue(msg.OperationId, out var session) || session.Operator != sender)
		{
			if (_shrapnel.IsShrapnelOperation(msg.OperationId))
			{
				HandleShrapnelEndRequest(sender, msg);
				return;
			}

			_log.LogWarning("[MedicalOps] end refused for unknown/wrong operation {OperationId} from {Sender}.", msg.OperationId, sender);
			return;
		}

		var finalMl = Math.Max(0f, Math.Min(session.AvailableMl, msg.TotalMl));
		if (finalMl - session.CommittedMl >= MinimumDeltaMl)
		{
			_applier.TryApplyDelta(session, finalMl - session.CommittedMl, out _);
		}

		_log.LogInformation(
			"[MedicalOps] operation {OperationId} ended with {Total:F2} ml committed.",
			session.OperationId, session.CommittedMl);
		Terminate(session, MedicalOperationTerminalReason.Completed);
	}

	public void HandleCancelRequest(ulong sender, MedicalOperationCancelMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_active.TryGetValue(msg.OperationId, out var session) || session.Operator != sender)
		{
			if (_shrapnel.IsShrapnelOperation(msg.OperationId))
			{
				_shrapnel.HandleCancelRequest(sender, msg);
				return;
			}

			_log.LogWarning("[MedicalOps] cancel refused for unknown/wrong operation {OperationId} from {Sender}.", msg.OperationId, sender);
			return;
		}

		_log.LogInformation(
			"[MedicalOps] operation {OperationId} cancelled with {Committed:F2} ml already committed.",
			session.OperationId, session.CommittedMl);
		Terminate(session, MedicalOperationTerminalReason.Cancelled);
	}

	// ---- Receiver event surface ----

	public void FireStartAckReceived(MedicalOperationStartAckMsg msg) => StartAckReceived?.Invoke(msg);

	public void FireStateReceived(MedicalOperationStateMsg msg) => StateReceived?.Invoke(msg);

	public void FireEndCommittedReceived(MedicalOperationEndCommittedMsg msg) => EndCommittedReceived?.Invoke(msg);

	// ---- ICuoService ----

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		_shrapnel.Update();
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var now = _time.NowMs;
		foreach (var session in _active.Values.ToList())
		{
			if (now - session.LastUpdateMs > OperationTimeoutMs)
			{
				_log.LogWarning("[MedicalOps] operation {OperationId} timed out after {Idle} ms idle.", session.OperationId, now - session.LastUpdateMs);
				Terminate(session, MedicalOperationTerminalReason.TimedOut);
			}
		}
	}

	void ICuoService.Stop()
	{
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_session.MemberRemoved -= OnMemberRemoved;
		_session.SessionEnded -= OnSessionEnded;
		_shrapnel.Dispose();
	}

	// ---- Host-side lifecycle ----

	private void Terminate(OperationSession session, MedicalOperationTerminalReason reason)
	{
		if (!_active.Remove(session.OperationId))
		{
			return;
		}

		_reservedItems.Remove(session.ItemInstanceId);
		if (session.LimbIndex >= 0)
		{
			_reservedTargetLimbs.Remove((session.Target, session.LimbIndex));
		}

		var end = _applier.BuildTerminal(session, reason);
		_log.LogInformation(
			"[MedicalOps] operation {OperationId} terminal {Reason}: committed {Committed:F2} ml, item {ItemAfter}, target health {Target}.",
			session.OperationId, reason, session.CommittedMl, end.ItemAfter?.Condition ?? -1f, session.Target);
		PublishEnd(end);
	}

	private void RejectStart(ulong operation, ulong target, MedicalOperationStartRequestMsg msg, string reason)
	{
		SendStartAck(new MedicalOperationStartAckMsg
		{
			Accepted = false,
			RejectReason = reason,
			OperatorSteamId = operation,
			TargetSteamId = target,
			ItemInstanceId = msg.ItemInstanceId,
			LimbIndex = msg.LimbIndex,
			Kind = msg.Kind,
		}, operation);
	}

	private void SendStartAck(MedicalOperationStartAckMsg msg, ulong operatorId)
	{
		if (operatorId == _session.LocalSteamId)
		{
			FireStartAckReceived(msg);
		}
		else
		{
			_sender.Send(operatorId, NetMsg.MedicalOperationStartAck, msg);
		}
	}

	private void PublishState(MedicalOperationStateMsg msg)
	{
		FireStateReceived(msg);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationState,
			msg);
	}

	private void PublishEnd(MedicalOperationEndCommittedMsg msg)
	{
		FireEndCommittedReceived(msg);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationEndCommitted,
			msg);
	}

	private void OnMemberRemoved(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_shrapnel.OnMemberRemoved(steamId);
		foreach (var session in _active.Values.ToList())
		{
			if (session.Operator == steamId || session.Target == steamId)
			{
				_log.LogWarning("[MedicalOps] operation {OperationId} terminated because {SteamId} left the session.", session.OperationId, steamId);
				Terminate(session, MedicalOperationTerminalReason.Disconnected);
			}
		}
	}

	private void OnSessionEnded()
	{
		_shrapnel.Clear();
		_active.Clear();
		_reservedItems.Clear();
		_reservedTargetLimbs.Clear();
	}

	private static int FindUseItemIndex(CharacterDataMsg data, ulong itemInstanceId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			if (data.Items[i].InstanceId == itemInstanceId)
			{
				return i;
			}
		}

		return -1;
	}
}
