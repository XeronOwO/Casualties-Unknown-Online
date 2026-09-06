using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-authoritative Stage 3 medical operation domain. It reuses the
/// generic medical session envelope (Start → updates → one EndCommitted) and
/// the shared item/limb reservations, and specializes the update payload for
/// bandage, splint/tourniquet removal, dislocation, AED, manual defibrillation
/// and amputation. All actions are exclusive (one operator at a time).
/// </summary>
internal sealed class OtherMedicalOperationSessionService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess access,
	IItemControl items,
	IPlayerInteractionVisibility visibility,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority,
	HashSet<ulong> sharedReservedItems,
	HashSet<(ulong Target, int Limb)> sharedReservedTargetLimbs,
	Func<ulong, bool> hasActiveInjectionOrShrapnel,
	MedicalOperationIdAllocator operationIds,
	ILogger log)
{
	private const int OperationTimeoutMs = 20000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = access;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ITimeSource _time = time;
	private readonly HashSet<ulong> _sharedReservedItems = sharedReservedItems;
	private readonly HashSet<(ulong Target, int Limb)> _sharedReservedTargetLimbs = sharedReservedTargetLimbs;
	private readonly Func<ulong, bool> _hasActiveInjectionOrShrapnel = hasActiveInjectionOrShrapnel;
	private readonly MedicalOperationIdAllocator _operationIds = operationIds;
	private readonly ILogger _log = log;
	private readonly OtherMedicalOperationApplier _applier = new(access, items, kernelAuthority, session);
	private readonly OtherMedicalRemovalApplier _removal = new(access, items, kernelAuthority, session, log);

	private readonly Dictionary<ulong, OtherMedicalOperationSession> _sessions = [];
	private bool _disposed;

	public event Action<MedicalOperationStartAckMsg>? StartAckReceived;
	public event Action<MedicalOperationStateMsg>? StateReceived;
	public event Action<MedicalOperationEndCommittedMsg>? EndCommittedReceived;

	// ---- Client send surface ----

	public void SendStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex, MedicalOperationKind kind)
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
			Kind = kind,
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

	public void SendUpdate(ulong operationId, MedicalOperationUpdateAction action, float value1 = 0f, float value2 = 0f, float value3 = 0f, bool flag1 = false)
	{
		var msg = new MedicalOperationUpdateMsg
		{
			OperationId = operationId,
			Action = (int)action,
			Value1 = value1,
			Value2 = value2,
			Value3 = value3,
			Flag1 = flag1,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUpdate(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, msg, reliable: true);
		}
	}

	public void SendEndRequest(ulong operationId, float total = 0f)
	{
		var msg = new MedicalOperationEndRequestMsg
		{
			OperationId = operationId,
			TotalMl = total,
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

	// ---- Host handlers ----

	public void HandleStartRequest(ulong sender, MedicalOperationStartRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!IsStage3Kind(msg.Kind))
		{
			return;
		}

		var target = msg.TargetSteamId;
		if (sender == target || sender == 0 || target == 0)
		{
			return;
		}

		if (!_access.IsInWorld(sender) || !_access.IsInWorld(target))
		{
			RejectStart(sender, target, msg, "Participants are not in-world.");
			return;
		}

		if (!_visibility.HasLineOfSight(sender, target))
		{
			RejectStart(sender, target, msg, "No line of sight.");
			return;
		}

		var operatorData = _access.GetCharacterData(sender);
		var targetData = _access.GetCharacterData(target);
		if (operatorData?.Health is not { } operatorHealth || !operatorHealth.Conscious || !operatorHealth.Alive)
		{
			RejectStart(sender, target, msg, "Operator is not conscious/alive.");
			return;
		}

		if (targetData?.Health is not { } targetHealth || !targetHealth.Alive)
		{
			RejectStart(sender, target, msg, "Target is not alive.");
			return;
		}

		if (_sessions.Values.Any(s => s.Operator == sender) || _hasActiveInjectionOrShrapnel(sender))
		{
			RejectStart(sender, target, msg, "Operator already has an active medical operation.");
			return;
		}

		if (!OtherMedicalOperationStartValidator.TryValidate(targetData, operatorData, msg, out var validationReason))
		{
			RejectStart(sender, target, msg, validationReason);
			return;
		}

		var itemIndex = msg.ItemInstanceId == 0 ? -1 : PlayerItemIndex.Find(operatorData, msg.ItemInstanceId);
		var originalItem = itemIndex >= 0 ? operatorData.Items[itemIndex] : null;

		if (msg.ItemInstanceId != 0)
		{
			if (itemIndex < 0)
			{
				RejectStart(sender, target, msg, "Item not found.");
				return;
			}

			if (originalItem!.Condition <= 0f)
			{
				RejectStart(sender, target, msg, "Item is empty.");
				return;
			}

			if (_sharedReservedItems.Contains(msg.ItemInstanceId))
			{
				RejectStart(sender, target, msg, "Item is already reserved.");
				return;
			}
		}

		if (msg.LimbIndex >= 0)
		{
			if (msg.LimbIndex >= targetData.Limbs.Count)
			{
				RejectStart(sender, target, msg, "Target limb not found.");
				return;
			}

			if (_sharedReservedTargetLimbs.Contains((target, msg.LimbIndex)))
			{
				RejectStart(sender, target, msg, "Target limb is already reserved.");
				return;
			}
		}

		var now = _time.NowMs;
		var newSession = new OtherMedicalOperationSession
		{
			OperationId = _operationIds.Next(),
			Operator = sender,
			Target = target,
			ItemInstanceId = msg.ItemInstanceId,
			ItemId = originalItem?.ItemId ?? "",
			LimbIndex = msg.LimbIndex,
			Kind = msg.Kind,
			StartedMs = now,
			LastUpdateMs = now,
		};

		_sessions.Add(newSession.OperationId, newSession);
		if (msg.ItemInstanceId != 0)
		{
			_sharedReservedItems.Add(msg.ItemInstanceId);
		}

		if (msg.LimbIndex >= 0)
		{
			_sharedReservedTargetLimbs.Add((target, msg.LimbIndex));
		}

		_log.LogInformation(
			"[MedicalOps3] started {Kind} operation {OperationId}: {Operator} -> {Target}, item {ItemId} (id {InstanceId}), limb {Limb}.",
			msg.Kind, newSession.OperationId, sender, target, originalItem?.ItemId ?? "-", msg.ItemInstanceId, msg.LimbIndex);

		SendStartAck(new MedicalOperationStartAckMsg
		{
			OperationId = newSession.OperationId,
			Accepted = true,
			OperatorSteamId = sender,
			TargetSteamId = target,
			ItemInstanceId = msg.ItemInstanceId,
			LimbIndex = msg.LimbIndex,
			Kind = msg.Kind,
		}, sender);
	}

	public void HandleUpdate(ulong sender, MedicalOperationUpdateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!TryGet(msg.OperationId, out var session) || session!.Operator != sender)
		{
			_log.LogWarning("[MedicalOps3] update refused for unknown/wrong operation {OperationId} from {Sender}.", msg.OperationId, sender);
			return;
		}

		var action = (MedicalOperationUpdateAction)msg.Action;
		if (float.IsNaN(msg.Value1) || float.IsInfinity(msg.Value1)
			|| (action == MedicalOperationUpdateAction.Cut && msg.Value1 <= 0f)
			|| (action == MedicalOperationUpdateAction.Shock && msg.Value1 < 0f))
		{
			_log.LogWarning("[MedicalOps3] update {OperationId} ignored non-finite/negative Stage 3 value {Value}.",
				msg.OperationId, msg.Value1);
			return;
		}

		session.LastUpdateMs = _time.NowMs;
		var state = default(MedicalOperationStateMsg?);

		var applied = session.Kind switch
		{
			MedicalOperationKind.Bandage when action == MedicalOperationUpdateAction.Wrap =>
				_applier.ApplyBandageWrap(session, out state),
			MedicalOperationKind.Dislocation when action == MedicalOperationUpdateAction.Hit =>
				_applier.ApplyDislocationHit(session, msg.Flag1, out state),
			MedicalOperationKind.Amputation when action == MedicalOperationUpdateAction.Cut =>
				_applier.ApplyAmputationCut(session, msg.Value1, out state),
			MedicalOperationKind.Aed when action == MedicalOperationUpdateAction.Stage || action == MedicalOperationUpdateAction.Shock =>
				_applier.ApplyAedStage(session, action, msg.Value1, out state),
			MedicalOperationKind.ManualDefib when action == MedicalOperationUpdateAction.Shock =>
				_applier.ApplyManualShock(session, msg.Value1, out state),
			_ => false,
		};

		if (applied)
		{
			session.Sequence++;
			if (state is not null)
			{
				PublishState(state);
			}
		}
	}

	public void HandleEndRequest(ulong sender, MedicalOperationEndRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!TryGet(msg.OperationId, out var session) || session!.Operator != sender)
		{
			return;
		}

		if (float.IsNaN(msg.TotalMl) || float.IsInfinity(msg.TotalMl) || msg.TotalMl < 0f)
		{
			_log.LogWarning("[MedicalOps3] end {OperationId} ignored non-finite/negative total {Total}.",
				msg.OperationId, msg.TotalMl);
			return;
		}

		if (session.Kind is MedicalOperationKind.SplintRemoval or MedicalOperationKind.TourniquetRemoval)
		{
			_removal.Remove(session);
		}

		if (session.Kind == MedicalOperationKind.Amputation && msg.TotalMl > session.Progress)
		{
			_applier.ApplyAmputationCut(session, msg.TotalMl - session.Progress, out _);
		}

		PrepareTerminal(session);
		var dislocateSucceeded = session.Kind == MedicalOperationKind.Dislocation && msg.TotalMl > 0.5f;
		_log.LogInformation("[MedicalOps3] operation {OperationId} ({Kind}) ended with progress {Progress:F3}.",
			session.OperationId, session.Kind, session.Progress);
		Terminate(session, MedicalOperationTerminalReason.Completed, dislocateSucceeded);
	}

	public void HandleCancelRequest(ulong sender, MedicalOperationCancelMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!TryGet(msg.OperationId, out var session) || session!.Operator != sender)
		{
			return;
		}

		PrepareTerminal(session);
		_log.LogInformation("[MedicalOps3] operation {OperationId} ({Kind}) cancelled with progress {Progress:F3}.",
			session.OperationId, session.Kind, session.Progress);
		Terminate(session, MedicalOperationTerminalReason.Cancelled);
	}

	// ---- Lifecycle ----

	public void Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var now = _time.NowMs;
		foreach (var session in _sessions.Values.ToList())
		{
			if (now - session.LastUpdateMs > OperationTimeoutMs)
			{
				_log.LogWarning("[MedicalOps3] operation {OperationId} timed out after {Idle} ms idle.",
					session.OperationId, now - session.LastUpdateMs);
				PrepareTerminal(session);
				Terminate(session, MedicalOperationTerminalReason.TimedOut);
			}
		}
	}

	public void OnMemberRemoved(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		foreach (var session in _sessions.Values.ToList())
		{
			if (session.Operator == steamId || session.Target == steamId)
			{
				PrepareTerminal(session);
				Terminate(session, MedicalOperationTerminalReason.Disconnected);
			}
		}
	}

	public void Clear()
	{
		foreach (var session in _sessions.Values)
		{
			ReleaseSession(session);
		}

		_sessions.Clear();
	}

	public bool HasActiveOtherOperator(ulong steamId) =>
		_sessions.Values.Any(s => s.Operator == steamId);

	public bool IsOtherOperation(ulong operationId) =>
		_sessions.Values.Any(s => s.OperationId == operationId);

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		Clear();
	}

	// ---- Internals ----

	private void PrepareTerminal(OtherMedicalOperationSession session)
	{
		if (session.Kind == MedicalOperationKind.ManualDefib)
		{
			_applier.DrainManualDefibTime(session, _time.NowMs);
		}
	}

	private void Terminate(OtherMedicalOperationSession session, MedicalOperationTerminalReason reason, bool dislocateSucceeded = false)
	{
		if (!_sessions.Remove(session.OperationId))
		{
			return;
		}

		ReleaseSession(session);
		var end = _applier.BuildTerminal(session, reason, dislocateSucceeded);
		_log.LogInformation("[MedicalOps3] operation {OperationId} terminal {Reason}: progress {Progress:F3}.",
			session.OperationId, reason, session.Progress);
		EndCommittedReceived?.Invoke(end);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationEndCommitted,
			end);
	}

	private void ReleaseSession(OtherMedicalOperationSession session)
	{
		if (session.ItemInstanceId != 0)
		{
			_sharedReservedItems.Remove(session.ItemInstanceId);
		}

		if (session.LimbIndex >= 0)
		{
			_sharedReservedTargetLimbs.Remove((session.Target, session.LimbIndex));
		}
	}

	private void PublishState(MedicalOperationStateMsg msg)
	{
		StateReceived?.Invoke(msg);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationState,
			msg);
	}

	private void SendStartAck(MedicalOperationStartAckMsg msg, ulong operatorId)
	{
		if (operatorId == _session.LocalSteamId)
		{
			StartAckReceived?.Invoke(msg);
		}
		else
		{
			_sender.Send(operatorId, NetMsg.MedicalOperationStartAck, msg);
		}
	}

	private void RejectStart(ulong operatorId, ulong target, MedicalOperationStartRequestMsg msg, string reason)
	{
		_log.LogWarning("[MedicalOps3] refused start for {Operator} on {Target} ({Kind}): {Reason}.",
			operatorId, target, msg.Kind, reason);
		SendStartAck(new MedicalOperationStartAckMsg
		{
			Accepted = false,
			RejectReason = reason,
			OperatorSteamId = operatorId,
			TargetSteamId = target,
			ItemInstanceId = msg.ItemInstanceId,
			LimbIndex = msg.LimbIndex,
			Kind = msg.Kind,
		}, operatorId);
	}

	private bool TryGet(ulong operationId, out OtherMedicalOperationSession? session) =>
		_sessions.TryGetValue(operationId, out session);

	private static bool IsStage3Kind(MedicalOperationKind kind) =>
		kind is MedicalOperationKind.Bandage
			or MedicalOperationKind.SplintRemoval
			or MedicalOperationKind.TourniquetRemoval
			or MedicalOperationKind.Dislocation
			or MedicalOperationKind.Aed
			or MedicalOperationKind.ManualDefib
			or MedicalOperationKind.Amputation;

}
