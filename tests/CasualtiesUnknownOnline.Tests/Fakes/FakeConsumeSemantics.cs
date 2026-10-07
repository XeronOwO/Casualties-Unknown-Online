using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The suite's stand-in for the game's own registry behind
/// <see cref="IConsumeSemantics"/>. A test host has no game scene, so the vanilla
/// answer the consume chain reads at runtime (<c>Item.GlobalItems</c>) is listed
/// here instead: every item whose <c>ItemInfo</c> is a <c>LiquidItemInfo</c> with
/// <c>usable</c> set, which is the census of the decompiled <c>Item.cs</c> — the
/// class the game's own <c>Body.UseItem</c> gate admits. The production verdict is
/// never this list: it is the game's registry (GameConsumeFacts), so a stale entry
/// here can only make a suite test wrong, never a live session.
/// <para>
/// Three of the listed ids are NOT drinks even though the game marks them usable
/// (<c>bloodbag</c> and <c>bloodbaghuman</c> draw blood, <c>liquidcentrifuge</c>
/// runs the centrifuge): the item class cannot express which native call a
/// delegate makes, which is why the chain refuses a gesture whose own action
/// reached no drink. The two blood bags never get that far — the injection rule is
/// asked first at every site that could measure them
/// (<c>LocalUseItemEligibility.FamilyOf</c> on the operator's side, the host
/// chain's own order behind it), so their <c>DrawBlood</c> never runs here.
/// </para>
/// </summary>
internal sealed class FakeConsumeSemantics : IConsumeSemantics
{
	internal static readonly FakeConsumeSemantics Instance = new();

	/// <summary>The vanilla items whose <c>ItemInfo</c> is a <c>LiquidItemInfo</c> with <c>usable</c> set (Item.cs's SetupItems).</summary>
	private static readonly HashSet<string> UsableLiquidContainers = new(StringComparer.Ordinal)
	{
		// Drinkable medicines (Item.cs:947-1423).
		"naltrexone", "sodiumnitroprusside", "vasopressin", "amiodarone", "painkillers",
		"keratinbooster", "braingrow", "antidepressants", "antibiotics", "mindwipe",
		"antirad", "sleepingpills",
		// Medical containers that are also usable (Item.cs:1718-1925).
		"bloodbag", "saline", "ringersolution", "bloodbaghuman",
		// Drinks (Item.cs:3155-3522).
		"waterbottle", "minibarrel", "milk", "bowlofcereal", "chocolatemilk", "ketchup",
		"waterjug", "bleach", "energydrink", "coffee", "soup", "sodabottle", "applejuice",
		"lemonade", "icetea", "sodacan", "alcohol",
		// Utility and crafting containers (Item.cs:5667-6754).
		"liquidcentrifuge", "craftingbottle", "canteen",
	};

	public bool IsUsableLiquidContainer(string itemId) => UsableLiquidContainers.Contains(itemId);
}
