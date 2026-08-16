using System;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The pure rules of the Heater cooker conversion (Heater.cs:41-49) — the
/// candidate predicate and the created-steak fingerprint. The patch is the
/// thin adapter; this class is the L0-testable decision surface. The game's
/// own predicate is <c>cooker &amp;&amp; Stats.HasTag("meat") &amp;&amp; id != "steak"</c>
/// and the created steak's condition is <c>item.condition * 0.3f</c>.
/// </summary>
internal static class HeaterCookRule
{
	/// <summary>The game's cooked item definition (Heater.cs:44-46).</summary>
	internal const string CookedItemId = "steak";

	/// <summary>The game's condition multiplier (Heater.cs:46).</summary>
	internal const float CookedConditionMultiplier = 0.3f;

	/// <summary>How far the created steak may sit from the captured raw-item position before the patch refuses to claim it (same-frame spawn at the exact position).</summary>
	internal const float SpawnMatchDistance = 0.5f;

	/// <summary>The condition comparison tolerance — the value is assigned in the same callback before decay can advance.</summary>
	internal const float ConditionTolerance = 0.001f;

	/// <summary>The game's cook predicate (Heater.cs:44).</summary>
	internal static bool IsCookCandidate(bool cooker, bool hasMeatTag, string itemId) =>
		cooker && hasMeatTag && itemId != CookedItemId;

	/// <summary>The condition the native conversion writes onto the created steak (Heater.cs:46).</summary>
	internal static float CookedCondition(float sourceCondition) => sourceCondition * CookedConditionMultiplier;

	/// <summary>The created steak is the one whose condition is the native conversion's exact product.</summary>
	internal static bool IsCookedCondition(float cookedCondition, float sourceCondition) =>
		Math.Abs(cookedCondition - CookedCondition(sourceCondition)) <= ConditionTolerance;

	/// <summary>The created steak spawns at the raw item's transform position in the same physics callback (Heater.cs:46) — squared distance, no sqrt on the hot path.</summary>
	internal static bool IsCookedSpawnAt(float x, float y, float sourceX, float sourceY)
	{
		var dx = x - sourceX;
		var dy = y - sourceY;
		return (dx * dx) + (dy * dy) <= SpawnMatchDistance * SpawnMatchDistance;
	}
}
