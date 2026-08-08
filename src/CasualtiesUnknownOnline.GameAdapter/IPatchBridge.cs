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

	/// <summary>The spawn landing camera shake is deferred while the start gate holds — replay it at release.</summary>
	void DeferLifePodShake();

	bool OnGuestStartAttempt();

	/// <summary>
	/// Host clicked start (StartRun/StartTutorial entry — BEFORE the transition
	/// animation): tell the guests to start following immediately. isTutorial
	/// tells them which run to start — the world params do not exist yet (they
	/// are captured at the host's GenerateWorld boundary), so the entry kind
	/// rides in the join message instead of the params.
	/// </summary>
	void OnWorldJoinRequested(bool isTutorial);

	/// <summary>
	/// Guest side, called by the world-gen wrapper before the generation
	/// coroutine may consume any Random: false while the host's world params
	/// have not arrived (the wrapper holds); applying them (idempotent — a
	/// second call with the same params is a no-op) returns true. Host/solo:
	/// always true — nothing to wait for.
	/// </summary>
	bool EnsureGuestWorldParams();

	/// <summary>
	/// Called by the world-gen wrapper immediately before the generation
	/// coroutine starts: both sides force Random.state back to the captured
	/// baseline. The host captured it at its run-start entry (the click moment)
	/// — everything the game consumed between that moment and here (transition,
	/// scene loading) is overwritten, so both sides' generation streams start
	/// from the same state. Guest: the params were just applied, same value.
	/// </summary>
	void ResetGenStreamToBaseline();

	/// <summary>An inventory-internal move completed (SwapSlots/SwitchHands) — re-report the character snapshot immediately so the peer's clone updates in real time (the 1 Hz throttle alone reads as a 1-2 s delay).</summary>
	void OnInventoryChanged();

	// ---- World items (runtime-generated item entities) ----

	/// <summary>True in a live session — the spawn landing sound is deferred until the start-gate release.</summary>
	bool IsSessionActive { get; }

	/// <summary>True when this side is the session host (earthquake authority, host-only drops).</summary>
	bool IsHostMode { get; }

	/// <summary>An earthquake just started in WorldGeneration.Update — the host broadcasts it (timing sync + the next delay; guests re-align their timer).</summary>
	void OnEarthquakeStarted(float duration, float nextDelay);

	/// <summary>True while the deferred spawn landing sound is being replayed — the Sound.Play patch must not defer it again.</summary>
	bool IsReplayingLifePodSound { get; }

	void OnItemInstantiated(Item item);

	void OnItemDestroyed(Item item);

	/// <summary>Prefix of Body.PickUpItem — remember the world position (rollback target for a refused pickup).</summary>
	void OnItemPickupStart(Item item);

	void OnItemPickedUp(Item item);

	void OnItemDropped(Item item);

	/// <summary>
	/// An item was THROWN (Body.ThrowItem): the drop report fired in the
	/// DropItem prefix — BEFORE ThrowItem set the throw velocity (Body.cs:
	/// 1659-1661) — so a second report carrying the real flight velocity keeps
	/// the peer's copy flying instead of dropping in place.
	/// </summary>
	void OnItemThrown(Item item);

	/// <summary>An item was loaded into a container. WasWorldItem = it was part of the world before the load — a world item loaded into a body-side container (backpack) left the world (pickup semantics).</summary>
	void OnItemLoadedIntoContainer(Item item, bool wasWorldItem);

	void OnItemUnloadedFromContainer(Item item);

	void OnContainerUnloadedAll(Container container);
}
