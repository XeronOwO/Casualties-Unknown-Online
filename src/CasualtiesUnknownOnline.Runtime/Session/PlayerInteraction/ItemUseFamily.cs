namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The routing family ONE entry's gesture resolves to, in the order the host's
/// chain dispatches them (wear, injection, topical, drink, solid food, limb tool).
/// It answers "which native action does this gesture run", not "what kind of item
/// is this": one item can satisfy several families and
/// <see cref="ItemUseEntry"/> says which of its actions was made.
/// </summary>
public enum ItemUseFamily
{
	/// <summary>No family this entry carries; the host refuses it by name and nothing may be measured.</summary>
	None,

	/// <summary>
	/// The wear placement (<c>Body.WearWearable</c>): the host places the item on the
	/// target's body, and nothing is measured on the operator's side — a wear gesture
	/// has no dose and no native action of its own to capture.
	/// </summary>
	Wear,

	/// <summary>
	/// The injection chain's business. This one-shot path refuses it BY NAME on both
	/// entries and never dispatches its branch: a blood bag's <c>useAction</c> draws
	/// blood (<c>Item.DrawBlood</c>), so an injectable container's own action must not
	/// be run by a measurement. The refusal is keyed on the STACKS (an injectable
	/// liquid present), not on the definition — a container refilled with a
	/// non-injectable liquid is the drink family's by its own <c>usable</c> flag, and
	/// the drink measurement classifies the DEFINITION before running it, so a
	/// non-drink action is refused unrun (see
	/// <c>docs/backlog/review/drink-measurement-destructive-use-action.md</c>).
	/// </summary>
	Injection,

	/// <summary>
	/// A dose the item's own limb action applies to the wound view's selected limb
	/// (<c>WaterContainerItem.ApplyToLimb</c>). The wound view's release is its native
	/// call site, so only that entry may answer it.
	/// </summary>
	Topical,

	/// <summary>
	/// A drink: the item's own use action reaches <c>WaterContainerItem.Drink</c>, and
	/// the committed dose travels to the affected side, which runs each liquid's own
	/// <c>onDrink</c>.
	/// </summary>
	Drink,

	/// <summary>
	/// The eat: the item's own use action feeds a body and hands it nothing the
	/// affected side would have to materialize. The whole action runs on the affected
	/// side, and nothing is measured or committed on the host.
	/// </summary>
	SolidFood,

	/// <summary>
	/// The item's own limb action with no liquid container. The affected side runs it
	/// against its own limb, so the wound view's release is the entry that carries it.
	/// </summary>
	LimbTool,
}
