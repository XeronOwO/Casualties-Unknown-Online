using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → owner instruction for one native remote-backpack inventory operation.
/// The host has already validated the requester, owner, line-of-sight and the
/// referenced authoritative items; this message tells the owner's own client to
/// execute the exact native body/item operation. The owner's existing item sync
/// paths (use, slot, craft, character snapshot) then report the authoritative
/// result back to the host and to every peer's clone, so the remote display
/// proxies are never mutated here.
/// </summary>
[ProtoContract]
public sealed class RemoteInventoryApplyMsg
{
	[ProtoMember(1)]
	public RemoteInventoryOperationKind Kind { get; set; }

	/// <summary>The player who actually owns the inventory being operated on; this message is delivered only to that SteamId.</summary>
	[ProtoMember(2)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The primary item instance id (the item the viewer dragged).</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The second item instance id for two-item operations (combine, battery load/unload); 0 when not used.</summary>
	[ProtoMember(4)]
	public ulong TargetItemInstanceId { get; set; }

	/// <summary>The destination body-slot index for <see cref="RemoteInventoryOperationKind.MoveToSlot"/>; -1 when not used.</summary>
	[ProtoMember(5)]
	public int TargetSlotIndex { get; set; } = -1;
}
