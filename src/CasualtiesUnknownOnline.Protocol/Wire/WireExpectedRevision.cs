using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one aggregate revision precondition.
/// </summary>
[ProtoContract]
public sealed class WireExpectedRevision
{
	[ProtoMember(1)]
	public ulong AggregateId { get; set; }

	[ProtoMember(2)]
	public ulong Revision { get; set; }
}
