using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Wire publishing for the medical operation session domain: start acks,
/// non-terminal State broadcasts and terminal EndCommitted broadcasts. It owns
/// no session state; the host service keeps the registry and calls it after an
/// authoritative decision.
/// </summary>
internal sealed class MedicalOperationSessionPublisher(
	ISessionControl session,
	PacketSender sender,
	Action<MedicalOperationStartAckMsg> fireStartAck,
	Action<MedicalOperationStateMsg> fireState,
	Action<MedicalOperationEndCommittedMsg> fireEnd)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly Action<MedicalOperationStartAckMsg> _fireStartAck = fireStartAck;
	private readonly Action<MedicalOperationStateMsg> _fireState = fireState;
	private readonly Action<MedicalOperationEndCommittedMsg> _fireEnd = fireEnd;

	internal void SendStartAck(MedicalOperationStartAckMsg msg, ulong operatorId)
	{
		if (operatorId == _session.LocalSteamId)
		{
			_fireStartAck(msg);
		}
		else
		{
			_sender.Send(operatorId, NetMsg.MedicalOperationStartAck, msg);
		}
	}

	internal void RejectStart(
		ulong operatorId,
		ulong target,
		MedicalOperationStartRequestMsg msg,
		string reason)
	{
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

	internal void PublishState(MedicalOperationStateMsg msg)
	{
		_fireState(msg);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationState,
			msg);
	}

	internal void PublishEnd(MedicalOperationEndCommittedMsg msg)
	{
		_fireEnd(msg);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationEndCommitted,
			msg);
	}
}
