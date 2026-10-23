using System.Linq;

namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// The wording of a resolution, in one place so the console command and the future panel state cannot drift:
/// "craftable now", "reachable through N steps" with the steps in run order, or "cannot be crafted" with the
/// line that stopped it named. It is game-free on purpose — the console command, a panel or a log line all read
/// the same sentence.
/// </summary>
public static class CraftChainReport
{
	/// <summary>One sentence (or two) for a resolution.</summary>
	public static string Describe(string recipeName, CraftResolution resolution)
	{
		switch (resolution.Reachability)
		{
			case CraftReachability.Now:
				return $"{recipeName}: craftable now.";

			case CraftReachability.ThroughSteps:
				var steps = string.Join(", ", resolution.Steps.Select((step, index) =>
					step.Times > 1 ? $"{index + 1}. {step.RecipeId} x{step.Times}" : $"{index + 1}. {step.RecipeId}"));
				return $"{recipeName}: reachable through {resolution.Steps.Count} step(s) — {steps}.";

			default:
				return $"{recipeName}: cannot be crafted — nothing here provides \"{resolution.Blocker}\".";
		}
	}
}
