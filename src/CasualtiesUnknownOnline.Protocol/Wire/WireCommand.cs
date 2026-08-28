using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Typed wire command payload. One command is one logical operation; the
/// kernel maps this to a typed GameState command on the host.
/// </summary>
[ProtoContract]
public sealed class WireCommand
{
	[ProtoMember(1)]
	public WireCommandKind Kind { get; set; }

	[ProtoMember(2)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(3)]
	public WireItemLocation? Location { get; set; }

	[ProtoMember(4)]
	public WireItemData? Data { get; set; }

	[ProtoMember(5)]
	public ulong ExpectedRevision { get; set; }

	[ProtoMember(6)]
	public ulong NewOwner { get; set; }

	[ProtoMember(7)]
	public WireTerminalKind TerminalKind { get; set; }

	[ProtoMember(8)]
	public ulong RangeStart { get; set; }

	[ProtoMember(9)]
	public ulong RangeEnd { get; set; }

	[ProtoMember(10)]
	public List<WireContainerChild> ContainerChildren { get; set; } = [];

	[ProtoMember(11)]
	public int RejectionReason { get; set; }
}
