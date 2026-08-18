using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A nested container-content change inside a CARRIED container: guest → host
/// report of the parent container's full fact after a body-internal move (an
/// item shifted between a backpack's slot, a held container, a limb pouch…).
/// The host records the parent fact and broadcasts it through the existing
/// carried-fact event (ItemCarriedSync) so the peers' clones re-render the
/// container's new contents immediately — one operation = one message, never a
/// decomposed per-content report.
/// </summary>
[ProtoContract]
public sealed class ItemContainerContentMsg
{
	/// <summary>The container item's instance id (the transfer-table/character-snapshot primary key).</summary>
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	/// <summary>The parent container's full wire fact, including its recursive Contents.</summary>
	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();
}
