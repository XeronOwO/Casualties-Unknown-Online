namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game's own content facts the cross-player CONSUME chain judges with,
/// instead of a CUO id/liquid table. The Runtime holds the QUESTION and every
/// call site; the Game Adapter answers it from the game's registry
/// (<c>Item.GlobalItems</c>), because no game assembly may cross into the
/// Runtime.
/// <para>
/// The question is the native dispatch's own gate, not a copy of it: an item is
/// drunk when its <c>ItemInfo</c> is a <c>LiquidItemInfo</c> whose
/// <c>usable</c> flag is set, because that flag is exactly what
/// <c>Body.UseItem</c> tests before it runs the item's <c>useAction</c> — and a
/// liquid container's own use action is the one that reaches
/// <c>WaterContainerItem.Drink</c>. The sibling limb-use chains ask their flag
/// on the same class (<c>usableOnLimb</c>, <see cref="ILimbUseSemantics"/>), so
/// the two families are separated by the item's own data rather than by an id
/// list.
/// </para>
/// </summary>
public interface IConsumeSemantics
{
	/// <summary>
	/// The item's own data says the game may USE it (its <c>ItemInfo.Stats</c> is
	/// a <c>LiquidItemInfo</c> with <c>usable</c> set), which is the gate
	/// <c>Body.UseItem</c> applies before running the item's <c>useAction</c>.
	/// False for an unknown id, for a solid item, and for the liquid containers
	/// the game only ever draws from a limb (a syringe's <c>usable</c> is false).
	/// </summary>
	bool IsUsableLiquidContainer(string itemId);
}
