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
/// generic medical session envelope (Start → updates → one EndCommitted) and the
/// shared item/operator claims, and specializes the update payload for
/// bandage, splint/tourniquet removal, dislocation, AED, manual defibrillation
/// and amputation. Several operators may work one victim — and one limb — at the
/// same time: the unit of work is (target, limb, kind) plus, where the minigame has
/// discrete pieces, the piece itself, and a unit whose outcome resolves once is
/// settled by its first completion while the other operations still open on it are
/// answered and stopped (<see cref="MedicalOperationUnitRules"/>).
/// </summary>
internal sealed class OtherMedicalOperationSessionService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess access,
	IItemControl items,
	IPlayerInteractionVisibility visibility,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority,
	MedicalOperationClaims claims,
	MedicalOperationIdAllocator operationIds,
	MedicalTargetBodyGate bodyGate,
	ILogger log)
{
	private const int OperationTimeoutMs = 20000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = access;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ITimeSource _time = time;
	private readonly MedicalOperationClaims _claims = claims;
	private readonly MedicalTargetBodyGate _bodyGate = bodyGate;
	private readonly MedicalOperationIdAllocator _operationIds = operationIds;
	private readonly ILogger _log = log;
	private readonly OtherMedicalOperationApplier _applier = new(access, items, kernelAuthority, session);
	private readonly OtherMedicalRemovalApplier _removal = new(access, items, kernelAuthority, session, log);

	private readonly Dictionary<ulong, OtherMedicalOperationSession> _sessions = [];
	private OtherMedicalOperationSettlement? _settlement;
	private bool _disposed;

	/// <summary>The one owner of a session's end; built lazily because a field initializer cannot reference the siblings.</summary>
	private OtherMedicalOperationSettlement Settlement =>
		_settlement ??= new(_session, _sender, _claims, _time, _applier, _removal, _log);

	/// <summary>The cut policy's read-only probe (WorldTransientPolicy.OtherMedicalSessionKey): open Stage-3 sessions.</summary>
	internal int PendingCutSessions => _sessions.Count;

	public event Action<MedicalOperationStartAckMsg>? StartAckReceived;
	public event Action<MedicalOperationStateMsg>? StateReceived;

	/// <summary>The settled session's one terminal — forwarded from the settlement, which owns the end.</summary>
	public event Action<MedicalOperationEndCommittedMsg>? EndCommittedReceived
	{
		add => Settlement.EndCommitted += value;
		remove => Settlement.EndCommitted -= value;
	}

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

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, targetSteamId))
		{
			_log.LogInformation("[MedicalOps3] refused locally: {Operator} cannot see {Target} on this client.", _session.LocalSteamId, targetSteamId);
			RejectStart(_session.LocalSteamId, targetSteamId, msg, "No line of sight.");
			return;
		}

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

		var target = msg.TargetSteamId;
		if (sender == target || sender == 0 || target == 0)
		{
			return;
		}

		if (!IsStage3Kind(msg.Kind))
		{
			// Answered, not dropped. This family is the facade's fallback for every kind that is neither
			// an injection nor a fragment pull, so an unnamed or unknown kind lands HERE — and it used to
			// return in silence: no log, no ack, and an operator waiting for one. That silence is also why
			// the None sentinel alone could not make such a message audible (found in review, 2026-10-11).
			RejectStart(sender, target, msg, "Unsupported operation.");
			return;
		}

		if (!_access.IsInWorld(sender) || !_access.IsInWorld(target))
		{
			RejectStart(sender, target, msg, "Participants are not in-world.");
			return;
		}

		var operatorData = _access.GetCharacterData(sender);
		if (operatorData?.Health is not { } operatorHealth || !operatorHealth.Conscious || !operatorHealth.Alive)
		{
			RejectStart(sender, target, msg, "Operator is not conscious/alive.");
			return;
		}

		if (_claims.IsOperatorBusy(sender))
		{
			RejectStart(sender, target, msg, "Operator already has an active medical operation.");
			return;
		}

		if (!OtherMedicalOperationStartValidator.TryValidate(operatorData, msg, out var itemReason))
		{
			RejectStart(sender, target, msg, itemReason);
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

			if (_claims.IsItemReserved(msg.ItemInstanceId))
			{
				RejectStart(sender, target, msg, "Item is already reserved.");
				return;
			}
		}

		// Everything the host owns is settled; only the target's own body is still open,
		// so the request is parked and the target's client answers it.
		_bodyGate.Begin(sender, target, msg.LimbIndex, msg.Kind, verdict =>
		{
			if (!verdict.Accepted)
			{
				RejectStart(sender, target, msg, verdict.Reason);
				return;
			}

			CommitStart(sender, target, msg, originalItem);
		});
	}

	/// <summary>
	/// Runs on the target's accept. The answer took a round trip, so the host's own
	/// facts are re-checked before the session opens: a departure or another
	/// operation's claim that landed in that window must not be overridden by a
	/// verdict that predates it.
	/// </summary>
	private void CommitStart(ulong sender, ulong target, MedicalOperationStartRequestMsg msg, CharacterItemMsg? originalItem)
	{
		switch (MedicalStartRecheck.Run(_access, _claims, sender, target, msg, out var rejectReason))
		{
			case MedicalStartRecheckOutcome.OperatorGone:
				_log.LogInformation("[MedicalOps3] start for {Operator} dropped: the operator left while the target answered.", sender);
				return;
			case MedicalStartRecheckOutcome.Reject:
				RejectStart(sender, target, msg, rejectReason);
				return;
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
		_claims.TryReserveOperator(newSession.Operator);
		if (msg.ItemInstanceId != 0)
		{
			_claims.TryReserveItem(msg.ItemInstanceId);
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
			MedicalOperationKind.Cpr when action == MedicalOperationUpdateAction.Compress =>
				_applier.ApplyCprCompression(session, msg.Value1, out state),
			MedicalOperationKind.Cpr when action == MedicalOperationUpdateAction.Ventilate =>
				_applier.ApplyCprVentilation(session, msg.Value1, out state),
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

		if (session.Kind == MedicalOperationKind.Amputation && msg.TotalMl > session.Progress)
		{
			_applier.ApplyAmputationCut(session, msg.TotalMl - session.Progress, out _);
		}

		Settlement.PrepareTerminal(session);
		var dislocateSucceeded = session.Kind == MedicalOperationKind.Dislocation && msg.TotalMl > 0.5f;
		_log.LogInformation("[MedicalOps3] operation {OperationId} ({Kind}) ended with progress {Progress:F3}.",
			session.OperationId, session.Kind, session.Progress);
		Settlement.Terminate(_sessions, session, MedicalOperationTerminalReason.Completed, dislocateSucceeded);
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

		Settlement.PrepareTerminal(session);
		_log.LogInformation("[MedicalOps3] operation {OperationId} ({Kind}) cancelled with progress {Progress:F3}.",
			session.OperationId, session.Kind, session.Progress);
		Settlement.Terminate(_sessions, session, MedicalOperationTerminalReason.Cancelled);
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
				Settlement.PrepareTerminal(session);
				Settlement.Terminate(_sessions, session, MedicalOperationTerminalReason.TimedOut);
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
				Settlement.PrepareTerminal(session);
				Settlement.Terminate(_sessions, session, MedicalOperationTerminalReason.Disconnected);
			}
		}
	}

	public void Clear()
	{
		foreach (var session in _sessions.Values)
		{
			Settlement.ReleaseSession(session);
		}

		_sessions.Clear();
	}

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
			or MedicalOperationKind.Amputation
			or MedicalOperationKind.Cpr;

}
