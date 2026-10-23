using System.Collections.Concurrent;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Whether an item's own <c>useAction</c> is a DRINK, read from the delegate's compiled body before anything
/// runs it. The drink chain measures a dose by running the native action with its one <c>Drink</c> call
/// diverted, which is safe only when the action really is a drink: a usable item whose action is
/// <c>Item.DrawBlood</c> (the two blood bags) writes a limb's blood volume and fills the bag with blood, so
/// running it to find out that it does not drink is destructive. This classifier answers the question without
/// running anything, the way <see cref="GameSolidFoodFacts"/> answers the eating shape.
///
/// <para>
/// The verdicts are three, not two, because "not a drink" and "I cannot read this body" refuse for different
/// reasons and the caller names them differently; both fail closed. The classification is read once per item
/// id: the use action's compiled body never changes while the assembly is loaded, and the gesture path asks on
/// every release.
/// </para>
/// </summary>
internal static class GameDrinkActionFacts
{
	internal enum Verdict
	{
		/// <summary>The action reaches <c>WaterContainerItem.Drink</c>: the dose measurement can divert it.</summary>
		Drinks,

		/// <summary>The action is absent, the item is not usable, or the action reaches no Drink call — running it would be the defect.</summary>
		NotADrink,

		/// <summary>The action's compiled body cannot be read (a dynamic method): refuse rather than guess.</summary>
		Unreadable,
	}

	/// <summary>
	/// One verdict per id, read once. A <see cref="ConcurrentDictionary{TKey,TValue}"/> because the bridge
	/// method that reaches this is public: the in-repo callers are Unity main-thread patch callbacks, but a
	/// mod calling the bridge from its own thread must not race the cache (a doubled read is harmless — the
	/// verdict is a pure function of the item definition).
	/// </summary>
	private static readonly ConcurrentDictionary<string, Verdict> Verdicts = new();

	internal static Verdict Classify(string itemId)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			return Verdict.NotADrink;
		}

		return Verdicts.GetOrAdd(itemId, Read);
	}

	private static Verdict Read(string itemId)
	{
		// The registry is populated by the game's own item setup; a query before it has run (or on a build that
		// never loads items) answers "not a drink" instead of throwing inside a gesture path.
		if (Item.GlobalItems is not { } items
			|| !items.TryGetValue(itemId, out var info)
			|| !info.usable
			|| info.useAction is null)
		{
			return Verdict.NotADrink;
		}

		var instructions = UseActionInstructions.Read(info.useAction);
		if (instructions is null)
		{
			return Verdict.Unreadable;
		}

		return UseActionInstructions.Calls(instructions, typeof(WaterContainerItem), "Drink")
			? Verdict.Drinks
			: Verdict.NotADrink;
	}
}
