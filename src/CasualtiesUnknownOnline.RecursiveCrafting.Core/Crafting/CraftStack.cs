namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// One stack the player currently holds, as the resolver reads it: the id, an optional quality, how many units
/// the stack is, and the condition each of those units still carries. A stack whose condition is spent is still
/// a unit and can still be consumed for a line that only needs its remaining condition.
/// </summary>
public sealed class CraftStack(string itemId, int amount, float condition, string? quality = null)
{
	/// <summary>The game's item id of this stack.</summary>
	public string ItemId { get; } = itemId;

	/// <summary>How many units the stack holds.</summary>
	public int Amount { get; } = amount;

	/// <summary>The condition each unit in the stack carries (1 = fresh, 0 = spent).</summary>
	public float Condition { get; } = condition;

	/// <summary>The quality the stack carries, or null for none.</summary>
	public string? Quality { get; } = quality;
}
