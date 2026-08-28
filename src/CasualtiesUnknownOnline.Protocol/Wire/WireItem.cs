using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one authoritative kernel item fact, used by checkpoints.
/// </summary>
[ProtoContract]
public sealed class WireItem
{
	[ProtoMember(1)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(2)]
	public ulong Revision { get; set; }

	[ProtoMember(3)]
	public WireItemLocation Location { get; set; } = new();

	[ProtoMember(4)]
	public WireItemData Data { get; set; } = new();
}
