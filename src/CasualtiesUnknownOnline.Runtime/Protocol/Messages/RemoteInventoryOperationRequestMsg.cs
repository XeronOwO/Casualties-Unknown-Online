using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for one host-authoritative remote-backpack inventory
/// operation (drop, move into a remote container, pour/dump). The host is the
/// cross-player authority: it validates the owner/item/container against its
/// authoritative character and kernel state, performs the durable mutation, and
/// records the participant-result event that makes the affected player's body
/// apply the exact authoritative local change. No display-proxy mutation is
/// requested or allowed.
/// </summary>
[ProtoContract]
public sealed class RemoteInventoryOperationRequestMsg
{
	[ProtoMember(1)]
	public RemoteInventoryOperationKind Kind { get; set; }

	/// <summary>The SteamId of the player whose carried inventory is being operated on.</summary>
	[ProtoMember(2)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The stable instance id of the item being dropped/moved/poured (0 = unbound — not operable).</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The stable instance id of the destination container for <see cref="RemoteInventoryOperationKind.MoveToContainer"/> (0 for other operations).</summary>
	[ProtoMember(4)]
	public ulong TargetContainerInstanceId { get; set; }
}
