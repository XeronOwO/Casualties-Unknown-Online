namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// One message-type handler in the session protocol. Implementations extend
/// <see cref="PacketHandlerBase{TPacket, TContext}"/>, which provides the
/// default protobuf decode for the packet type (T) and hands only the narrow
/// handler context the implementation declared — a handler only implements
/// <see cref="PacketHandlerBase{TPacket, TContext}.Handle"/>.
/// </summary>
public interface IPacketHandler
{
	/// <summary>Processes one received frame (already direction-validated by the
	/// gateway) with the control surfaces it needs.</summary>
	void Process(ulong sender, byte[] frame, HandlerContext ctx);
}
