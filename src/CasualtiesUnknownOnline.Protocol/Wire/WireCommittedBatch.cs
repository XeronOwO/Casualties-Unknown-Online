using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one atomic committed kernel batch.
/// </summary>
[ProtoContract]
public sealed class WireCommittedBatch
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }

	[ProtoMember(2)]
	public ulong GlobalRevision { get; set; }

	[ProtoMember(3)]
	public ulong Actor { get; set; }

	[ProtoMember(4)]
	public int Authority { get; set; }

	[ProtoMember(5)]
	public ulong RunEpoch { get; set; }

	[ProtoMember(6)]
	public List<WireExpectedRevision> Preconditions { get; set; } = [];

	[ProtoMember(7)]
	public List<WireEvent> Events { get; set; } = [];
}
