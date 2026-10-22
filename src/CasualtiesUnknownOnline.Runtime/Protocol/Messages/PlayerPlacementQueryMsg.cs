using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → receiving client: resolve where this give would put the item, with YOUR rules, before any custody
/// moves. The query carries the same <see cref="CharacterItemMsg"/> the transfer would carry — the definition
/// and the properties the receiving rules read — plus the destination the giver's release named. Nothing on
/// either side has been written yet: this is the question the user's 2026-10-22 ruling requires before the
/// item can leave the giver.
/// </summary>
[ProtoContract]
public sealed class PlayerPlacementQueryMsg
{
	/// <summary>The host's correlation id; the answer echoes it.</summary>
	[ProtoMember(1)]
	public ulong QueryId { get; set; }

	/// <summary>The item as the host holds it: id, definition, slot and the state the receiving rules read.</summary>
	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	/// <summary>The body slot the giver's release named, or -1 when the release named a container.</summary>
	[ProtoMember(3)]
	public int RequestedSlot { get; set; } = -1;

	/// <summary>The container instance the giver's release named, or 0 when the release named a body slot.</summary>
	[ProtoMember(4)]
	public ulong RequestedContainerInstanceId { get; set; }
}
