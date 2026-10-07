using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of SOLID consumables that may be used on
/// another player: the food items whose native use action feeds the eating body
/// through <c>Body.Eat</c> / <c>Body.Drink</c> and the body fields around them.
/// It is a read-only presence registry — the routing uses it only to decide
/// which held items the consume chain carries, never as a source of truth about
/// the effect. Unknown items are deliberately refused so an unsupported effect
/// is never silently approximated.
/// <para>
/// The DRINK half this catalog used to carry is gone: a liquid container's dose
/// is the ml its own use action computes on the operator's client
/// (<c>WaterContainerItem.Drink</c>) and its effect is the liquid's own
/// <c>onDrink</c> delegate, run on the affected side, so neither the
/// <c>DrinkAmountMl</c> constant nor the per-100-ml liquid table has anything
/// left to answer (see <see cref="ConsumeAdmission"/>). Solid food is a
/// different native shape — its <c>useAction</c> writes the eating body and the
/// item directly — so it keeps this table until it gets its own migration.
/// </para>
/// </summary>
public static class RemoteConsumeCatalog
{
	private static readonly IReadOnlyDictionary<string, RemoteFoodEffect> Food =
		new Dictionary<string, RemoteFoodEffect>(StringComparer.Ordinal)
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

	public static bool IsFoodItem(string itemId) => Food.ContainsKey(itemId);

	public static bool TryGetFood(string itemId, out RemoteFoodEffect effect) =>
		Food.TryGetValue(itemId, out effect!);
}
