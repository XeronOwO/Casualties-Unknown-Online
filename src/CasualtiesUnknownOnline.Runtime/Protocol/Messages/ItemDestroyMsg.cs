using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A world item was destroyed (decay to zero, consumed by use): guest → host
/// as a report (the host drops it from the world-item table and relays), host
/// → guest as a broadcast relay (the source excluded).
/// </summary>
[ProtoContract]
public sealed class ItemDestroyMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }
}
