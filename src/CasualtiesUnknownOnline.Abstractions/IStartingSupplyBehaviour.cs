namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The starting-supplies grant's engine-typed half (S4.3): what the Game Adapter does to a
/// live body — create the run setting's items and put them into the body's slots.
///
/// It lives in Abstractions because it must not reference Unity: the adapter implements it
/// over the live game, and a composition that has no engine at all supplies its own
/// implementation for tests or a dedicated server. That is the same shape
/// <see cref="IModItemSpawner"/> gives the mod content API, and it is what keeps the
/// grant's decision (and every test of it) free of game types — the CLR binds a method
/// body's Unity InternalCall members when it JITs the method, so a type that names one can
/// never run in a test host at all.
///
/// Items and bodies cross this boundary as opaque handles. The caller only ever compares a
/// body to itself (the grant's once-per-body rule) and hands an item straight back to the
/// body that produced the question, so no implementation shape leaks into the decision
/// this seam exists to keep testable.
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
