using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One wire chunk of a kernel checkpoint. The receiver restores all chunks,
/// then applies the committed tail from <c>BaseGlobalRevision + 1</c>.
/// </summary>
[ProtoContract]
public sealed class WireCheckpoint
{
	[ProtoMember(1)]
	public int ChunkIndex { get; set; }

	[ProtoMember(2)]
	public int ChunkCount { get; set; }

	[ProtoMember(3)]
	public ulong RunEpoch { get; set; }

	[ProtoMember(4)]
	public ulong GlobalRevision { get; set; }

	[ProtoMember(5)]
	public List<WireItem> Items { get; set; } = [];

	[ProtoMember(6)]
	public List<WireRandomStream> RandomStreams { get; set; } = [];
}
