using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The single transport entry point for the Phase C four-envelope protocol.
/// Directions are bidirectional because the same frame id carries commands
/// up and batches/checkpoints down; the service branches on the local role.
/// </summary>
[PacketHandler(NetMsg.KernelEnvelope, NetMessageDirection.Bidirectional)]
public sealed class KernelEnvelopeHandler : PacketHandlerBase<ProtocolFrame, IKernelProtocolContext>
{
	protected override void Handle(ulong sender, ProtocolFrame msg, IKernelProtocolContext ctx) =>
		ctx.KernelProtocol.HandleFrame(sender, msg);
}
