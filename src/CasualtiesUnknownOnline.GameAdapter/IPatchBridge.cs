using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The narrow surface Harmony patches may reach. Patch classes are static
/// (Harmony invokes them via reflection) so constructor injection cannot reach
/// them; instead the DI-owned GameAdapter binds once at construction and the
/// patches read only this interface — never the service itself (user
/// architecture rule: state belongs to its owner, DI owns behavior).
/// </summary>
internal interface IPatchBridge
{
	bool IsWorldGenIsolated { get; }

	bool IsWaitingForReady { get; }

	void OnWorldGenerate();

	void OnBlockSet(Vector2Int pos, ushort block);

	void OnBlockDamaged(Vector2 pos, float dmg);

	bool OnGuestStartAttempt();

	// ---- World items (runtime-generated item entities) ----

	void OnItemInstantiated(Item item);

	void OnItemDestroyed(Item item);

	/// <summary>Prefix of Body.PickUpItem — remember the world position (rollback target for a refused pickup).</summary>
	void OnItemPickupStart(Item item);

	void OnItemPickedUp(Item item);

	void OnItemDropped(Item item);

	void OnItemLoadedIntoContainer(Item item);

	void OnItemUnloadedFromContainer(Item item);

	void OnContainerUnloadedAll(Container container);
}
