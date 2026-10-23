using System.Collections.Generic;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// The resolver's answer: the third state for the recipe panel, the steps that make the chain work in the order
/// they run, and — for a chain that cannot resolve — the ingredient that stopped it, so the UI can say why
/// instead of just "cannot be crafted".
/// </summary>
public sealed class CraftResolution
{
	private CraftResolution(CraftReachability reachability, IReadOnlyList<CraftStep> steps, string? blocker)
	{
		Reachability = reachability;
		Steps = steps;
		Blocker = blocker;
	}

	/// <summary>Which of the three states the entry is in.</summary>
	public CraftReachability Reachability { get; }

	/// <summary>The steps a completed chain runs, in order (empty when the recipe is craftable now).</summary>
	public IReadOnlyList<CraftStep> Steps { get; }

	/// <summary>For an unreachable chain: the ingredient id that could not be produced, or null otherwise.</summary>
	public string? Blocker { get; }

	internal static CraftResolution Now() => new(CraftReachability.Now, [], null);

	internal static CraftResolution ThroughSteps(IReadOnlyList<CraftStep> steps) => new(CraftReachability.ThroughSteps, steps, null);

	internal static CraftResolution Unreachable(string? blocker) => new(CraftReachability.Unreachable, [], blocker);
}
