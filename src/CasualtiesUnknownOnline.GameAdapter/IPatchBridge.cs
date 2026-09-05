using System.Collections;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The narrow surface Harmony patches may reach. Patch classes are static
/// (Harmony invokes them via reflection) so constructor injection cannot reach
/// them; instead the DI-owned GameAdapter binds once at construction and the
/// patches read only this interface — never the service itself (user
/// architecture rule: state belongs to its owner, DI owns behavior).
/// </summary>
internal interface IPatchBridge : IRemoteBackpackPatchBridge
{
	bool IsWorldGenIsolated { get; }

	bool IsWaitingForReady { get; }

	/// <summary>True while the CUO Online UI modal window is open — the adapter suppresses the game's native input handling (pause/ESC) behind it.</summary>
	bool IsOnlineUiModalOpen { get; }

	/// <summary>Guest: generation finished (or finishing) but the start gate still holds — the GlobalDark fade must not black out the kept loading screen.</summary>
	bool IsInGateWindow { get; }

	/// <summary>
	/// PlayerCamera.DoAlert is about to show a popup while the start-gate
	/// window holds (the layer title at generation end, its delayed
	/// description) — queue it and return true to skip the original; false
	/// lets the popup show normally.
	/// </summary>
	bool TryDeferStartGateAlert(string text, bool important);

	void OnWorldGenerate();

	/// <summary>SceneManager.LoadScene(string) prefix — the old scene unloads inside the load: engage the destroy-report suppression BEFORE the teardown destroys (#191).</summary>
	void OnSceneLoadBegin();

	/// <summary>
	/// Wrap the vanilla <c>WorldGenerateWorldBorders</c> iterator so the Game
	/// Adapter can distribute mod-bound structures after the vanilla worldgen
	/// has finished its terrain/structures but before the collider pass. The
	/// returned coroutine is driven by <see cref="WorldGenRandomIsolation"/>,
	/// so its Random consumption stays on the sealed generation stream.
	/// </summary>
	IEnumerator WrapStructureWorldGen(IEnumerator original, WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.GenerateOres</c> finished — distribute
	/// mod-bound custom tile ore after the vanilla ore pass but before the
	/// structure/liquid passes, all inside the sealed generation stream.
	/// </summary>
	void OnCustomTileOreGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.GenerateOres</c> finished — distribute
	/// mod-bound liquid-tile pools after the vanilla ore pass, inside the same
	/// sealed generation stream.
	/// </summary>
	void OnCustomLiquidWorldGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.PlaceCrystals</c> finished — distribute
	/// mod-bound custom building entities after colliders exist, inside the same
	/// sealed generation stream. The deterministic buildings never need a wire
	/// message; runtime Start reports are suppressed during generation.
	/// </summary>
	void OnCustomBuildingWorldGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.PlaceCrystals</c> finished — scatter
	/// mod-bound custom items' loose world spawns, inside the same sealed
	/// generation stream. The resulting ground items are later bound by the
	/// existing generation-item snapshot.
	/// </summary>
	void OnCustomItemWorldGeneration(WorldGeneration world);

	void OnBlockSet(Vector2Int pos, ushort block);

	void OnBlockDamaged(Vector2 pos, float dmg, bool bonusMetal);

	/// <summary>
	/// A local block break hit a custom tile index. The Game Adapter spawns the
	/// tile's authored drops while still inside the damage-block scope so they
	/// ride the pending break report like vanilla block drops.
	/// </summary>
	void OnCustomTileBroken(WorldGeneration world, Vector2Int cell, ushort block);

	/// <summary>
	/// PlayerCamera.SetTimeScale is starting (outside CUO apply/sleep-suppress
	/// scopes). Returns whether the ORIGINAL method may run: the host always
	/// may (it is the time authority); a guest may only run local-only speeds
	/// (Slowmo/Paused) or forced local transitions — Normal/Fast/SuperFast
	/// become host requests, UnconsciousFast/DyingFast are host-owned.
	/// </summary>
	bool OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool force);

	/// <summary>PlayerCamera.SetTimeScale finished — the host reports the speed it just applied so the world-time domain can broadcast it (guests never report; apply/sleep scopes never report).</summary>
	void OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed);

	/// <summary>A building entity was damaged (Body.cs:1946 attack, explosion diff, or cactus collision self-damage) — report it (the entity's health is local-only otherwise). <c>playHitSound</c> is true for attack/explosion damage (the receiver replays the entity's own hitSound) and false for silent damage sources such as cactus collision self-damage. <c>playHitFlash</c> is true only for a Body.Attack melee hit (the receiver replays the native red HitFlash).</summary>
	void OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound = true, bool playHitFlash = false);

	/// <summary>
	/// The local player swung — <c>Body.Attack</c> (Body.cs:1887, conscious +
	/// off-cooldown + doAttackAnim), <c>Body.ThrowItem</c> (Body.cs:1665, with
	/// an item), or a successful direct placeable use (<c>scrapmetal</c> /
	/// <c>climbingrope</c> / <c>scaffoldingpack</c>, Item.cs:2165/2208/2249). All
	/// play the one-shot <c>ArmsSwing</c> clip, which the peer's render clone
	/// must replay. Report the swing so it rides the IsAttacking snapshot flag
	/// (the clone plays ArmsSwing on the flag's rising edge).
	/// </summary>
	void OnArmSwing();

	/// <summary>
	/// The local player's <c>Body.Attack</c> instantiated its one-shot
	/// <c>attackAnim</c> prefab (ClawAnim/SwingAnim/LaserAnim, Body.cs:1913-1920)
	/// — report the exact prefab + facing + attack direction so the peers replay
	/// the same visual on the owner's render clone.
	/// </summary>
	void OnAttackAnim(string prefab, Vector2 direction, bool isRight, Vector2 position);

	/// <summary>A lockable entity was opened (health = 0 write path — Openable/lockpick/keypad) — report it.</summary>
	void OnBuildingEntityOpened(BuildingEntity entity);

	/// <summary>
	/// A trap/mechanism event fired (the trap's own trigger transition — the
	/// patch verified it). The local effect already ran (original behaviour):
	/// report (guest) or broadcast (host). Position-keyed: the entity's
	/// transform position.
	/// </summary>
	void OnTrapTriggered(EntityEventKind kind, Vector2 position, byte extra);

	/// <summary>
	/// A player-lit dynamite item detonated (CustomItemBehaviour.DynamiteExplode)
	/// — the native explosion ran on this side; report the one-shot fact (item
	/// id + world position) so the host applies it to its own world and the
	/// peers replay the body/visual segment.
	/// </summary>
	void OnDynamiteExploded(ulong itemId, Vector2 position);

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
	/// plays the Deny sound and nothing is consumed; no scope, no report). A
	/// non-null refusal marker tells the Harmony prefix to skip the native
	/// consume path (the crafting-content guard). The normal returned state
	/// crosses to OnCraftEnd via Harmony __state.
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

	/// <summary>
	/// The extra encumbrance a LOCAL carrier body owes while it carries or
	/// piggybacks a teammate. Zero for non-local bodies or when there is no
	/// active carry relation, so the native <c>Body.GetTotalEncumberance</c>
	/// result is unchanged for remote clones and standalone players.
	/// </summary>
	float GetCarriedEncumbrance(Body body);

	/// <summary>
	/// True when the given body is the LOCAL player's body and that player is
	/// currently the carrier half of a carry/piggyback relation. Harmony pose
	/// patches use this to suppress the native idle-sit while carrying; the
	/// runtime carry mirror is the single source of truth (no extra local
	/// marker to keep in sync).
	/// </summary>
	bool IsLocalCarrier(Body body);

	/// <summary>
	/// The local carrier's <c>Body.Update</c> finished. Re-pin the remote rider
	/// clones to the just-updated local carrier transform; CUO's own update pump
	/// may have pinned them before the game moved the local body this frame.
	/// </summary>
	void OnLocalCarrierBodyUpdated();

	// ---- World items (runtime-generated item entities) ----

	/// <summary>True in a live session — the spawn landing sound is deferred until the start-gate release.</summary>
	bool IsSessionActive { get; }

	/// <summary>True when this side is the session host (earthquake authority, host-only drops).</summary>
	bool IsHostMode { get; }

	/// <summary>
	/// Resolve a mod-registered runtime item template by item id. Returns false
	/// when the id has no custom template; the caller falls back to
	/// <c>Resources.Load</c> for vanilla items.
	/// </summary>
	bool TryResolveItemTemplate(string id, out GameObject? template);

	/// <summary>
	/// Resolve a mod-registered runtime building template by building id.
	/// Returns false when the id has no custom template; the caller falls back
	/// to <c>Resources.Load</c> for vanilla buildings.
	/// </summary>
	bool TryResolveBuildingTemplate(string id, out GameObject? template);

	/// <summary>
	/// Apply a registered runtime building instance hook to a freshly
	/// instantiated custom building. The instance is still inactive when this is
	/// called, so hook-returned components attach before <c>Awake</c> runs.
	/// </summary>
	void ApplyCustomBuildingInstanceHooks(string id, GameObject instance);

	/// <summary>
	/// Resolve the synthetic <c>ItemLootPool</c> category for a fixed drop
	/// source. Returns false when no custom items have been registered for that
	/// source or the loot pool is not ready yet.
	/// </summary>
	bool TryGetModDropSourceCategory(ModItemDropSource source, out string category);

	/// <summary>
	/// Returns the mod-authored <see cref="BlockInfo"/> for a custom tile
	/// index, or null when the block is vanilla/unregistered. The
	/// <c>WorldGeneration.GetBlockInfo</c> patch uses this to let the original
	/// switch continue handling every vanilla block while supplying behavior for
	/// static custom tiles.
	/// </summary>
	BlockInfo? TryGetCustomBlockInfo(ushort block);

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

	/// <summary>
	/// PlayerCamera.HandleReleaseDragging released a dragged item over an
	/// in-world remote player. Returns true when the cross-player use request was
	/// sent (the native drop must be skipped), false to let the original drop
	/// path run.
	/// </summary>
	bool TryHandleDraggedItemUseOnRemote(Item dragItem, Body localBody);

	/// <summary>PickUpItem ran — where the item ended up (slot / container / world): the takeout-flow outcome diagnostic.</summary>
	void OnPickUpResult(string itemId, int slot, string home, Vector2 position);

	/// <summary>True while the deferred spawn landing sound is being replayed — the Sound.Play patch must not defer it again.</summary>
	bool IsReplayingLifePodSound { get; }

	void OnItemInstantiated(Item item);

	/// <summary>Item.Update's nullable dereference is about to throw (rb null / no WorldGeneration.world) — the diagnostic report, deduped by the domain (the menu-scene NRE burst hunt).</summary>
	void OnBrokenItemUpdate(Item item, string reason);

	/// <summary>A guest-side standalone world-item copy produced a native impact
	/// effect (drop/step sound, dust, plush squeak) that was suppressed because
	/// the host owns the physics. Logged so this authority boundary is
	/// observable without making collision callbacks noisy at normal levels.</summary>
	void OnNonAuthoritativeItemImpactSuppressed(Item item, string source);

	void OnItemDestroyed(Item item);

	/// <summary>True while this side may run the Heater cooker's native conversion (host/solo, or a guest without an active session). A guest in a live session returns false — its world items are layer-isolated and the host's ItemCook broadcast owns the conversion.</summary>
	bool IsHeaterCookAuthority { get; }

	/// <summary>HeaterCookPatch prefix: the native conversion is about to run — stamp the raw item's instance id and return it (0 = not reportable, keep the generic fallback).</summary>
	ulong OnHeaterCookBegin(Item item);

	/// <summary>HeaterCookPatch postfix: the created steak passed the fingerprint check — commit one ItemCook report (the source id leaves the table, the steak enters it, both generic hooks stay silent).</summary>
	void OnHeaterCookCompleted(ulong sourceItemId, Item cookedItem, float sourceCondition, Vector2 sourcePosition);

	/// <summary>HeaterCookPatch postfix could not identify the created steak — log it; the generic item hooks remain the fallback.</summary>
	void OnHeaterCookCaptureFailed(ulong sourceItemId);

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

	/// <summary>A GunScript persistent-state transition ran (fire/rack/safety/
	/// load/unload or an Update-driven auto-rack step) — the gun-state sync
	/// domain compares it to the last reported snapshot and only routes an
	/// actual change through the existing item-use fact path.</summary>
	void OnGunStateChanged(GunScript gun);

	/// <summary>One slot's occupant changed through a slot move (SwapSlots/SwitchHands) — report the item's new slot so the host's record stays current.</summary>
	void OnSlotMoved(Body body, int slot, string origin);

	/// <summary>An item was worn straight from the inventory (WearWearable — hand/backpack → limb) — a slot-move report with the limb wear encoding as the new slot, so the peers' clones re-home it immediately.</summary>
	void OnItemWorn(Item item);

	/// <summary>A fluid fixed-update tick — the session replaces the game's per-side simulation (host: the multi-member pass over every member's viewport; guest: nothing — the grid only changes through the streamed regions).</summary>
	void OnFluidFixedUpdate();

	/// <summary>The local player drank (DrinkLiquid ran with the full local effect) — report the consumed cell (guest → host; host → broadcast).</summary>
	void OnFluidDrinkReported(Vector2Int pos);

	/// <summary>
	/// <c>FluidManager.RenderFluids</c> is about to render. Returns true when
	/// custom liquid tiles are present and the adapter rendered them (the
	/// original must be skipped); false keeps the vanilla render path.
	/// </summary>
	bool TryRenderCustomLiquids(FluidManager manager);

	/// <summary>Resolve the display colour for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomLiquidColor(byte worldByte, out Color color);

	/// <summary>Resolve water info for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type);

	/// <summary>Resolve display name/description for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomLiquidName(byte worldByte, out string name, out string description);

	/// <summary>
	/// <c>FluidManager.DrinkLiquid</c> is about to run on a custom world-fluid
	/// byte. Returns true when the adapter applied the drink (the original must
	/// be skipped); false lets the vanilla method handle it.
	/// </summary>
	bool TryDrinkCustomLiquid(FluidManager fluid, Vector2Int pos, Body body);

	/// <summary>
	/// <c>Body.HandleVariableUpdates</c> finished — re-apply the local body's
	/// per-second liquid-tile touch rates. The projection class filters to the
	/// local body.
	/// </summary>
	void ApplyLiquidTileBodyTouch(Body body);

	/// <summary>
	/// A trader interaction ran locally (the full game method — the acting
	/// player's effects are already applied): report it (guest → host, the host
	/// executes the trader-side change) or broadcast the state (host — already
	/// authoritative). Purchase carries the locally-created item (the rollback
	/// hold for a rejected purchase).
	/// </summary>
	void OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, int itemValue, Item? purchaseItem);

	/// <summary>A hostile trader swing ran locally (TraderScript.Swing — the
	/// animation, swing sound and local damage are already applied) — report
	/// the presentation so the peers replay it on their same-position trader.</summary>
	void OnTraderSwing(TraderScript trader);

	/// <summary>A speech bubble was spoken (the game method ran in full — the
	/// text is the FINAL string): report it (guest → host) or broadcast it
	/// (host — a player bubble to the other members, a trader bubble to every
	/// member).</summary>
	void OnSpeechReported(Talker talker, string text);

	/// <summary>An enemy bit the LOCAL player (SpiderHandler.DamageLimb ran on the
	/// local body) — capture the post-bite limb/body state and report it (guest →
	/// host) or broadcast it (host) as the dedicated EnemyBite event.</summary>
	void OnEnemyBite(Limb limb);

	/// <summary>A limb latch changed on the LOCAL body (BreakBone/MendBone/
	/// Dislocate/UnDislocate/Dismember — the patch verified the write) — report
	/// the body's full post-event terminal state (guest → host) or broadcast it
	/// (host) as the dedicated LimbStateEvent event.</summary>
	void OnLimbStateEvent(Limb limb);

	/// <summary>A spider just recomputed its move target (host side) — the combat director replaces the single-player OverlapCircle result with the nearest in-world player.</summary>
	void OnSpiderTargetDecided(SpiderHandler spider);

	/// <summary>CrystalEnemy.body getter resolved (host side) — the combat director may replace the local body with the nearest remote player body.</summary>
	void OnCrystalEnemyBodyResolved(CrystalEnemy enemy, ref Body body);

	/// <summary>
	/// CrystalEnemy.Lunge is starting (host side). Returns a Harmony __state for
	/// the local-victim case (the pre-lunge limb trace — the postfix verifies
	/// the actual limb write and then reports EnemyLungeMsg), or null when the
	/// host ordered a remote victim / no victim is along the ray.
	/// </summary>
	object? OnCrystalLungeBegin(CrystalEnemy enemy);

	/// <summary>CrystalEnemy.Lunge just finished (host side) — verify the native hit on the local body and report the post-lunge terminal state when the limb diff confirms it.</summary>
	void OnCrystalLungeEnd(object? state);

	/// <summary>
	/// SpiderHandler.OnCollisionEnter2D completed (host side) — an item hit an
	/// animal. The native branch only runs within 50 units of the local body;
	/// this entry generalizes to the in-world player set, applies the missing
	/// host-side effects when native skipped, and returns the health damage to
	/// relay through the existing BuildingEntityDamaged event.
	/// </summary>
	float? OnEnemyItemCollision(SpiderHandler spider, Collision2D collision);

	/// <summary>ElderThornbackBehaviour.Update ran its 1 s proximity tick on the local body — report the post-tick terminal state.</summary>
	void OnElderHorrorTick(Body body);

	/// <summary>ElderThornbackBehaviour.OnDestroy rewarded the local player — report the post-reward terminal state.</summary>
	void OnElderHorrorDefeat(Body body);

	/// <summary>XalorisScript.OnWillRenderObject ran its 0.5 s septic tick on the local body — report the post-tick septic shock.</summary>
	void OnXalorisSepticTick(Body body);

	/// <summary>GrabberPlant.Update grabbed the local body — report the post-grab terminal state.</summary>
	void OnGrabberGrabbed(Body body);
	/// <summary>
	/// The game just played a local player-character action event (the Sound.Play
	/// call ran inside a Body.Attack / ThrowItem / TryExertSound call-identity
	/// scope, or GunScript.Fire postfix — the clip is the EXACT chosen one;
	/// GunFire also carries the recoil kick). Report it so the peers replay the
	/// presentation on the owner's clone (guest → host; host → broadcast).
	/// </summary>
	void OnCharacterSound(CharacterSoundKind kind, string clip, Vector2 pos, float volume, bool followOwner, bool twoDimensional, float recoilDegrees);

	/// <summary>
	/// The local body just landed (Body.HandleGroundedState's became-grounded
	/// branch — the Grounded clip already played and the native landing dust
	/// already spawned when <paramref name="cloudSize"/> is non-zero). Report it
	/// so the peers replay the presentation on the owner's clone (guest → host;
	/// host → broadcast).
	/// </summary>
	void OnCharacterLandingVisual(byte cloudSize, Vector2 position, float velocityX);

	/// <summary>
	/// The local body just collapsed via the game's ragdoll key (PlayerCamera's
	/// ragdoll input path ran Body.Ragdoll and the standing flag flipped).
	/// Report it so the peers replay the lying pose on the owner's clone
	/// immediately (guest → host; host → broadcast); the 20 Hz entity-state
	/// stream remains the fallback.
	/// </summary>
	void OnCharacterRagdoll(Vector2 position);

	/// <summary>
	/// The local player's BleedParticle just spawned a world-blood decal (the
	/// native <c>BleedParticle.Update</c> transient ground/wall branch). Report
	/// the decal so the peers spawn the same visual (guest → host; host →
	/// broadcast). The decal is presentation-only and transient.
	/// </summary>
	void OnWorldBloodSpawn(Vector2 position, bool ground);

	/// <summary>
	/// The local player's <c>Body.Update</c> finished. Re-assert the decoded
	/// mod-status body projection on this body (the bridge checks it is the
	/// local body and the projection service owns the effect).
	/// </summary>
	void ApplyBodyStatusProjection(Body body);

	/// <summary>
	/// The local player's <c>Limb.Update</c> finished. Re-assert the decoded
	/// mod-status limb projection on this limb (the bridge checks the limb
	/// belongs to the local body and the projection service owns the effect).
	/// </summary>
	void ApplyLimbStatusProjection(Body body, Limb limb);
	/// <summary>
	/// The local player's <c>Body.HandleCirculation</c> is about to run. Remove
	/// the previously reapplied mod circulation offset so the native formula
	/// computes from the unmodified base (the bridge checks local body).
	/// </summary>
	void ApplyBodyCirculationPrefix(Body body);

	/// <summary>
	/// The local player's <c>Body.HandleCirculation</c> finished. Reapply the
	/// current decoded mod circulation offsets and refresh the native readout
	/// strings (the bridge checks local body).
	/// </summary>
	void ApplyBodyCirculationPostfix(Body body);

	/// <summary>
	/// The vanilla moodle manager is rebuilding its rows. Add the active mod
	/// moodles whose static definition belongs to <paramref name="importantRow"/>
	/// (true = main row, before the native side-row switch; false = side row,
	/// after the native side moodles).
	/// </summary>
	void ApplyModMoodles(MoodleManager manager, bool importantRow);



}
