using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The suite's stand-in for the game's own answer behind
/// <see cref="ISolidFoodSemantics"/>. A test host has no game scene, so what the
/// production implementation reads at runtime — <c>Item.GlobalItems</c> plus the
/// item's own <c>useAction</c> delegate — is listed here instead.
/// <para>
/// The lists are the census of the decompiled <c>Item.SetupItems()</c>: the 41
/// vanilla items whose use action calls <c>Body.Eat</c>/<c>Body.Drink</c>, plus
/// the component-driven <c>nondescriptcan</c> whose action calls
/// <c>NonDescriptCan.Eat</c> one call away. Two of them are not the ordinary
/// shape: <c>exposedcore</c>'s action destroys the item object itself, and
/// <c>bucketofchicken</c>/<c>popcorn</c> hand the eater a replacement object at
/// their last bite. The production verdict is never this list — it is the
/// delegate's own compiled body (GameSolidFoodFacts), so a stale entry here can
/// only make a suite test wrong, never a live session.
/// </para>
/// </summary>
internal sealed class FakeSolidFoodSemantics : ISolidFoodSemantics
{
	internal static readonly FakeSolidFoodSemantics Instance = new();

	/// <summary>The vanilla items whose own use action feeds the eating body.</summary>
	private static readonly HashSet<string> FeedsTheBody = new(StringComparer.Ordinal)
	{
		"animalflesh", "aquapple", "banana", "blobflesh", "bloodsac", "bread", "browncap",
		"bucketofchicken", "bulbskin", "burger", "cactusflesh", "cake", "candybar", "cereal",
		"chips", "chocolatebar", "cookies", "dogfood", "dryfoliage", "experimentflesh",
		"exposedcore", "fleshchunk", "foliage", "foliagemeal", "frigiantfruit", "funguschunk",
		"geofruit", "hardcandy", "internalorgans", "mushpear", "nondescriptcan", "nutrientbar",
		"pancake", "paprikash", "pemmican", "pizzaslice", "popcorn", "popfruit", "steak",
		"stonefruitopen", "venomgland", "xalorissponge",
	};

	/// <summary>The one whose action also destroys the item object (<c>Object.Destroy(item.gameObject)</c>).</summary>
	private static readonly HashSet<string> DestroysTheItem = new(StringComparer.Ordinal) { "exposedcore" };

	/// <summary>The two whose action instantiates a replacement and hands it to the eater at the last bite.</summary>
	private static readonly HashSet<string> HandsOverAnItem = new(StringComparer.Ordinal) { "bucketofchicken", "popcorn" };

	public SolidFoodVerdict Classify(string itemId) =>
		!FeedsTheBody.Contains(itemId) ? SolidFoodVerdict.NotSolidFood
		: HandsOverAnItem.Contains(itemId) ? SolidFoodVerdict.EatsAndReplaces
		: DestroysTheItem.Contains(itemId) ? SolidFoodVerdict.EatsAndDestroys
		: SolidFoodVerdict.Eats;
}
