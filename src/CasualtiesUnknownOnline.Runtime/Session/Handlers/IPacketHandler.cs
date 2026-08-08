namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// One message-type handler in the session protocol. Implementations extend
/// <see cref="PacketHandlerBase{TPacket}"/>, which provides the default
/// protobuf decode for the packet type (T) — a handler only implements
/// <see cref="PacketHandlerBase{TPacket}.Handle"/>.
/// </summary>
public interface IPacketHandler
{
	/// <summary>Processes one received frame (already direction-validated by the
	/// gateway) with the control surfaces it needs.</summary>
	void Process(ulong sender, byte[] frame, HandlerContext ctx);
}
