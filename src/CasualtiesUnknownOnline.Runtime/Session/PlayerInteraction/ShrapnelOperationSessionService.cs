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
/// Host-authoritative shared shrapnel session domain. One session is created
/// per target limb when the first operator starts the native shrapnel
/// minigame; later operators join the same session. The host owns per-piece
/// positions and short lease ownership, rejects non-owner movement, releases
/// leases on release/cancel/disconnect, commits removed pieces into the
/// authoritative limb snapshot and emits one terminal EndCommitted only when
/// the whole shared session ends.
/// </summary>
internal sealed class ShrapnelOperationSessionService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess access,
	IItemControl items,
	IPlayerInteractionVisibility visibility,
	ITimeSource time,
	ItemKernelAuthority kernelAuthority,
	HashSet<ulong> sharedReservedItems,
	Func<ulong, bool> hasActiveInjection,
	ILogger log)
{
	private const int MaxPieces = 5;
	private const float RemoveThresholdY = 35f;
	private const long LeaseMs = 150;
	private const int IdleTimeoutMs = 15000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = access;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ITimeSource _time = time;
	private readonly HashSet<ulong> _sharedReservedItems = sharedReservedItems;
	private readonly Func<ulong, bool> _hasActiveInjection = hasActiveInjection;
	private readonly ILogger _log = log;

	private readonly ShrapnelSessionStateWriter _writer = new(access, items, kernelAuthority, session, log);
	private readonly Dictionary<(ulong Target, int Limb), ShrapnelOperationSession> _sessions = [];
	private ulong _nextOperationId = 1;
	private bool _disposed;

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

		var msg = new MedicalOperationUpdateMsg
		{
			OperationId = operationId,
			PieceIndex = update.PieceIndex,
			X = update.X,
			Y = update.Y,
			Grabbed = update.Grabbed,
			Released = update.Released,
			BreakGrasp = update.BreakGrasp,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUpdate(_session.LocalSteamId, msg);
		}
		else
		{
			var semantic = update.Grabbed || update.Released || update.BreakGrasp;
			_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, msg, reliable: semantic);
		}
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

		if (!_visibility.HasLineOfSight(sender, target))
		{
			RejectStart(sender, target, msg, "No line of sight.");
			return;
		}

		var userData = _access.GetCharacterData(sender);
		var targetData = _access.GetCharacterData(target);
		if (userData?.Health is not { } userHealth || !userHealth.Conscious || !userHealth.Alive)
		{
			RejectStart(sender, target, msg, "Operator is not conscious/alive.");
			return;
		}

		if (targetData?.Health is not { } targetHealth || !targetHealth.Conscious || !targetHealth.Alive)
		{
			RejectStart(sender, target, msg, "Target is not conscious/alive.");
			return;
		}

		if (msg.LimbIndex < 0 || msg.LimbIndex >= targetData.Limbs.Count)
		{
			RejectStart(sender, target, msg, "Target limb not found.");
			return;
		}

		var targetLimb = targetData.Limbs[msg.LimbIndex];
		if (targetLimb.Dismembered || targetLimb.Shrapnel <= 0)
		{
			RejectStart(sender, target, msg, "Target limb has no shrapnel.");
			return;
		}

		if (_hasActiveInjection(sender) || HasActiveShrapnelOperator(sender))
		{
			RejectStart(sender, target, msg, "Operator already has an active medical operation.");
			return;
		}

		var itemInstanceId = msg.ItemInstanceId;
		if (itemInstanceId != 0)
		{
			var itemIndex = FindUseItemIndex(userData, itemInstanceId);
			if (itemIndex < 0 || itemIndex >= userData.Items.Count)
			{
				RejectStart(sender, target, msg, "Tweezers not found.");
				return;
			}

			var item = userData.Items[itemIndex];
			if (item.ItemId != "tweezers" || item.Condition <= 0f)
			{
				RejectStart(sender, target, msg, "Item is not usable tweezers.");
				return;
			}

			if (_sharedReservedItems.Contains(itemInstanceId))
			{
				RejectStart(sender, target, msg, "Item is already reserved.");
				return;
			}
		}

		var key = (target, msg.LimbIndex);
		if (!_sessions.TryGetValue(key, out var shrapnel))
		{
			shrapnel = new ShrapnelOperationSession
			{
				OperationId = _nextOperationId++,
				Target = target,
				LimbIndex = msg.LimbIndex,
				LastUpdateMs = _time.NowMs,
			};
			_writer.InitializePieces(shrapnel, targetLimb.Shrapnel);
			_sessions.Add(key, shrapnel);
			_log.LogInformation("[Shrapnel] session {OperationId} created for {Target} limb {Limb} ({Count} pieces).",
				shrapnel.OperationId, target, msg.LimbIndex, targetLimb.Shrapnel);
		}

		JoinOperator(shrapnel, sender, itemInstanceId, userData);

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

		if (!TryGet(msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
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
		var owner = piece.Owner == sender || piece.Owner == 0 || piece.LeaseExpiryMs <= now;
		if (!owner)
		{
			_log.LogInformation("[Shrapnel] session {OperationId} rejected non-owner move on piece {Piece} from {Sender} (owner {Owner}).",
				shrapnel.OperationId, msg.PieceIndex, sender, piece.Owner);
			return;
		}

		if (msg.BreakGrasp)
		{
			_writer.ApplyBreakGrasp(shrapnel);
			piece.Owner = 0;
			piece.LeaseExpiryMs = 0;
		}
		else
		{
			piece.X = ClampX(msg.X);
			piece.Y = ClampY(msg.Y);
			if (msg.Grabbed)
			{
				piece.Owner = sender;
				piece.LeaseExpiryMs = now + LeaseMs;
			}
			else if (msg.Released)
			{
				piece.Owner = 0;
				piece.LeaseExpiryMs = 0;
			}
			else if (piece.Owner != sender)
			{
				piece.Owner = sender;
				piece.LeaseExpiryMs = now + LeaseMs;
			}

			if (piece.Y >= RemoveThresholdY)
			{
				piece.Removed = true;
				piece.Owner = 0;
				piece.LeaseExpiryMs = 0;
				_log.LogInformation("[Shrapnel] session {OperationId} piece {Piece} removed by {Sender}.", shrapnel.OperationId, msg.PieceIndex, sender);
			}
		}

		shrapnel.LastUpdateMs = now;
		shrapnel.Sequence++;
		_writer.UpdateAuthoritativeLimb(shrapnel);

		if (AllRemoved(shrapnel))
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

		if (!TryGet(msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
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

		if (!TryGet(msg.OperationId, out var shrapnel) || !shrapnel!.Operators.Contains(sender))
		{
			return;
		}

		LeaveOperator(shrapnel, sender, MedicalOperationTerminalReason.Cancelled);
	}

	// ---- Lifecycle ----

	public void Update()
	{
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
			ReleaseItems(shrapnel);
		}

		_sessions.Clear();
	}

	public bool HasActiveShrapnelOperator(ulong steamId) =>
		_sessions.Values.Any(s => s.Operators.Contains(steamId));

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

	private void JoinOperator(ShrapnelOperationSession shrapnel, ulong operatorId, ulong itemInstanceId, CharacterDataMsg userData)
	{
		shrapnel.Operators.Add(operatorId);
		if (itemInstanceId != 0)
		{
			_sharedReservedItems.Add(itemInstanceId);
			shrapnel.OperatorItems[operatorId] = itemInstanceId;
			_writer.DrainTweezers(operatorId, itemInstanceId, userData);
		}

		shrapnel.LastUpdateMs = _time.NowMs;
		_log.LogInformation("[Shrapnel] operator {Operator} joined session {OperationId}.", operatorId, shrapnel.OperationId);
	}

	private void LeaveOperator(ShrapnelOperationSession shrapnel, ulong operatorId, MedicalOperationTerminalReason reason)
	{
		ReleaseOperatorPieces(shrapnel, operatorId);
		ReleaseOperatorItem(shrapnel, operatorId);
		shrapnel.Operators.Remove(operatorId);
		shrapnel.LastUpdateMs = _time.NowMs;
		_log.LogInformation("[Shrapnel] operator {Operator} left session {OperationId} ({Reason}).", operatorId, shrapnel.OperationId, reason);

		if (AllRemoved(shrapnel))
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

	private void ReleaseOperatorPieces(ShrapnelOperationSession shrapnel, ulong operatorId)
	{
		foreach (var piece in shrapnel.Pieces.Values)
		{
			if (piece.Owner == operatorId)
			{
				piece.Owner = 0;
				piece.LeaseExpiryMs = 0;
			}
		}
	}

	private void ReleaseOperatorItem(ShrapnelOperationSession shrapnel, ulong operatorId)
	{
		if (shrapnel.OperatorItems.TryGetValue(operatorId, out var itemId))
		{
			shrapnel.OperatorItems.Remove(operatorId);
			_sharedReservedItems.Remove(itemId);
		}
	}

	private void ReleaseItems(ShrapnelOperationSession shrapnel)
	{
		foreach (var itemId in shrapnel.OperatorItems.Values)
		{
			_sharedReservedItems.Remove(itemId);
		}

		shrapnel.OperatorItems.Clear();
	}

	private bool AllRemoved(ShrapnelOperationSession shrapnel) =>
		shrapnel.Pieces.Values.All(p => p.Removed);

	private void PublishState(ShrapnelOperationSession shrapnel)
	{
		var state = _writer.BuildState(shrapnel);
		StateReceived?.Invoke(state);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationState,
			state);
	}

	private void Terminate(ShrapnelOperationSession shrapnel, MedicalOperationTerminalReason reason)
	{
		var key = (shrapnel.Target, shrapnel.LimbIndex);
		if (!_sessions.Remove(key))
		{
			return;
		}

		ReleaseItems(shrapnel);
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

	private bool TryGet(ulong operationId, out ShrapnelOperationSession? shrapnel)
	{
		foreach (var session in _sessions.Values)
		{
			if (session.OperationId == operationId)
			{
				shrapnel = session;
				return true;
			}
		}

		shrapnel = null;
		return false;
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

	private static float ClampX(float x) => Math.Max(-524f, Math.Min(524f, x));

	private static float ClampY(float y) => Math.Max(-364f, Math.Min(524f, y));
}
