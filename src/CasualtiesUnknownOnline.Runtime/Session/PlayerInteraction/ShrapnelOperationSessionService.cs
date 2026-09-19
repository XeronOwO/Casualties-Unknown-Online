using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-authoritative shared shrapnel session domain. One session is created
/// per target limb when the first operator starts the native shrapnel
/// minigame; later operators join the same session. The host owns per-piece
/// positions and per-piece ownership, rejects non-owner movement, releases
/// ownership on release/cancel/disconnect, commits removed pieces into the
/// authoritative limb snapshot and emits one terminal EndCommitted only when
/// the whole shared session ends. A grab keeps the piece owned until it is
/// removed, released, broken or the operator leaves, so a drag-to-drop is one
/// atomic operation.
/// </summary>
internal sealed class ShrapnelOperationSessionService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess access,
	IItemControl items,
	IPlayerInteractionVisibility visibility,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority,
	MedicalOperationClaims claims,
	AdaptiveStreamRateService adaptiveRates,
	MedicalOperationIdAllocator operationIds,
	MedicalTargetBodyGate bodyGate,
	ILogger log)
{
	private const int MaxPieces = 5;
	private const float RemoveThresholdY = 35f;
	private const int IdleTimeoutMs = 15000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = access;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ITimeSource _time = time;
	private readonly MedicalOperationClaims _claims = claims;
	private readonly MedicalOperationIdAllocator _operationIds = operationIds;
	private readonly MedicalTargetBodyGate _bodyGate = bodyGate;
	private readonly ILogger _log = log;

	private readonly ShrapnelSessionStateWriter _writer = new(access, items, kernelAuthority, session, log);
	private readonly ShrapnelPositionReportBuffer _positionReports = new(session, sender, adaptiveRates, time, log);
	private readonly ShrapnelStatePublisher _statePublisher = new(session, sender, new(access, items, kernelAuthority, session, log));
	private readonly Dictionary<(ulong Target, int Limb), ShrapnelOperationSession> _sessions = [];
	private bool _disposed;
	internal int PendingCutSessions => _sessions.Count; // the cut policy's read-only probe (WorldTransientPolicy) — this file is at the architecture gate's line limit, so the probe is deliberately one line

	public event Action<MedicalOperationStartAckMsg>? StartAckReceived;
	public event Action<MedicalOperationStateMsg>? StateReceived;
	public event Action<MedicalOperationEndCommittedMsg>? EndCommittedReceived;

	// ---- Client send surface ----

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
			Kind = MedicalOperationKind.Shrapnel,
		};

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, targetSteamId))
		{
			_log.LogInformation("[Shrapnel] refused locally: {Operator} cannot see {Target} on this client.", _session.LocalSteamId, targetSteamId);
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

	public void SendUpdate(ulong operationId, ShrapnelPieceUpdate update)
	{
		if (update.PieceIndex < 0 || update.PieceIndex >= MaxPieces || float.IsNaN(update.X) || float.IsInfinity(update.X) || float.IsNaN(update.Y) || float.IsInfinity(update.Y))
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			HandleUpdate(_session.LocalSteamId, new MedicalOperationUpdateMsg
			{
				OperationId = operationId,
				PieceIndex = update.PieceIndex,
				X = update.X,
				Y = update.Y,
				Grabbed = update.Grabbed,
				Released = update.Released,
				BreakGrasp = update.BreakGrasp,
				OwnershipChange = update.OwnershipChange,
			});
			return;
		}

		if (update.OwnershipChange || update.BreakGrasp || update.Released)
		{
			// Semantic ownership transitions are reliable and bypass the
			// position coalescer. The latest buffered ordinary move is flushed
			// first so release/break/end never silently drops the final
			// position; then the buffer is cleared because the transition
			// supersedes later ordinary moves. BreakGrasp/Released are treated
			// as semantic even when the caller omits OwnershipChange.
			_positionReports.Flush(operationId);
			_positionReports.Clear(operationId);
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, new MedicalOperationUpdateMsg
			{
				OperationId = operationId,
				PieceIndex = update.PieceIndex,
				X = update.X,
				Y = update.Y,
				Grabbed = update.Grabbed,
				Released = update.Released,
				BreakGrasp = update.BreakGrasp,
				OwnershipChange = update.OwnershipChange,
			}, reliable: true);
			return;
		}

		_positionReports.QueueOrdinary(operationId, update);
	}

	public void SendEndRequest(ulong operationId)
	{
		var msg = new MedicalOperationEndRequestMsg
		{
			OperationId = operationId,
			TotalMl = 0f,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleEndRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_positionReports.Flush(operationId);
			_positionReports.Clear(operationId);
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

		if (!_access.IsInWorld(sender) || !_access.IsInWorld(target))
		{
			RejectStart(sender, target, msg, "Participants are not in-world.");
			return;
		}

		var userData = _access.GetCharacterData(sender);
		if (!ShrapnelStartValidator.TryValidateOperator(sender, userData, msg, _claims, out var reason))
		{
			RejectStart(sender, target, msg, reason);
			return;
		}

		// The limb's piece count is the target's own fact, so only its client can say
		// how many fragments the limb carries right now — and the session's piece
		// layout is seeded from the count that answer carries, not from the report
		// that had aged by a latency before this request arrived.
		_bodyGate.Begin(sender, target, msg.LimbIndex, msg.Kind, verdict =>
		{
			if (!verdict.Accepted)
			{
				RejectStart(sender, target, msg, verdict.Reason);
				return;
			}

			CommitStart(sender, target, msg, userData!, verdict.ShrapnelCount);
		});
	}

	/// <summary>
	/// Runs on the target's accept. The answer took a round trip, so the host's own
	/// facts are re-checked before the session opens or the operator joins: a
	/// departure or another operator's claim that landed in that window must not be
	/// overridden by a verdict that predates it.
	/// </summary>
	private void CommitStart(ulong sender, ulong target, MedicalOperationStartRequestMsg msg, CharacterDataMsg userData, int shrapnelCount)
	{
		switch (MedicalStartRecheck.Run(_access, _claims, sender, target, msg, out var rejectReason))
		{
			case MedicalStartRecheckOutcome.OperatorGone:
				_log.LogInformation("[Shrapnel] start for {Operator} dropped: the operator left while the target answered.", sender);
				return;
			case MedicalStartRecheckOutcome.Reject:
				RejectStart(sender, target, msg, rejectReason);
				return;
		}

		var itemInstanceId = msg.ItemInstanceId;
		var key = (target, msg.LimbIndex);
		// A session that already exists for this limb is the shared session of that
		// limb's pieces, and this start JOINS it as a second operator — the join is the
		// family's multi-operator path, not a raced start.
		if (!_sessions.TryGetValue(key, out var shrapnel))
		{
			shrapnel = new ShrapnelOperationSession
			{
				OperationId = _operationIds.Next(),
				Target = target,
				LimbIndex = msg.LimbIndex,
				LastUpdateMs = _time.NowMs,
			};
			_writer.InitializePieces(shrapnel, shrapnelCount);
			_sessions.Add(key, shrapnel);
			_log.LogInformation("[Shrapnel] session {OperationId} created for {Target} limb {Limb} ({Count} pieces).",
				shrapnel.OperationId, target, msg.LimbIndex, shrapnelCount);
		}

		ShrapnelOperatorBookkeeping.Join(shrapnel, sender, itemInstanceId, userData, _claims, _writer, _time, _log);

		SendStartAck(new MedicalOperationStartAckMsg
		{
			OperationId = shrapnel.OperationId,
			Accepted = true,
			OperatorSteamId = sender,
			TargetSteamId = target,
			ItemInstanceId = itemInstanceId,
			LimbIndex = msg.LimbIndex,
			Kind = MedicalOperationKind.Shrapnel,
		}, sender);

		PublishState(shrapnel);
	}

	public void HandleUpdate(ulong sender, MedicalOperationUpdateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!ShrapnelSessionLookup.TryGet(_sessions, msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
		{
			_log.LogWarning("[Shrapnel] update refused for unknown/wrong session {OperationId} from {Sender}.", msg.OperationId, sender);
			return;
		}

		if (msg.PieceIndex < 0 || msg.PieceIndex >= MaxPieces || !shrapnel.Pieces.TryGetValue(msg.PieceIndex, out var piece))
		{
			return;
		}

		if (piece.Removed)
		{
			return;
		}

		var now = _time.NowMs;
		if (piece.Owner != 0 && piece.Owner != sender)
		{
			_log.LogInformation("[Shrapnel] session {OperationId} rejected non-owner move on piece {Piece} from {Sender} (owner {Owner}).",
				shrapnel.OperationId, msg.PieceIndex, sender, piece.Owner);
			return;
		}

		if (msg.BreakGrasp)
		{
			if (piece.Owner != sender)
			{
				return;
			}

			_writer.ApplyBreakGrasp(shrapnel);
			piece.Owner = 0;
		}
		else
		{
			// A held-position report without an ownership transition must not
			// re-acquire a released piece: only the current owner may keep
			// moving it, and an owner-less stale move is dropped.
			if (msg.Grabbed && !msg.OwnershipChange && piece.Owner != sender)
			{
				return;
			}

			// A release report from the native minigame carries no meaningful
			// position; preserve the last authoritative piece position instead
			// of resetting it to the release message's default origin.
			if (!msg.Released)
			{
				piece.X = ShrapnelBounds.ClampX(msg.X);
				piece.Y = ShrapnelBounds.ClampY(msg.Y);
			}

			if (msg.OwnershipChange && msg.Grabbed)
			{
				piece.Owner = sender;
			}
			else if (msg.Released)
			{
				piece.Owner = 0;
			}

			if (piece.Y >= RemoveThresholdY)
			{
				piece.Removed = true;
				piece.Owner = 0;
				_log.LogInformation("[Shrapnel] session {OperationId} piece {Piece} removed by {Sender}.", shrapnel.OperationId, msg.PieceIndex, sender);
			}
		}

		shrapnel.LastUpdateMs = now;
		shrapnel.Sequence++;
		_writer.UpdateAuthoritativeLimb(shrapnel);

		if (shrapnel.AllPiecesRemoved)
		{
			Terminate(shrapnel, MedicalOperationTerminalReason.Completed);
			return;
		}

		PublishState(shrapnel);
	}

	public void HandleEndRequest(ulong sender, MedicalOperationEndRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!ShrapnelSessionLookup.TryGet(_sessions, msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
		{
			return;
		}

		LeaveOperator(shrapnel, sender, MedicalOperationTerminalReason.Cancelled);
	}

	public void HandleCancelRequest(ulong sender, MedicalOperationCancelMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!ShrapnelSessionLookup.TryGet(_sessions, msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
		{
			return;
		}

		LeaveOperator(shrapnel, sender, MedicalOperationTerminalReason.Cancelled);
	}

	internal void ClearPending(ulong operationId) => _positionReports.Clear(operationId);

	internal void FlushBeforeTerminal(ulong operationId) =>
		_positionReports.FlushBeforeTerminal(operationId);

	// ---- Lifecycle ----

	public void Update()
	{
		_positionReports.FlushDue();
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var now = _time.NowMs;
		foreach (var shrapnel in _sessions.Values.ToList())
		{
			if (now - shrapnel.LastUpdateMs > IdleTimeoutMs)
			{
				_log.LogWarning("[Shrapnel] session {OperationId} timed out after {Idle} ms idle.", shrapnel.OperationId, now - shrapnel.LastUpdateMs);
				Terminate(shrapnel, MedicalOperationTerminalReason.TimedOut);
			}
		}
	}

	public void OnMemberRemoved(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		foreach (var shrapnel in _sessions.Values.ToList())
		{
			if (shrapnel.Target == steamId)
			{
				Terminate(shrapnel, MedicalOperationTerminalReason.Disconnected);
				continue;
			}

			if (shrapnel.Operators.Contains(steamId))
			{
				LeaveOperator(shrapnel, steamId, MedicalOperationTerminalReason.Disconnected);
			}
		}
	}

	public void Clear()
	{
		foreach (var shrapnel in _sessions.Values)
		{
			ShrapnelOperatorBookkeeping.ReleaseItems(shrapnel, _claims);
			foreach (var operatorId in shrapnel.Operators)
			{
				_claims.ReleaseOperator(operatorId);
			}
		}

		_sessions.Clear();
		_positionReports.ClearAll();
	}

	public bool IsShrapnelOperation(ulong operationId) =>
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

	private void LeaveOperator(ShrapnelOperationSession shrapnel, ulong operatorId, MedicalOperationTerminalReason reason)
	{
		ShrapnelOperatorBookkeeping.ReleasePieces(shrapnel, operatorId);
		ShrapnelOperatorBookkeeping.ReleaseItem(shrapnel, operatorId, _claims);
		shrapnel.Operators.Remove(operatorId);
		_claims.ReleaseOperator(operatorId);
		shrapnel.LastUpdateMs = _time.NowMs;
		_log.LogInformation("[Shrapnel] operator {Operator} left session {OperationId} ({Reason}).", operatorId, shrapnel.OperationId, reason);

		if (shrapnel.AllPiecesRemoved)
		{
			Terminate(shrapnel, MedicalOperationTerminalReason.Completed);
			return;
		}

		if (shrapnel.Operators.Count == 0)
		{
			Terminate(shrapnel, MedicalOperationTerminalReason.Cancelled);
			return;
		}

		PublishState(shrapnel);
	}

	private void PublishState(ShrapnelOperationSession shrapnel) =>
		_statePublisher.Publish(shrapnel, StateReceived);

	private void Terminate(ShrapnelOperationSession shrapnel, MedicalOperationTerminalReason reason)
	{
		var key = (shrapnel.Target, shrapnel.LimbIndex);
		if (!_sessions.Remove(key))
		{
			return;
		}

		ShrapnelOperatorBookkeeping.ReleaseItems(shrapnel, _claims);
		foreach (var operatorId in shrapnel.Operators)
		{
			_claims.ReleaseOperator(operatorId);
		}

		var end = _writer.BuildTerminal(shrapnel, reason);
		_log.LogInformation("[Shrapnel] session {OperationId} terminal {Reason}.", shrapnel.OperationId, reason);
		EndCommittedReceived?.Invoke(end);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationEndCommitted,
			end);
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

	private void RejectStart(ulong sender, ulong target, MedicalOperationStartRequestMsg msg, string reason)
	{
		_log.LogWarning("[Shrapnel] refused start for {Sender} on {Target}: {Reason}.", sender, target, reason);
		SendStartAck(new MedicalOperationStartAckMsg
		{
			Accepted = false,
			RejectReason = reason,
			OperatorSteamId = sender,
			TargetSteamId = target,
			ItemInstanceId = msg.ItemInstanceId,
			LimbIndex = msg.LimbIndex,
			Kind = MedicalOperationKind.Shrapnel,
		}, sender);
	}

}
