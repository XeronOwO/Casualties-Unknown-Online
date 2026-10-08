using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Eater → host outcome of a cross-player SOLID-FOOD use. The solid-food family
/// is the one the host cannot compute: the item's own <c>useAction</c> is a
/// delegate that writes the eating body and the item it is handed, so the eat
/// runs on the AFFECTED side's client against its own body and its own object of
/// the owner's item — and only that client can say what the item became.
/// <para>
/// The report is not a permission: the host admitted the use first (the same
/// request flow every family uses) and that admission is the grant this message
/// is matched against, one report per admitted eat. What it carries is the
/// item's post-eat state, which the host commits into the owner's authoritative
/// record and hands to the owner through the ordinary use result — the same
/// report → arbitration → result path the item-report family uses, with the item
/// still owned by the player who offered it.
/// </para>
/// <para>
/// Only the CONDITION travels: it is the one item member every solid-food
/// delegate writes (<c>item.condition -= X</c>). The delegates that consume or
/// replace the item object are known from the item's own data before the eat
/// (<see cref="Session.PlayerInteraction.SolidFoodVerdict"/>), so the host does
/// not have to infer them from a report the eater could not make (the object is
/// already gone on that side by the time it could).
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerItemEatOutcomeMsg
{
	/// <summary>The eaten item's stable instance id — the host matches it against the admitted eat's grant.</summary>
	[ProtoMember(1)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The eaten item's condition after the use action ran, read off the eater's own copy.</summary>
	[ProtoMember(2)]
	public float Condition { get; set; }
}
