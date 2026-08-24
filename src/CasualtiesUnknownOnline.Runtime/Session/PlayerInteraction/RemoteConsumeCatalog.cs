using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of consumables that may be used on another
/// player in the first cross-player item-use slice: known drinkable liquids
/// (water/food/juice/energy containers) and solid food items. It is a read-only
/// presence registry — the Online UI uses it only to decide which held items are
/// worth exposing, never as a source of truth. Unknown liquids/items are
/// deliberately refused by the host so an unsupported effect is never silently
/// approximated.
/// </summary>
public static class RemoteConsumeCatalog
{
	public const float DrinkAmountMl = 100f;

	private static readonly IReadOnlyDictionary<string, RemoteFoodEffect> Food =
		new Dictionary<string, RemoteFoodEffect>(System.StringComparer.Ordinal)
		{
			["bread"] = new("bread", 0.34f, Hunger: 9f, Thirst: 2f, WeightOffset: 0.5f),
			["cake"] = new("cake", 0.10f, Hunger: 8f, WeightOffset: 1.25f, Happiness: 0.8f),
			["banana"] = new("banana", 0.50f, Hunger: 9f, Thirst: 4f, WeightOffset: 0.1f, Happiness: 1f, RadiationSickness: 1f),
			["foliagemeal"] = new("foliagemeal", 0.50f, Hunger: 18f, Thirst: 2f, Sickness: 3f),
			["burger"] = new("burger", 0.334f, Hunger: 14f, WeightOffset: 1.2f, Happiness: 1.5f),
			["pancake"] = new("pancake", 0.251f, Hunger: 10f, WeightOffset: 1.2f, Happiness: 1.25f, Sickness: 1.5f),
			["pizzaslice"] = new("pizzaslice", 0.334f, Hunger: 12f, WeightOffset: 1.2f, Happiness: 1.5f),
			["steak"] = new("steak", 0.334f, Hunger: 15f, WeightOffset: 1.6f, Happiness: 2f),
			["pemmican"] = new("pemmican", 0.25f, Hunger: 12f, WeightOffset: 1.35f),
			["cookies"] = new("cookies", 0.10f, Hunger: 3f, WeightOffset: 0.7f, Sickness: 3f, Happiness: 0.85f),
			["chips"] = new("chips", 0.10f, Hunger: 3f, WeightOffset: 0.9f, Sickness: 3f, Happiness: 0.7f),
			["cereal"] = new("cereal", 0.20f, Hunger: 6f, Thirst: -2.5f, WeightOffset: 0.45f),
			["dogfood"] = new("dogfood", 0.20f, Hunger: 6f, WeightOffset: 0.4f),
			["hardcandy"] = new("hardcandy", 0.20f, Hunger: 1f, WeightOffset: 0.7f, Sickness: 3f, Happiness: 1f),
			["fleshchunk"] = new("fleshchunk", 0.20f, Hunger: 14f, WeightOffset: 1.2f, Happiness: -0.1f),
			["candybar"] = new("candybar", 1f, Hunger: 6f, WeightOffset: 0.8f, Happiness: 2.5f, Sickness: 5f),
			["chocolatebar"] = new("chocolatebar", 0.34f, Hunger: 7f, WeightOffset: 0.8f, Happiness: 2.5f, Sickness: 20f),
			["paprikash"] = new("paprikash", 0.50f, Hunger: 12f, WeightOffset: 0.5f, Happiness: 1f, Sickness: 2f),
			["nutrientbar"] = new("nutrientbar", 0.34f, Hunger: 12.5f, WeightOffset: 0.4f),
			["stonefruitopen"] = new("stonefruitopen", 1f, Hunger: 8f, Thirst: -5f, WeightOffset: 0.3f),
			["experimentflesh"] = new("experimentflesh", 1f, Hunger: 12.5f, WeightOffset: 1.5f, Happiness: -6f, Sickness: 16f),
			["animalflesh"] = new("animalflesh", 1f, Hunger: 7.5f, WeightOffset: 1f, Happiness: -0.75f, Sickness: 4f),
			["internalorgans"] = new("internalorgans", 0.34f, Hunger: 15f, WeightOffset: 3f, Happiness: -18f, Sickness: 32f),
			["blobflesh"] = new("blobflesh", 1f, Hunger: 6f, WeightOffset: 0.6f, Happiness: 1f, Sickness: 5f),
			["xalorissponge"] = new("xalorissponge", 1f, Hunger: 8f, WeightOffset: 0.15f, Sickness: 2f, SepticShock: 5f),
		};

	private static readonly IReadOnlyDictionary<string, RemoteLiquidEffect> Liquids =
		new Dictionary<string, RemoteLiquidEffect>(System.StringComparer.Ordinal)
		{
			["water"] = new("water", ThirstPer100Ml: 9f, TemperaturePer100Ml: -0.25f),
			["carbonatedwater"] = new("carbonatedwater", ThirstPer100Ml: 9f, HappinessPer100Ml: 0.8f, TemperaturePer100Ml: -0.25f),
			["milk"] = new("milk", ThirstPer100Ml: 9f, HungerPer100Ml: 3f, HappinessPer100Ml: 0.5f, TemperaturePer100Ml: -0.25f),
			["applejuice"] = new("applejuice", ThirstPer100Ml: 9f, WeightPer100Ml: 0.1f, HappinessPer100Ml: 1f, TemperaturePer100Ml: -0.3f),
			["lemonade"] = new("lemonade", ThirstPer100Ml: 9f, WeightPer100Ml: 0.2f, HappinessPer100Ml: 1.2f, TemperaturePer100Ml: -0.3f),
			["icetea"] = new("icetea", ThirstPer100Ml: 9f, WeightPer100Ml: 0.4f, HappinessPer100Ml: 1.4f, TemperaturePer100Ml: -0.3f, SicknessPer100Ml: 3.5f),
			["soup"] = new("soup", ThirstPer100Ml: 9f, HungerPer100Ml: 10f, HappinessPer100Ml: 1f),
			["coffee"] = new("coffee", ThirstPer100Ml: 12f, WeightPer100Ml: 0.1f, StaminaPer100Ml: 25f, EnergyPer100Ml: 15f, HappinessPer100Ml: 2.5f, SicknessPer100Ml: 15f, CaffeinatedPer100Ml: 350f),
			["energydrink"] = new("energydrink", ThirstPer100Ml: 9f, WeightPer100Ml: 0.4f, StaminaPer100Ml: 25f, EnergyPer100Ml: 20f, HappinessPer100Ml: 2.5f, SicknessPer100Ml: 20f, CaffeinatedPer100Ml: 400f),
			["soda"] = new("soda", ThirstPer100Ml: 9f, WeightPer100Ml: 0.4f, StaminaPer100Ml: 4f, EnergyPer100Ml: 4f, HappinessPer100Ml: 1.5f, SicknessPer100Ml: 5f, CaffeinatedPer100Ml: 80f, TemperaturePer100Ml: -0.3f),
			["sportsdrink"] = new("sportsdrink", ThirstPer100Ml: 13f, WeightPer100Ml: 1f, StaminaPer100Ml: 25f, EnergyPer100Ml: 10f, HappinessPer100Ml: 0.5f, SicknessPer100Ml: 2f),
			["chocolatemilk"] = new("chocolatemilk", ThirstPer100Ml: 9f, HungerPer100Ml: 2.5f, HappinessPer100Ml: 1f, TemperaturePer100Ml: -0.25f, SicknessPer100Ml: 12f),
			["cereal"] = new("cereal", ThirstPer100Ml: 7.5f, HungerPer100Ml: 6f, HappinessPer100Ml: 1.5f, TemperaturePer100Ml: -0.25f),
			["ketchup"] = new("ketchup", ThirstPer100Ml: 6f, HungerPer100Ml: 8f, WeightPer100Ml: 1f, HappinessPer100Ml: -2.5f, SicknessPer100Ml: 5f),
		};

	public static bool IsFoodItem(string itemId) => Food.ContainsKey(itemId);

	public static bool IsKnownLiquid(string liquidId) => Liquids.ContainsKey(liquidId);

	public static bool TryGetFood(string itemId, out RemoteFoodEffect effect) =>
		Food.TryGetValue(itemId, out effect!);

	public static bool TryGetLiquid(string liquidId, out RemoteLiquidEffect effect) =>
		Liquids.TryGetValue(liquidId, out effect!);

	/// <summary>
	/// True when the liquid stack is non-empty, every liquid in it is in the
	/// curated catalog and the total is above zero. Mixed unknown liquids are
	/// refused as a whole so the host never approximates an unsupported effect.
	/// </summary>
	public static bool IsDrinkable(IReadOnlyList<LiquidStackMsg>? liquids)
	{
		if (liquids is null || liquids.Count == 0)
		{
			return false;
		}

		var total = 0f;
		foreach (var liquid in liquids)
		{
			if (!IsKnownLiquid(liquid.LiquidId))
			{
				return false;
			}

			total += liquid.Amount;
		}

		return total > 0f;
	}

	/// <summary>True when the item is either a known solid food or a drinkable liquid container.</summary>
	public static bool IsUsableItem(CharacterItemMsg item) =>
		IsFoodItem(item.ItemId) || IsDrinkable(item.Liquids);
}
