namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What an item's own use action does, as far as the cross-player path has to
/// care — answered by the Game Adapter from the game's own content
/// (<see cref="ISolidFoodSemantics"/>), never by a CUO id table.
/// <para>
/// The family this describes is the one native shape the migrated chains could
/// not copy: a solid food's <c>useAction</c> is a delegate that writes the
/// eating body AND the item it is handed, so the affected side cannot run it
/// without an item instance — which is why the object comes first
/// (<c>mod-cross-player-solid-food-semantics</c> steps 1 and 2) and the eat is
/// the third.
/// </para>
/// <para>
/// The two extra shapes are not flavours of eating: they are the two things a
/// food delegate can do to the OBJECT, and each one decides whether the
/// cross-player path may run it at all. Both are read from the delegate's own
/// compiled body by the adapter, because no <c>ItemInfo</c> field carries them.
/// </para>
/// </summary>
public enum SolidFoodVerdict
{
	/// <summary>Not the solid-food family: the use action does not feed a body.</summary>
	NotSolidFood,

	/// <summary>The use action feeds the eating body and leaves the item to its own condition cost.</summary>
	Eats,

	/// <summary>
	/// The use action feeds the eating body AND destroys the item object itself
	/// (the vanilla <c>exposedcore</c>), so the item is gone whatever its
	/// condition says. The owner's real item is removed with it.
	/// </summary>
	EatsAndDestroys,

	/// <summary>
	/// The use action feeds the eating body AND hands the eater a replacement
	/// object (the vanilla <c>bucketofchicken</c> and <c>popcorn</c> at their
	/// last bite). The cross-player path CARRIES it: the hand-feed arrangement makes the replacement reach the OWNER's
	/// slot — the eating client parents the item into a hand slot, watches the object the delegate instantiates and reports
	/// its definition back, so the owner's own item becomes it in place.
	/// </summary>
	EatsAndReplaces,

	/// <summary>
	/// The use action runs against the item and REPLACES it, without feeding a body — the vanilla closed stone fruit
	/// (<c>stonefruitclosed</c>): it costs stamina, drops the hand item and instantiates <c>stonefruitopen</c>
	/// (<c>Item.cs:2606-2621</c>). No <c>Body.Eat</c>/<c>Body.Drink</c> call appears in its delegate, so the feeding test alone
	/// classified it as "not a solid food" and the cross-player gesture refused it. The path carries it exactly like
	/// <see cref="EatsAndReplaces"/>: the item's own action runs on the affected side and the replacement travels back through
	/// the outcome report (user ruling 2026-10-26: "run the item's own action for any usable item").
	/// </summary>
	Replaces,
}
