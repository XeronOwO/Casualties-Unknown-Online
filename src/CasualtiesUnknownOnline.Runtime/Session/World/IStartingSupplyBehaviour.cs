namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The starting-supplies grant's engine-typed half (S4.3): what the Game Adapter does to a
/// live body — create the run setting's items and put them into the body's slots.
///
/// It is a FRAMEWORK seam, not a mod contract: its one production implementation is the Game
/// Adapter's own, its one production caller is that adapter's own coordinator, and the test
/// host substitutes its own implementation — which is the whole reason the seam exists. So it
/// lives here, beside the other seams the adapter implements and a test host substitutes
/// (<see cref="INativeWorldFacts"/> in this namespace; <c>IModItemSpawner</c> and the
/// placement seams in <c>Runtime.Session.Mods</c>), and not on the mod-visible Abstractions
/// surface — which promises mod authors a shape and therefore may not carry a type no mod can
/// reach.
///
/// Nothing here names Unity: the CLR binds a method body's Unity InternalCall members when
/// it JITs the method, so a type that names one can never run in a test host at all. That is
/// what keeps the grant's decision (and every test of it) free of game types.
///
/// Items and bodies cross this boundary as opaque handles. The caller only ever compares a
/// body to itself (the grant's once-per-body rule) and hands an item straight back to the
/// body that produced the question, so no implementation shape leaks into the decision this
/// seam exists to keep testable — and identity, which is the whole of what the caller reads,
/// is exactly what a handle carries. An engine-typed parameter is not the alternative here:
/// this assembly may not reference the game's assemblies, which is the reason the seam
/// exists at all.
/// </summary>
public interface IStartingSupplyBehaviour
{
	/// <summary>The local player's body, or null when there is none (a menu scene, a scene swap in flight).</summary>
	object? LocalBody { get; }

	/// <summary>
	/// Create one plan item at the local body's position. Null = the id produced no item
	/// (or there is no body to create it at): the caller accounts for it as unplaced
	/// rather than claiming it landed.
	/// </summary>
	/// <param name="itemId">The content id (the game's own <c>Utils.Create</c> resource name).</param>
	object? Create(string itemId);

	/// <summary>
	/// Put a created item into <paramref name="slot"/> of <paramref name="body"/> (a handle
	/// this implementation produced earlier). False = the slot could not take it, and the
	/// item stays where it was created.
	/// </summary>
	bool TryPlace(object body, object item, int slot);
}
