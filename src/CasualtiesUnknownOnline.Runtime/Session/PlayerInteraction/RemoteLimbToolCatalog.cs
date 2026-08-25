using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of non-liquid limb tools for the cross-player
/// item-use slice. It maps known tool item ids to a pure
/// <see cref="RemoteLimbToolProfile"/>. Unknown tools are refused as a whole so
/// an unsupported timed/component effect is never silently approximated.
/// </summary>
public static class RemoteLimbToolCatalog
{
	private static readonly IReadOnlyDictionary<string, RemoteLimbToolProfile> Registry =
		new Dictionary<string, RemoteLimbToolProfile>(System.StringComparer.Ordinal)
		{
			// Full-use effects from Item.cs SetupItems:
			// boneweldingtool (693-705), clottingmush (1582-1591),
			// chestdrain (1605-1615), musharm (614-625).
			["boneweldingtool"] = new("boneweldingtool",
				ConditionCost: 0.5f,
				SkinHealth: -25f,
				MuscleHealth: -26f,
				Pain: 30f,
				BleedAmount: 5f,
				BoneHealTimerMultiplier: 0.25f,
				BloodViscosity: 2f),
			["clottingmush"] = new("clottingmush",
				ConditionCost: 0.34f,
				BleedAmountMultiplier: 0.6f,
				BloodViscosity: 10f),
			["chestdrain"] = new("chestdrain",
				ConditionCost: 1f,
				RequiredLimbIndex: 1,
				BleedAmount: 2f,
				Hemothorax: -35f),
			["musharm"] = new("musharm",
				ConditionCost: 1f,
				SkinHealAmount: 8f,
				BandageSlowAmount: 10f),
		};

	public static bool IsToolItem(string itemId) => Registry.ContainsKey(itemId);

	public static bool TryGet(string itemId, out RemoteLimbToolProfile profile) =>
		Registry.TryGetValue(itemId, out profile!);
}
