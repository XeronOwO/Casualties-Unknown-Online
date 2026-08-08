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

	/// <summary>A player's attack damaged a building entity (Body.cs:1946) — report it (the entity's health is local-only otherwise).</summary>
	void OnBuildingEntityDamaged(BuildingEntity entity, float damage);

	/// <summary>A lockable entity was opened (health = 0 write path — Openable/lockpick/keypad) — report it.</summary>
	void OnBuildingEntityOpened(BuildingEntity entity);

	/// <summary>The spawn landing sound is deferred while the start gate holds — play it when the gate releases.</summary>
	void DeferLifePodSound();

	bool OnGuestStartAttempt();

	// ---- World items (runtime-generated item entities) ----

	/// <summary>
	/// Guest in a live session: BuildingEntity's destroy-drop branch (BuildingEntity.cs:56-121)
	/// is suppressed — the entity's health is reduced on BOTH sides by the BlockDamaged
	/// stream, so both would roll drops independently (two different items). Only the host
	/// rolls and reports them; the guest just destroys the entity.
	/// </summary>
	bool IsGuestItemDropSuppressed { get; }

	/// <summary>True in a live session — the spawn landing sound is deferred until the start-gate release.</summary>
	bool IsSessionActive { get; }

	/// <summary>True while the deferred spawn landing sound is being replayed — the Sound.Play patch must not defer it again.</summary>
	bool IsReplayingLifePodSound { get; }

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
