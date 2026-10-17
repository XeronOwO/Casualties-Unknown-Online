using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → participant(s) authoritative result of a cross-player inventory
/// transfer. One operation = one message: the item's full captured fact leaves
/// FromSteamId's body and enters ToSteamId's body. The receiving Game Adapter
/// is responsible for choosing the local target slot (the host does not know
/// the peer's live slot layout), applying the mutation inside a RemoteApply
/// scope and immediately re-reporting the character snapshot so every clone
/// learns the real slot within the same run.
/// </summary>
[ProtoContract]
public sealed class PlayerInventoryTransferMsg
{
	/// <summary>The player whose carried item is being removed.</summary>
	[ProtoMember(1)]
	public ulong FromSteamId { get; set; }

	/// <summary>The player whose body receives the item.</summary>
	[ProtoMember(2)]
	public ulong ToSteamId { get; set; }

	/// <summary>The full authoritative item fact (instance ids + state + contents).</summary>
	[ProtoMember(3)]
	public CharacterItemMsg? Item { get; set; }

	/// <summary>
	/// The authoritative destination container's instance id. 0 = place directly into an inventory slot.
	/// On a cross-player transfer it names a container the RECEIVER carries (a handed-over item aimed at an
	/// item shown in the receiver's inventory), and the receiving client's own container rules decide whether it
	/// fits; on a same-owner move it names the mover's own container.
	/// </summary>
	[ProtoMember(4)]
	public ulong TargetParentItemId { get; set; }
}
