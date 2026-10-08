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
	/// last bite). The cross-player path refuses this shape by name: the
	/// replacement would be created on the EATER's machine — a phantom object in
	/// that world, plus the game's own "too far" alert when it tries to put it in
	/// the eater's hand — and the eater is not the item's owner, so there is
	/// nothing to hand it to.
	/// </summary>
	EatsAndReplaces,
}
