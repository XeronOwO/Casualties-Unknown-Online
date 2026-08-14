using CasualtiesUnknownOnline.Runtime.Protocol;
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

	/// <summary>Guest: generation finished (or finishing) but the start gate still holds — the GlobalDark fade must not black out the kept loading screen.</summary>
	bool IsInGateWindow { get; }

	void OnWorldGenerate();

	/// <summary>SceneManager.LoadScene(string) prefix — the old scene unloads inside the load: engage the destroy-report suppression BEFORE the teardown destroys (#191).</summary>
	void OnSceneLoadBegin();

	void OnBlockSet(Vector2Int pos, ushort block);

	void OnBlockDamaged(Vector2 pos, float dmg);

	/// <summary>A player's attack damaged a building entity (Body.cs:1946) — report it (the entity's health is local-only otherwise).</summary>
	void OnBuildingEntityDamaged(BuildingEntity entity, float damage);

	/// <summary>
	/// The local player swung — <c>Body.Attack</c> (Body.cs:1887, conscious +
	/// off-cooldown + doAttackAnim) or <c>Body.ThrowItem</c> (Body.cs:1665, with
	/// an item). Both play the one-shot <c>ArmsSwing</c> clip, which the peer's
	/// render clone must replay. Report the swing so it rides the IsAttacking
	/// snapshot flag (the clone plays ArmsSwing on the flag's rising edge).
	/// </summary>
	void OnArmSwing();

	/// <summary>A lockable entity was opened (health = 0 write path — Openable/lockpick/keypad) — report it.</summary>
	void OnBuildingEntityOpened(BuildingEntity entity);

	/// <summary>
	/// A trap/mechanism event fired (the trap's own trigger transition — the
	/// patch verified it). The local effect already ran (original behaviour):
	/// report (guest) or broadcast (host). Position-keyed: the entity's
	/// transform position.
	/// </summary>
	void OnTrapTriggered(EntityEventKind kind, Vector2 position, byte extra);

	/// <summary>A world entity started outside world generation (the spawn command)
	/// — a runtime creation: report it so the peers create the same entity at the same place.</summary>
	void OnEntityInstantiated(BuildingEntity entity);

	/// <summary>The spawn landing sound is deferred while the start gate holds — play it when the gate releases.</summary>
	void DeferLifePodSound();

	/// <summary>The spawn landing camera shake is deferred while the start gate holds — replay it at release.</summary>
	void DeferLifePodShake();

	/// <summary>
	/// Recipe.TryMake prefix: open the craft operation scope and snapshot the
	/// materials (null when the recipe has no matching materials — the game
	/// plays the Deny sound and nothing is consumed; no scope, no report). The
	/// returned state crosses to OnCraftEnd via Harmony __state.
	/// </summary>
	object? OnCraftBegin(Recipe recipe);

	/// <summary>Recipe.TryMake postfix: build ONE CraftReportMsg from the operation's terminal state (materials' post-state, liquid-container diffs, inventory-diff products) and commit it.</summary>
	void OnCraftEnd(object? state);

	/// <summary>
	/// Body.CombineItems prefix: open the combine scope and snapshot the
	/// pre-state (conditions + the water-branch decision). The returned state
	/// crosses to OnCombineEnd via Harmony __state.
	/// </summary>
	object? OnCombineBegin(Body body, Item it1, Item it2);

	/// <summary>Body.CombineItems postfix: verify the terminal change (a refused load or a full-condition no-op commits nothing) and commit ONE Combine report.</summary>
	void OnCombineEnd(object? state);

	/// <summary>LiquidTransfer.Finish ran (the transfer UI confirmed) — commit ONE LiquidTransfer report with both containers' post-state.</summary>
	void OnLiquidTransferFinished(WaterContainerItem transferTo, WaterContainerItem transferFrom);

	/// <summary>A craft-claimed destroyed item's end-of-frame OnDestroy must not report (its fact rode the craft report) — true consumes the claim.</summary>
	bool ShouldSuppressDestroy(Item item);

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

	/// <summary>The GlobalDark fade was skipped because the gate window holds (diagnostic — the "black, then the wait" report).</summary>
	void OnDarkenSkipped();

	/// <summary>
	/// An earthquake break (SetBlock(0) inside WorldGeneration.Update) — apply
	/// or drop it. Quake rate is per-side (16/s each), so overlapping player
	/// regions double the total ("two players standing together break faster").
	/// The block is applied only when it is far (> 60 blocks) from every EARLIER
	/// numbered player (SteamId order) — the region is already covered by them;
	/// the last-numbered player's coverage is free of overlaps, keeping the
	/// total break rate at solo level while separated players keep solo rate.
	/// </summary>
	bool ShouldApplyQuakeBreak(Vector2Int blockPos);

	/// <summary>A drag-drop was refused by DoPickupCheck (distance / line-of-sight) — diagnostic for "cannot take items out of a ground container".</summary>
	void OnPickupCheckFailed(string itemId, float distance, bool blocked);

	/// <summary>A drag release fell through to the world path (TryPerformWorldActions) instead of a UI target — the "cannot take out of a ground container" diagnostic.</summary>
	void OnDragReleasedToWorld();

	/// <summary>PickUpItem ran — where the item ended up (slot / container / world): the takeout-flow outcome diagnostic.</summary>
	void OnPickUpResult(string itemId, int slot, string home, Vector2 position);

	/// <summary>True while the deferred spawn landing sound is being replayed — the Sound.Play patch must not defer it again.</summary>
	bool IsReplayingLifePodSound { get; }

	void OnItemInstantiated(Item item);

	/// <summary>Item.Update's nullable dereference is about to throw (rb null / no WorldGeneration.world) — the diagnostic report, deduped by the domain (the menu-scene NRE burst hunt).</summary>
	void OnBrokenItemUpdate(Item item, string reason);

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

	/// <summary>An item was USED (Body.UseItemInHand / Body.UseItem — the use action ran) — report the post-use digest so the host validates and corrects.</summary>
	void OnItemUsed(Item item);

	/// <summary>One slot's occupant changed through a slot move (SwapSlots/SwitchHands) — report the item's new slot so the host's record stays current.</summary>
	void OnSlotMoved(Body body, int slot, string origin);

	/// <summary>An item was worn straight from the inventory (WearWearable — hand/backpack → limb) — a slot-move report with the limb wear encoding as the new slot, so the peers' clones re-home it immediately.</summary>
	void OnItemWorn(Item item);

	/// <summary>A fluid fixed-update tick — the session replaces the game's per-side simulation (host: the multi-member pass over every member's viewport; guest: nothing — the grid only changes through the streamed regions).</summary>
	void OnFluidFixedUpdate();

	/// <summary>The local player drank (DrinkLiquid ran with the full local effect) — report the consumed cell (guest → host; host → broadcast).</summary>
	void OnFluidDrinkReported(Vector2Int pos);

	/// <summary>
	/// A trader interaction ran locally (the full game method — the acting
	/// player's effects are already applied): report it (guest → host, the host
	/// executes the trader-side change) or broadcast the state (host — already
	/// authoritative). Purchase carries the locally-created item (the rollback
	/// hold for a rejected purchase).
	/// </summary>
	void OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, int itemValue, Item? purchaseItem);

	/// <summary>A speech bubble was spoken (the game method ran in full — the
	/// text is the FINAL string): report it (guest → host) or broadcast it
	/// (host — a player bubble to the other members, a trader bubble to every
	/// member).</summary>
	void OnSpeechReported(Talker talker, string text);

	/// <summary>An enemy bit the LOCAL player (SpiderHandler.DamageLimb ran on the
	/// local body) — capture the post-bite limb/body state and report it (guest →
	/// host) or broadcast it (host) as the dedicated EnemyBite event.</summary>
	void OnEnemyBite(Limb limb);
}
