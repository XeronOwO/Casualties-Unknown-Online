using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Recursive wire form of an item snapshot inside a player-interaction result.
/// Container contents travel with the parent so the receiving projection can
/// restore the exact local item tree.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteractionItem
{
	[ProtoMember(1)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(2)]
	public WireItemData Data { get; set; } = new();

	[ProtoMember(3)]
	public List<WirePlayerInteractionItem> Contents { get; set; } = [];
}
