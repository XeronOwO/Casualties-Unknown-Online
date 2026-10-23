namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>What a recipe's entry is, from the resolver's point of view — the third UI state the ticket asks for.</summary>
public enum CraftReachability
{
	/// <summary>Every ingredient is in the inventory right now.</summary>
	Now,

	/// <summary>Missing ingredients are themselves craftable, so the recipe is reachable through steps.</summary>
	ThroughSteps,

	/// <summary>At least one ingredient cannot be produced or found.</summary>
	Unreachable,
}
