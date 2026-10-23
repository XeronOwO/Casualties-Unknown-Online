using System.Collections.Generic;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// One stack the player currently holds, as the resolver reads it: the id, how many units the stack is, the
/// condition each of those units still carries, and the qualities (the game's crafting tags) it declares. A
/// stack whose condition is spent is still a unit and can still be consumed for a line that only needs its
/// remaining condition.
/// </summary>
public sealed class CraftStack(string itemId, int amount, float condition, params string[] qualities)
{
	/// <summary>The game's item id of this stack.</summary>
	public string ItemId { get; } = itemId;

	/// <summary>How many units the stack holds.</summary>
	public int Amount { get; } = amount;

	/// <summary>The condition each unit in the stack carries (1 = fresh, 0 = spent).</summary>
	public float Condition { get; } = condition;

	/// <summary>The crafting qualities (tags) the stack declares; the game's items carry several.</summary>
	public IReadOnlyList<string> Qualities { get; } = qualities ?? [];
}
