using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The single transport entry point for the Phase C four-envelope protocol.
/// Directions are bidirectional because the same frame id carries commands
/// up and batches/checkpoints down; the service branches on the local role.
/// </summary>
[PacketHandler(NetMsg.KernelEnvelope, NetMessageDirection.Bidirectional)]
public sealed class KernelEnvelopeHandler(NetworkTrafficMonitor traffic) : PacketHandlerBase<ProtocolFrame, IKernelProtocolContext>
{
	private readonly NetworkTrafficMonitor _traffic = traffic;

	protected override void Handle(ulong sender, ProtocolFrame msg, IKernelProtocolContext ctx)
	{
		if (ProtocolFrameTrafficClassifier.TryGetPayloadType(msg) is { } payloadType && CurrentFrameLength > 0)
		{
			_traffic.RecordReceivePayload(sender, payloadType, CurrentFrameLength);
		}

		ctx.KernelProtocol.HandleFrame(sender, msg);
	}
}
