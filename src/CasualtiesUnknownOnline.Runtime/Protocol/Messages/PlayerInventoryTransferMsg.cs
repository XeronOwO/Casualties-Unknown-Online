using CasualtiesUnknownOnline.GameState.Domains.Players;
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

	/// <summary>
	/// The destination rung the host chose for the created item. <see cref="PlayerInventoryDestination.BodySlot"/>
	/// is every pre-existing transfer (and the only one an older producer sets); a container rung also names
	/// <see cref="TargetParentItemId"/>; a drop carries no destination slot and no parent, and the receiving
	/// client materializes the item into its own world at its own position and reports the creation. The
	/// receiving client applies the named rung rather than choosing one — with one exception it cannot avoid:
	/// its LIVE container is the final judge of what it can hold, and a give whose live container refuses is
	/// answered by the drop rung there (the answer already named the container; the window is the limit the
	/// ticket records), never by a silent different slot.
	/// </summary>
	[ProtoMember(5)]
	public PlayerInventoryDestination Destination { get; set; }

	/// <summary>
	/// True when the destination is the ANSWERED one: the receiving client resolved it before the host wrote the
	/// row (the give's placement answer), so this client must NOT substitute another slot or container when its
	/// live body cannot take that exact destination. It reports the failure instead and the host returns the row
	/// to the giver; a substitution here would be the drift the answer exists to remove, and the host's row would
	/// state a destination this client never used.
	/// </summary>
	[ProtoMember(6)]
	public bool ExactDestination { get; set; }
}
