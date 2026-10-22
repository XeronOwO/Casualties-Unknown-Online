using CasualtiesUnknownOnline.GameState.Domains.Players;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → participant(s) authoritative result of a cross-player inventory
/// transfer. One operation = one message: the item's full captured fact leaves
/// FromSteamId's body and enters ToSteamId's body. For a give the destination is the one the RECEIVING client
/// answered (<see cref="ExactDestination"/>); every other transfer names the host's own choice. The receiving Game
/// Adapter applies that destination inside a RemoteApply scope, refuses to substitute when the flag is set, and
/// immediately re-reports the character snapshot so every clone learns the real place within the same run.
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
	/// The destination rung of this transfer. <see cref="PlayerInventoryDestination.BodySlot"/> is the ordinary
	/// slot rung (for a give: the slot the receiver answered); a container rung also names
	/// <see cref="TargetParentItemId"/> and is the receiver's own answered container; a drop carries no slot and no
	/// parent, and the receiving client materializes the item into its own world at its own position and reports
	/// the creation. The receiving client applies the named rung rather than choosing one: an exact give whose
	/// live destination no longer takes the item is refused by the receiver, not silently re-slotted.
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

	/// <summary>
	/// True when this transfer is the RETURN of a give whose answered destination the receiver could not take
	/// (the design's residual). The receiving client is the original giver: any slot on its body is acceptable,
	/// and when its live body accepts none the item falls at its feet instead of being destroyed — the user's
	/// 2026-10-22 ruling is that the item ends with the giver, never that a full body consumes it.
	/// </summary>
	[ProtoMember(7)]
	public bool Returned { get; set; }
}
