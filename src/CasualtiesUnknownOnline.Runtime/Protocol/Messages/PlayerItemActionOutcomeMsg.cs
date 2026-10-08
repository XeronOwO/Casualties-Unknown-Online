using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Affected side → host outcome of a cross-player use whose effect the host cannot
/// compute: the item's own action ran on the client the effect lands on, and only
/// that client can say what the item became.
/// <para>
/// Two families report here, and both were admitted by the host first (the same
/// request flow every family uses): the admission is the grant this report is
/// matched against, one report per admitted use.
/// </para>
/// <list type="bullet">
/// <item>the SOLID-FOOD eat (<see cref="PlayerItemUseResultMsg.TargetEatsTheItem"/>) —
/// the eater's client runs <c>Body.UseItem</c> → <c>Stats.useAction</c> against its
/// own body and its own object of the owner's item, and reports the condition the
/// eat left;</item>
/// <item>the LIMB-TOOL use (<see cref="PlayerItemUseResultMsg.TargetRunsLimbAction"/>) —
/// the treated player's client runs the item's own <c>useLimbAction</c> against its
/// own limb and its own object of the owner's item, and reports the condition that
/// run left plus whether it consumed the object.</item>
/// </list>
/// <para>
/// What it carries is the item's post-use state, which the host commits into the
/// owner's authoritative record and hands to the owner through the ordinary use
/// result — the same report → arbitration → result path the item-report family
/// uses, with the item still owned by the player who offered it.
/// </para>
/// <para>
/// <see cref="Consumed"/> is the limb-tool half's own observation. The delegates
/// that turn an item into a limb component (<c>splint</c>, <c>carcasssplint</c>,
/// <c>tourniquet</c> — <c>Item.cs:400-408</c>, <c>1479-1491</c>, <c>1505-1517</c>)
/// destroy the object they were handed, and the applier that ran the delegate can
/// see that afterwards. The solid-food half leaves it false on purpose: its report
/// is issued from inside the game's own use call, before a delegate that destroys
/// the item could have destroyed it, so that family's consumption stays what it
/// always was — the verdict the host reads off the item's own data BEFORE admitting
/// the eat.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerItemActionOutcomeMsg
{
	/// <summary>The used item's stable instance id — the host matches it against the admitted use's grant.</summary>
	[ProtoMember(1)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>
	/// The item's condition after the action ran, read off the affected side's own
	/// copy. Zero when <see cref="Consumed"/> is set: the object is gone, and the
	/// condition it held at that moment is not readable afterwards — the limb
	/// component that took it carries that value instead.
	/// </summary>
	[ProtoMember(2)]
	public float Condition { get; set; }

	/// <summary>
	/// True when the run consumed the item object itself, so its owner no longer
	/// carries it. The limb-tool family observes this after running the delegate;
	/// the solid-food family reports false and leaves the decision to the host's
	/// own verdict.
	/// </summary>
	[ProtoMember(3)]
	public bool Consumed { get; set; }
}
