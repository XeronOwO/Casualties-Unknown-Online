using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Base class for packet handlers: the generic parameter T is the protobuf
/// message class, and <see cref="Process"/> decodes the frame payload into it —
/// subclasses override <see cref="Handle(ulong, T, HandlerContext)"/> with the
/// session logic. Handlers take no constructor dependencies — the control
/// surfaces arrive per message via <see cref="HandlerContext"/>, which keeps
/// the constructor graph acyclic (SessionService → gateway → router → handlers
/// would otherwise cycle back into the session).
/// </summary>
public abstract class PacketHandlerBase<TPacket> : IPacketHandler where TPacket : class
{
	public void Process(ulong sender, byte[] frame, HandlerContext ctx) =>
		Handle(sender, NetPacket.DecodePayload<TPacket>(frame), ctx);

	protected abstract void Handle(ulong sender, TPacket msg, HandlerContext ctx);
}
