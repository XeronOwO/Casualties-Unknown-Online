using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Base class for packet handlers: the generic parameter T is the protobuf
/// message class, and <see cref="Process"/> decodes the frame payload into it —
/// subclasses override <see cref="Handle(ulong, T)"/> with the session logic.
/// </summary>
public abstract class PacketHandlerBase<TPacket>(SessionService session) : IPacketHandler where TPacket : class
{
	protected SessionService Session { get; } = session;

	public void Process(ulong sender, byte[] frame) => Handle(sender, NetPacket.DecodePayload<TPacket>(frame));

	protected abstract void Handle(ulong sender, TPacket msg);
}
