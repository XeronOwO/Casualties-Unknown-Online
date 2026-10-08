namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The solid-food chain's admission rule, in one place so the operator's gesture
/// verdict, the host's auto-select and the host's family dispatch cannot drift
/// apart: the item's own use action feeds the eating body
/// (<see cref="ISolidFoodSemantics"/>), which is the native call the whole chain
/// exists to run on the affected side.
/// <para>
/// Two shapes of that answer are separated here rather than at the call sites,
/// because they mean different things: <see cref="SolidFoodVerdict.EatsAndReplaces"/>
/// is a REFUSAL the host must name (the eater would be handed a replacement
/// object in their own world), while the other two are the family the one-shot
/// path carries. An item this rule refuses keeps the gesture it had before the
/// family existed — the release falls through to the native drop — so refusing
/// it here is not the same as refusing it on the host, and both sides ask THIS
/// rule.
/// </para>
/// </summary>
public static class SolidFoodAdmission
{
	/// <summary>The item's own use action, as its own shape.</summary>
	public static SolidFoodVerdict Classify(ISolidFoodSemantics semantics, string itemId) =>
		semantics.Classify(itemId);

	/// <summary>True when the cross-player path carries this item: the use action feeds the eating body and does not hand the eater a replacement object.</summary>
	public static bool IsFeedable(ISolidFoodSemantics semantics, string itemId) =>
		semantics.Classify(itemId) is SolidFoodVerdict.Eats or SolidFoodVerdict.EatsAndDestroys;
}
