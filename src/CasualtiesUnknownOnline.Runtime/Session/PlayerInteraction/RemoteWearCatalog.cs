using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative catalog of native wearable items for the cross-player
/// item-use slice. It maps every known wearable item id to its
/// <see cref="RemoteWearProfile"/> (<c>desiredWearLimb</c> and
/// <c>wearSlotId</c> from Item.cs SetupItems). Unknown devices are refused as a
/// whole so an unsupported wearable is never silently approximated. Pure data
/// — no game assembly dependency.
/// </summary>
public static class RemoteWearCatalog
{
	private static readonly IReadOnlyDictionary<string, RemoteWearProfile> Registry =
		new Dictionary<string, RemoteWearProfile>(System.StringComparer.Ordinal)
		{
			// Back/upper-torso wearables (Item.cs 5887-5971, 6367-6385, 6508-6526).
			["smallpack"] = new("smallpack", "back", 1),
			["duffelbag"] = new("duffelbag", "back", 1),
			["slingbag"] = new("slingbag", "back", 1),
			["bigpack"] = new("bigpack", "back", 1),
			["scubadivinggear"] = new("scubadivinggear", "back", 1),
			["jetpack"] = new("jetpack", "back", 1),

			// Leg pouches (Item.cs 5973-6030).
			["legpouch"] = new("legpouch", "thigh", 9),
			["materialpouch"] = new("materialpouch", "thighback", 12),

			// Torso-front wearables (Item.cs 5993-6010, 6528-6545, 6563-6580, 6639-6660).
			["liquidpouch"] = new("liquidpouch", "torsofront", 2),
			["fannypack"] = new("fannypack", "torsofront", 2),
			["bellyarmor"] = new("bellyarmor", "torsofront", 2),
			["traumarig"] = new("traumarig", "torsofront", 2),

			// Head wearables (Item.cs 6031-6238).
			["bikehelmet"] = new("bikehelmet", "hat", 0),
			["riothelmet"] = new("riothelmet", "hat", 0),
			["makeshifthelmet"] = new("makeshifthelmet", "hat", 0),
			["headlamp"] = new("headlamp", "hat", 0),
			["makeshiftheadlamp"] = new("makeshiftheadlamp", "hat", 0),
			["holidayhat"] = new("holidayhat", "hat", 0),
			["dustmask"] = new("dustmask", "mouth", 0),
			["safetyglasses"] = new("safetyglasses", "eyes", 0),
			["autozoomgoggles"] = new("autozoomgoggles", "eyes", 0),
			["blindfold"] = new("blindfold", "blindfold", 0),
			["balaclava"] = new("balaclava", "balaclava", 0),

			// Neck (Item.cs 6240-6259), autopump (Item.cs 1221-1238) and upper-torso cloth (Item.cs 6345-6492, 6599-6618).
			["scarf"] = new("scarf", "neck", 1),
			["autopump"] = new("autopump", "outertorso", 1),
			["tornshirt"] = new("tornshirt", "torso", 1),
			["hoodie"] = new("hoodie", "outertorso", 1),
			["striderpelt"] = new("striderpelt", "outertorso", 1),
			["carapace"] = new("carapace", "outertorso", 1),
			["bandolier"] = new("bandolier", "bandolier", 1),

			// Hands/arms (Item.cs 6260-6343, 6493-6507).
			["latexgloves"] = new("latexgloves", "hands", 5),
			["tacticalgloves"] = new("tacticalgloves", "hands", 5),
			["climbingclaws"] = new("climbingclaws", "hands", 5),
			["armwarmers"] = new("armwarmers", "arms", 4),
			["limbwraps"] = new("limbwraps", "wraps", 4),

			// Feet (Item.cs 6387-6447).
			["sneakers"] = new("sneakers", "feet", 11),
			["tacticalboots"] = new("tacticalboots", "feet", 11),
			["woodsandals"] = new("woodsandals", "feet", 11),

			// Knees and belt (Item.cs 6581-6598, 6620-6638).
			["kneepads"] = new("kneepads", "knees", 10),
			["belt"] = new("belt", "belt", 2),
		};

	public static bool IsWearItem(string itemId) => Registry.ContainsKey(itemId);

	public static bool TryGet(string itemId, out RemoteWearProfile profile) =>
		Registry.TryGetValue(itemId, out profile!);
}
