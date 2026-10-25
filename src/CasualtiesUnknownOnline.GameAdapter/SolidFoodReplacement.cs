namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// What a solid-food action left IN PLACE OF the item it was handed: the definition the eater's client observed the
/// game's own delegate instantiate, and its condition.
///
/// <para>
/// It exists because the game's peel is a REPLACEMENT, not a consumption (<c>Item.cs:2606-2620</c>: the closed stone
/// fruit instantiates <c>stonefruitopen</c> at its own position and puts it back into the SAME slot). Only the client
/// that ran the delegate can see that object, and only the item's OWNER can show it, so the fact has to travel from one
/// to the other — the outcome report carries it, and the owner's own client turns its item into it in place.
/// </para>
/// <para>
/// <see cref="None"/> is the ordinary case: an eat that took a bite, or one whose action destroyed the item.
/// </para>
/// </summary>
internal readonly record struct SolidFoodReplacement(string ItemId, float Condition)
{
	/// <summary>The action did not replace the item.</summary>
	internal static readonly SolidFoodReplacement None = new(string.Empty, 0f);

	/// <summary>Whether the action replaced the item with another definition.</summary>
	internal bool IsReplacement => !string.IsNullOrEmpty(ItemId);
}
