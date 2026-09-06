using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Wire publishing for shared shrapnel sessions. It computes the
/// recipient-specific <see cref="MedicalOperationStateMsg"/> from the
/// authoritative state writer and sends it to every handshaken remote member.
/// The caller supplies the local event callback; this class owns no mutable
/// session state.
/// </summary>
internal sealed class ShrapnelStatePublisher(
	ISessionControl session,
	PacketSender sender,
	ShrapnelSessionStateWriter writer)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ShrapnelSessionStateWriter _writer = writer;

	internal void Publish(ShrapnelOperationSession shrapnel, Action<MedicalOperationStateMsg>? fireState)
	{
		fireState?.Invoke(_writer.BuildState(shrapnel, _session.LocalSteamId));
		foreach (var member in _session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId))
		{
			_sender.Send(
				member.SteamId,
				NetMsg.MedicalOperationState,
				_writer.BuildState(shrapnel, member.SteamId));
		}
	}
}
