using System.Collections;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The IPatchBridge implementation. Harmony patches are static and cannot
/// receive constructor injection, so the DI-owned <see cref="GameAdapter"/>
/// binds this object once at construction; the patches read only this narrow
/// surface. Keeping the bridge in its own top-level class removes the entire
/// forwarding half from the adapter coordinator.
/// <para>
/// It also serves the per-domain patch ports the aggregate deliberately does not
/// carry (<see cref="IFluidPatchPort"/> today) — the seam resolves a port by
/// casting the bound bridge, so this declaration is what makes it reachable.
/// </para>
/// </summary>
internal sealed class GameAdapterBridge(GameAdapterDomains domains) : IPatchBridge, IFluidPatchPort, ILayerAdvancePatchPort, IItemCategoryPatchPort, IWearablePatchPort, IDragUseCuePatchPort, ISpectatorPatchPort, ISaveCutPatchPort, ILayerPanelPatchPort
{
	private readonly RemoteDragIntentDispatcher _remoteDragIntents = new(domains);
	private readonly RemoteMedicalOperationHandler _remoteMedicalOps = new(domains);
	private readonly ModContentPatchBridge _modContent = new(domains);
	private readonly CarriagePatchBridge _carriage = new(domains);

	public IModContentPatchBridge ModContent => _modContent;

	public ICarriagePatchBridge Carriage => _carriage;

	public ISessionSurfacePatchBridge SessionSurface => domains.MenuInput;

	public bool IsWorldGenIsolated => true;

	public bool IsWaitingForReady => domains.Gate.WaitingForReady;

	public bool IsInGateWindow => domains.Run.IsInGateWindow;

	public bool IsSessionActive => domains.Session.SessionActive;

	public bool IsHostMode => domains.Session.Role == SessionRole.Host && domains.Session.SessionActive;


	/// <inheritdoc />
	public float? RadiationLineCeiling
	{
		get
		{
			// The HOST's clamp, deliberately not a member's: a member's line follows the host's absolute state
			// (RadiationLineStateMsg at 5 Hz), and a member that clamped on its own would also do it while it holds
			// NO announced rule set - reading its own config - which is a local safe zone the host does not have and
			// cannot correct. A member's own descent between two broadcasts is a few tenths of a unit and is what the
			// host's next broadcast already corrects.
			if (!IsHostMode || !domains.HostRules.RadiationLineStopsAboveTheFloor)
			{
				return null;
			}

			// The line's position is halfHeight - timeGone (RadiationLine.cs), so a cap of
			// height - margin parks it margin world units above the layer floor - the behaviour the
			// host rule asks for, and the same number KrokMP uses.
			var world = WorldGeneration.world;
			return world != null ? world.height - RadiationLineTable.FloorMarginUnits : null;
		}
	}

	public bool IsReplayingLifePodSound => domains.LifePod.IsReplayingSound;

	/// <inheritdoc />
	public float? RadiationLineGuestTimeGone => domains.RadiationLineSync.GuestTimeGone();

	public bool IsHeaterCookAuthority =>
		domains.Session.Role != SessionRole.Guest || !domains.Session.SessionActive;

	public bool TryDeferStartGateAlert(string text, bool important) => domains.Gate.DeferAlert(text, important);

	public void OnWorldGenerate()
	{
		domains.Run.OnWorldGenerate();
		// Every enemy id and match hint names the layer being left: the roster state starts empty on BOTH roles, and the host holds snapshots until its new capture is complete.
		domains.EnemySync.OnWorldGenerationStarted();
		domains.Gate.AttachKeepLoading(); // both roles: while the gate waits for the others, the loading animation keeps playing
										  // The tutorial/debug layers skip the game's ResetLayerModifiers
										  // (WorldGeneration.cs:3280-3283 — it only runs for biomeOverride == None),
										  // so the previous layer's modifier keeps its static active state: a run
										  // with a modifier followed by the tutorial showed no banner (the tutorial
										  // never rolls one, WorldGeneration.cs:3626-3628) but the modifier's
										  // effects still ran. Clear it on EVERY side — the modifier instances are
										  // process-static (LayerModifier.availableModifiers), one set per game;
										  // the guest's active was set by the snapshot path.
		if (WorldGeneration.world != null && HarmonyTraverse.ReadBiomeOverride() != 0) // Unity object — ==; 0 = None
		{
			WorldGeneration.world.ResetLayerModifiers();
			domains.Log.LogInformation("[LayerMod] non-none layer — cleared the previous layer's residual modifiers.");
		}

		if (domains.Session.Role != SessionRole.Guest)
		{
			// A new world/layer is generating — the old layer's world items are
			// gone with the scene; the authoritative table starts empty again
			// (regression guard: this call lived inside the old GameAdapter's
			// OnWorldGenerate and was lost in the domain split).
			domains.Items.ResetItems();
			domains.ItemWorldSync.ResetPending(); // a pending drop's item is gone with the old layer
			domains.BlockBreakSync.ResetPending(); // a pending break's drops are gone with the old layer
		}
	}

	public void OnBlockSet(Vector2Int pos, ushort block) => domains.WorldEventSync.OnBlockSet(pos, block);

	public void OnBlockDamaged(Vector2Int cell, float dmg, bool bonusMetal, float applied) => domains.BlockBreakSync.OnBlockDamaged(cell, dmg, bonusMetal, applied);

	public void OnCustomTileBroken(WorldGeneration world, Vector2Int cell, ushort block) =>
		domains.TileContent.TrySpawnDrops(world, cell, block);

	public void OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound, bool playHitFlash)
	{
		domains.WorldEventSync.OnBuildingEntityDamaged(entity, damage, playHitSound, playHitFlash);
		domains.EnemySync.RecordLocalAttack(entity, damage); // a guest's local drop on a frozen enemy must not flash-revert on the next batch
	}

	public void OnBuildingEntityOpened(BuildingEntity entity) =>
		domains.WorldEventSync.OnBuildingEntityOpened(entity);

	public void OnTrapTriggered(EntityEventKind kind, Vector2 position, byte extra) =>
		domains.EntityEventSync.OnTrapTriggered(kind, position, extra);

	public void OnDynamiteExploded(ulong itemId, Vector2 position) =>
		domains.DynamiteExplosionSync.OnLocalExploded(itemId, position);

	public void OnEntityInstantiated(BuildingEntity entity)
	{
		// Enemy copies freeze before any AI/physics moves them, and the enemy
		// domain's generation-finished edge is what the spawn channel needs: a
		// generation copy's deferred Start must not be reported as a runtime creation.
		var generationAnimal = entity.animal && domains.EnemySync.OnAnimalInstantiated(entity);
		domains.EntitySpawnSync.OnEntityInstantiated(entity, generationAnimal); // the spawn-channel report (runtime creations; creation-time data rides the same message, #128)
	}

	public void OnBuildingEntityDestroyed(BuildingEntity entity) =>
		domains.EntitySpawnSync.OnRuntimeEntityDestroyed(entity); // drops the creation record/pending report (E3: a dead entity is never re-materialized)

	public void DeferLifePodSound() => domains.LifePod.DeferSound();

	public void DeferLifePodShake() => domains.LifePod.DeferShake();

	public bool OnGuestStartAttempt() => domains.GuestMenu.OnGuestStartAttempt();

	public bool HasRestorableWorld() => domains.WorldSaves.HasRestorableWorld;

	public void OnNativeSaveSlot()
	{
		if (!domains.NativeWorldFacts.TryWritePendingRunFields())
		{
			domains.Log.LogInformation("[SaveFacts] the native save slot ran with no live world yet; the restored run fields stay pending for the world-entry seam.");
		}
	}

	public bool OnHostContinueRequested() => domains.Run.OnHostContinueRequested();

	public void OnWorldJoinRequested(bool isTutorial) => domains.Run.OnWorldJoinRequested(isTutorial);

	public void OnSceneLoadBegin()
	{
		domains.ItemWorldSync.SuppressDestroys();
		// A scene change destroys the PlayerCamera the drag lived on: no while-dragging frame will clear the
		// cue ever again, so the load does it. The label is drawn by the plugin, which outlives the scene.
		domains.DragUseCue.Clear();
	}

	/// <summary>
	/// Decide the interception of the game's own "leave the world" action. The
	/// caller (the <c>PlayerCamera.ToMainMenu</c> patch) has already checked
	/// <see cref="HarmonyTraverse.HasLiveWorld"/> — the game's own "in world"
	/// expression — so the world half of the decision is passed in rather than
	/// re-derived here.
	///
	/// The seam's precondition is asked BEFORE anything is recorded: suppressing a
	/// scene load the seam would then drop (it samples one frame later, and the
	/// state can move) would consume the player's action and strand them in a world
	/// they asked to leave. A false answer here means the original load runs,
	/// exactly as it did before the solo trigger existed.
	/// </summary>
	public bool TryDeferMenuReturn(bool hasLiveWorld)
	{
		// This IS the recorded leave being honoured (SaveCutSeam.Leave →
		// PlayerCamera.ToMainMenu): let it through, or the world can never be left.
		var replaying = domains.MenuReturn.IsReplayingLeave;

		// A player's own leave arms the same mid-run cut a /save does: solo play is
		// its own save authority, a guest's answer is MenuOnly (no cut is written).
		var mode = RunMenuReturnPolicy.Decide(domains.Session.Role, inWorld: hasLiveWorld);
		var wouldLeave = RunMenuReturnPolicy.WouldLeaveWorld(mode, hasLiveWorld, cameraAvailable: true);

		if (!MenuExitInterception.ShouldSuppressSceneLoad(hasLiveWorld, replaying, wouldLeave))
		{
			return false;
		}

		domains.MenuReturn.Request(domains.Session.Role, RunMenuReturnOrigin.PlayerLeave, inWorld: true);
		return true;
	}

	/// <summary>
	/// A patch observed a local inventory change and asks for the immediate clone re-report. The order rule
	/// lives here because this is the patch layer's ONE entry point for it: the caller reports the change's
	/// item FACT first (slot move, wear, pickup, craft report), then the snapshot — a peer compares an
	/// arriving snapshot against the facts it already holds, so one that overtakes its event reads as a move
	/// without a report and fires the divergence pair (`20261005-e` read the drop's single line, `20261025-b` one pair per wear/swap); while a DROP report is registered but not yet sent, the snapshot is deferred, and the periodic one converges.
	/// </summary>
	public void OnInventoryChanged()
	{
		if (domains.ItemWorldSync.HasPendingDropReport)
		{
			domains.Log.LogDebug("[CloneRender] inventory change deferred — a drop report is pending (it announces the change on the next frame).");
			return;
		}

		domains.CharacterDataSync.ReportInventoryChanged(domains.Run.LocalBody);
	}

	public bool IsOnlineUiModalOpen => domains.MenuInput.IsOnlineUiModalOpen;

	public bool IsNonModalEscapeSurfaceOpen => domains.MenuInput.IsNonModalEscapeSurfaceOpen;

	public float GetCarriedEncumbrance(Body body) => Carriage.GetCarriedEncumbrance(body);

	public bool IsLocalCarrier(Body body) => Carriage.IsLocalCarrier(body);

	public void OnLocalCarrierBodyUpdated() => Carriage.OnLocalCarrierBodyUpdated();

	public bool EnsureGuestWorldParams() => domains.WorldParams.EnsureGuestApplied();

	public void ResetGenStreamToBaseline()
	{
		domains.WorldParams.ResetGenStreamToBaseline();
		// This runs in the generation coroutine's first step, after the guest's params hold has ended:
		// the descent's settle detection needs "the generation actually started", not "GenerateWorld was
		// called" (the wrapper can hold the call for the host's baseline while the previous world's
		// generatingWorld flag is already down).
		domains.Run.OnLocalGenerationStarted();
	}

	public IEnumerator WrapStructureWorldGen(IEnumerator original, WorldGeneration world) =>
		domains.StructureWorldGen.Wrap(original, world);

	public void OnCustomTileOreGeneration(WorldGeneration world) =>
		domains.TileWorldGen.Distribute(world);

	public void OnCustomLiquidWorldGeneration(WorldGeneration world) =>
		domains.LiquidTileWorldGen.Distribute(world);

	public void OnCustomBuildingWorldGeneration(WorldGeneration world) =>
		domains.BuildingWorldGen.Distribute(world);

	public void OnCustomItemWorldGeneration(WorldGeneration world) =>
		domains.ItemWorldGen.Scatter(world);

	public void OnEarthquakeStarted(float duration, float nextDelay) =>
		domains.WorldEventSync.OnEarthquakeStarted(duration, nextDelay);

	public void OnDarkenSkipped() =>
		domains.Log.LogInformation("[Gate] fade skipped while the gate window holds.");

	public void OnPickupCheckFailed(string itemId, float distance, bool blocked) =>
		domains.Log.LogWarning("[PickupCheck] {Item} refused — distance {Distance:F1}, line-of-sight blocked {Blocked}.", itemId, distance, blocked);

	public void OnDragReleasedToWorld() =>
		domains.Log.LogWarning("[DragFlow] release fell through to the WORLD path (no UI target hit).");

	public bool TryHandleDraggedItemUseOnRemote(Item dragItem, Body localBody) =>
		domains.DragUse.TryUseOnDisplayed(dragItem, localBody);

	public bool TryHandleRemoteMedicalLimbUse(Item dragItem, int limbIndex) =>
		_remoteMedicalOps.TryHandleLimbUse(dragItem, limbIndex);

	public bool TryStartRemoteShrapnelSpecial(Limb limb) =>
		_remoteMedicalOps.TryStartRemoteShrapnelSpecial(limb);

	public bool TryStartRemoteWoundSpecial(Limb limb) =>
		_remoteMedicalOps.TryStartRemoteWoundSpecial(limb);

	public bool TryDivertRemoteInjection(WaterContainerItem container, Limb limb, float amount) =>
		_remoteMedicalOps.TryDivertInjection(container, limb, amount);

	public void ReportRemoteMedicalHeartbeatSuppressed(string clip) =>
		domains.Log.LogDebug(
			"[MedicalView] suppressed native heartbeat {Clip}: the panel's alarm describes the displayed body, not this one.",
			clip);

	public bool TryDivertRemoteTopicalApply(WaterContainerItem container, Limb limb, float amount) =>
		RemoteTopicalUseHandler.TryDivertApplyToLimb(container, limb, amount);

	public float MeasureRemoteTopicalDose(Item dragItem, Limb limb) =>
		LocalUseItemEligibility.FamilyOf(dragItem, ItemUseEntry.Limb, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics)
			== ItemUseFamily.Topical
		&& RemoteTopicalUseHandler.TryMeasure(dragItem, limb, domains.LimbUseSemantics, domains.Log, out var doseMl)
			? doseMl
			: 0f;

	public bool TryDivertRemoteDrink(WaterContainerItem container, float amount) =>
		RemoteDrinkUseHandler.TryDivertDrink(container, amount);

	public float MeasureRemoteDrinkDose(Item dragItem, Body drinker) =>
		LocalUseItemEligibility.FamilyOf(dragItem, ItemUseEntry.Limb, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics)
			== ItemUseFamily.Drink
		&& RemoteDrinkUseHandler.TryMeasure(dragItem, drinker, domains.ConsumeSemantics, domains.Log, out var drinkDoseMl)
			? drinkDoseMl
			: 0f;

	public ulong LocalSteamId => _remoteDragIntents.LocalSteamId;

	public void AutoOpenRemoteBackpackForDrag(Item? dragItem) => domains.DragUse.AutoOpenFor(dragItem);

	public void ReportRemoteDragUnresolved(Item dragItem) => _remoteDragIntents.ReportUnresolved(dragItem);

	public bool ReportLocalReleaseOntoProxy(Item dragItem, ulong targetOwnerSteamId, string target, ulong targetItemInstanceId, int targetSlotIndex) =>
		_remoteDragIntents.ReportLocalReleaseOntoProxy(dragItem, targetOwnerSteamId, target, targetItemInstanceId, targetSlotIndex);

	public void EmitRemoteDragIntents(RemoteDragOutcome outcome) => _remoteDragIntents.Emit(outcome);

	public void EmitRemoteWhileDraggingFrame(RemoteDragOutcome outcome) => _remoteDragIntents.EmitWhileDraggingFrame(outcome);

	public void ReportRemoteGestureNotCarried(string gesture) => _remoteDragIntents.ReportGestureNotCarried(gesture);

	public void ReportProxyNotOperable(Item item, string what) => _remoteDragIntents.ReportProxyNotOperable(item, what);

	public void ReportLocalFavouriteMark(Item item, ulong ownerSteamId, bool marked) =>
		domains.Log.LogInformation("[FavouriteMark] {Action} {Item} in {Owner}'s inventory — the mark is local to this client; the owner's own item is untouched.",
			marked ? "marked" : "unmarked", item.id, ownerSteamId);

	public void OnPickUpResult(string itemId, int slot, string home, Vector2 position) =>
		domains.Log.LogInformation("[PickUpResult] {Item} → {Home} (slot {Slot}) at ({X:F1},{Y:F1}).", itemId, home, slot, position.x, position.y);

	public bool ShouldApplyQuakeBreak(Vector2Int blockPos)
	{
		var world = WorldGeneration.world;
		if (world == null || world.earthquakeTime <= 0f) // Unity object — ==; only quake breaks are gated (environment breaks pass)
		{
			return true;
		}

		// Numbering = SteamId order (globally consistent on every side). A
		// break applies only when it is far (> 60 blocks) from every EARLIER
		// numbered player — their region already covers it (their own breaks
		// run, and the last-numbered player's region has no overlap left).
		// Overlapping players therefore keep the total break rate at solo
		// level ("two players standing together break faster" is fixed);
		// separated players each keep the full solo rate.
		var earlier = domains.Session.Members
			.Where(m => m.SteamId < domains.Session.LocalSteamId)
			.ToList();
		if (earlier.Count == 0)
		{
			return true; // no earlier player — this side owns its region
		}

		foreach (var member in earlier)
		{
			var remote = domains.Entities.GetRemotePlayer(member.SteamId);
			if (remote is null)
			{
				continue;
			}

			var playerBlock = world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y));
			if (Vector2Int.Distance(blockPos, playerBlock) < 60)
			{
				return false; // covered by an earlier player — skip this break
			}
		}

		return true;
	}

	public void OnItemInstantiated(Item item) => domains.ItemWorldSync.OnItemInstantiated(item);

	public void OnBrokenItemUpdate(Item item, string reason) => domains.ItemWorldSync.OnBrokenItemUpdate(item, reason);

	public void OnNonAuthoritativeItemImpactSuppressed(Item item, string source)
	{
		var id = item.GetComponent<ItemInstanceId>()?.Id ?? 0;
		domains.Log.LogDebug("[ItemImpact] suppressed non-authoritative {Source} on {Type} (id {ItemId}) — guest copies are not the world-item physics authority.",
			source, item.id, id);
	}

	public void OnWorldItemImpact(Vector2 position, ItemImpactKind kind, byte soundIndex) =>
		domains.WorldItemImpactSync.Report(position, kind, soundIndex);

	public void OnItemDestroyed(Item item) => domains.ItemWorldSync.OnItemDestroyed(item);

	public void OnItemAppliedToLimb(Item item, Limb limb) => domains.LimbItemTransform.OnItemAppliedToLimb(item, limb);

	public void OnLimbComponentTakeOffBegin(GameObject limbObject, string definitionId) =>
		domains.LimbItemTransform.OnLimbComponentTakeOffBegin(limbObject, definitionId);

	public void OnLimbComponentTakeOffEnd(bool nativeRan) => domains.LimbItemTransform.OnLimbComponentTakeOffEnd(nativeRan);

	public void TryAdoptLimbRemovalItem(Item item) => domains.LimbItemTransform.TryAdoptLimbRemovalItem(item);

	public void OnItemPickupStart(Item item) => domains.PickupSync.OnPickupStart(item);

	public void OnItemPickedUp(Item item) => domains.PickupSync.OnPickedUp(item);

	public void OnItemDropped(Item item) => domains.ItemWorldSync.OnItemDropped(item);

	public void OnItemThrown(Item item) => domains.ItemWorldSync.OnItemThrown(item);

	public void OnItemLoadedIntoContainer(Item item, bool wasWorldItem) =>
		domains.ContainerSync.OnLoadedIntoContainer(item, wasWorldItem);

	public void OnItemUnloadedFromContainer(Item item, bool wasWorldItem) =>
		domains.ContainerSync.OnUnloadedFromContainer(item, wasWorldItem);

	public void OnContainerUnloadedAll(Container container) => domains.ContainerSync.OnUnloadedAll(container);

	public void OnItemUsed(Item item)
	{
		domains.ItemUseSync.OnItemUsed(item);
		domains.CraftingSync.OnItemUsed(item); // a blueprint use unlocks its recipe (the unlock fact — the destruction rides the use digest)
	}

	public void OnGunStateChanged(GunScript gun) => domains.GunStateSync.TryReport(gun);

	public object? OnCraftBegin(Recipe recipe) => domains.CraftingSync.OnCraftBegin(recipe);

	public void OnCraftEnd(object? state) => domains.CraftingSync.OnCraftEnd(state);

	public object? OnCombineBegin(Body body, Item it1, Item it2) => domains.CraftingSync.OnCombineBegin(body, it1, it2);

	public void OnCombineEnd(object? state) => domains.CraftingSync.OnCombineEnd(state);

	public void OnLiquidTransferFinished(WaterContainerItem transferTo, WaterContainerItem transferFrom) =>
		domains.CraftingSync.OnLiquidTransferFinished(transferTo, transferFrom);

	public bool ShouldSuppressDestroy(Item item) =>
		domains.CraftingSync.ShouldSuppressDestroy(item) || domains.HeaterCookSync.ShouldSuppressDestroy(item);

	public void OnSlotMoved(Body body, int slot, string origin) => domains.ItemSlotSync.OnSlotMoved(body, slot, origin);

	public void OnItemWorn(Item item) => domains.ItemSlotSync.OnItemWorn(item);

	public void OnFluidFixedUpdate() => domains.FluidSync.OnFluidFixedUpdate();

	/// <summary>The member's own descent the session owns (its end-of-layer choice or the drill pod's): the local regeneration must not follow (the layer is the host's capture), and the native two-layer flag rides the request.</summary>
	public bool TryDelegateLocalAdvance(bool twice) => domains.LayerAdvance.TryDelegateLocalAdvance(twice);

	/// <summary>
	/// The end-of-layer panel's close, asked right after the game's own panel write
	/// (<see cref="Patches.WorldGenerationPanelClosePatch"/>): the native latch re-opens whatever a later phase closed.
	/// </summary>
	public void CloseTheEndOfLayerPanelIfThisClientMustNotShowIt() => domains.LayerPanel.CloseThePanelThisClientMustNotShow();

	/// <summary>
	/// The end-of-layer panel's "Save and exit" takes CUO's own cut instead of the game's native save (the user's
	/// 2026-10-24 ruling: the native save is meaningless to CUO). The panel's entry is REPLACED, so the native body
	/// must not run at all — it would write `save.sv`, advance the depth without generating and leave the world in one
	/// go, none of which CUO wants. The cut itself is the DEFERRED menu return the game's own leave already uses: the
	/// request is recorded here and `SaveCutSeam` takes the cut at the pump's frame-end seam and only then leaves, so a
	/// cut can never read a half-left world (an inline "arm then leave" was read on the machine: the seam found no
	/// world to cut because the leave had already happened).
	/// </summary>
	public bool TakeWorldCutInsteadOfTheNativeSave()
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return false;
		}

		domains.MenuReturn.Request(domains.Session.Role, RunMenuReturnOrigin.PlayerLeave, inWorld: true);
		domains.Log.LogInformation("[LayerChoice] save-and-exit records the deferred menu return: the seam takes the CUO cut and then leaves, so the native save is never written.");
		return true;
	}

	/// <summary>A local descent that is NOT suppressed: on the host of a live session it is the shared edge every member follows, and the presentation window opens with it.</summary>
	public void OnHostDescent(bool twice)
	{
		domains.Run.OnHostDescentStarted();
		domains.LayerAdvance.OnHostDescent(twice);
	}

	public void UpdateDragUseCue(Item? dragItem) => domains.DragUseCue.Update(dragItem);

	public void OnFluidDrinkReported(Vector2Int pos) => domains.FluidSync.OnDrinkReported(pos);

	public bool TryRenderCustomLiquids(FluidManager manager) =>
		domains.LiquidTileRender.TryRender(manager);

	/// <summary>The item category's data anchor, forwarded from the Runtime's own item data: the static patches read it here, the shared classifiers read it through <c>StandingItems</c>.</summary>
	public bool IsWorldItemRegistered(ulong itemId) => domains.Items.IsWorldItemRegistered(itemId);

	/// <summary>The refusal half of the same port: the pickup gate refused a local gesture because its target is a standing item object.</summary>
	public void ReportStandingItemGestureRefused(Item item, string source) =>
		domains.Log.LogWarning("[StandingItem] {Type} (id {ItemId}) refused by {Source} — a standing item object is another member's carried item and is never reachable by a local gesture.",
			item.id, item.GetComponent<ItemInstanceId>()?.Id ?? 0, source);

	/// <summary>The wear placement port's one report: a read of or gesture on a wearable whose declared limb this body does not carry was refused — the null the game's own wear family dereferences (<c>Body.cs:1493-1494</c>, <c>:1541</c>, <c>:1558</c>, <c>:1575</c>).</summary>
	public void ReportWearPlacementRefused(string itemId, string limbName) =>
		domains.Log.LogWarning("[Wear] refused: item {Item} declares wear limb '{Limb}', which no limb of this body carries — the game's own placement would dereference the missing limb (Body.cs:1493-1494, :1541, :1558, :1575).",
			itemId, limbName);

	public bool TryGetCustomLiquidColor(byte worldByte, out Color color) =>
		domains.LiquidTileContent.TryGetDisplayColor(worldByte, out color);

	public bool TryGetCustomWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type) =>
		domains.LiquidTileContent.TryGetWaterInfo(worldByte, out buoyancy, out drag, out type);

	public bool TryGetCustomLiquidName(byte worldByte, out string name, out string description) =>
		domains.LiquidTileContent.TryGetDisplayName(worldByte, out name, out description);

	public bool TryDrinkCustomLiquid(FluidManager fluid, Vector2Int pos, Body body) =>
		domains.LiquidTileDrink.TryDrink(fluid, pos, body);

	public void ApplyLiquidTileBodyTouch(Body body) =>
		domains.LiquidTileBodyTouch.Apply(body);

	public void OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, ulong itemInstanceId, int itemValue, Item? purchaseItem) =>
		domains.TradeSync.OnTraderActionReported(trader, action, itemId, itemInstanceId, itemValue, purchaseItem);

	public void OnTraderSwing(TraderScript trader) => domains.TraderSwingSync.Report(trader);

	public void OnSpeechReported(Talker talker, string text) => domains.SpeechSync.OnSpeechReported(talker, text);

	public void OnEnemyBite(Limb limb) => domains.EnemySync.ReportEnemyBite(limb);

	public void OnSpiderTargetDecided(SpiderHandler spider) => domains.EnemyCombat.OnSpiderTargetDecided(spider);

	public void OnCrystalEnemyBodyResolved(CrystalEnemy enemy, ref Body body) => domains.EnemyCombat.ResolveCrystalTargetBody(enemy, ref body);

	public object? OnCrystalLungeBegin(CrystalEnemy enemy) => domains.EnemyCombat.OnCrystalLungeBegin(enemy);

	public void OnCrystalLungeEnd(object? state) => domains.EnemyCombat.OnCrystalLungeEnd(state);

	public float? OnEnemyItemCollision(SpiderHandler spider, Collision2D collision)
	{
		var damage = domains.EnemyCombat.OnEnemyItemCollision(spider, collision);
		if (damage is { } healthDamage)
		{
			var entity = spider.GetComponentInParent<BuildingEntity>();
			if (entity != null) // Unity object — ==
			{
				domains.WorldEventSync.OnBuildingEntityDamaged(entity, healthDamage, playHitSound: false);
			}
		}

		return damage;
	}

	public void OnElderHorrorTick(Body body) => domains.EnemyProximity.ReportElderHorrorTick(body);

	public void OnElderHorrorDefeat(Body body) => domains.EnemyProximity.ReportElderHorrorDefeat(body);

	public void OnXalorisSepticTick(Body body) => domains.EnemyProximity.ReportXalorisSepticTick(body);

	public void OnGrabberGrabbed(Body body) => domains.EnemyProximity.ReportGrabberGrabbed(body);

	public void OnArmSwing() => domains.Entities.MarkLocalAttackSwing();

	public void OnAttackAnim(string prefab, Vector2 direction, bool isRight, Vector2 position) =>
		domains.CharacterAttackAnimSync.Report(prefab, direction, isRight, position);

	public void OnCharacterSound(CharacterSoundKind kind, string clip, Vector2 pos, float volume, bool followOwner, bool twoDimensional, float recoilDegrees) =>
		domains.CharacterSoundSync.Report(kind, clip, pos, volume, followOwner, twoDimensional, recoilDegrees);

	public void OnCharacterLandingVisual(byte cloudSize, Vector2 position, float velocityX) =>
		domains.CharacterLandingVisualSync.Report(cloudSize, position, velocityX);

	public void OnCharacterRagdoll(Vector2 position) =>
		domains.CharacterRagdollSync.Report(position);

	public void OnWorldBloodSpawn(Vector2 position, bool ground) =>
		domains.WorldBloodSync.Report(position, ground);

	public void ApplyBodyStatusProjection(Body body)
	{
		if (body == domains.Run.LocalBody) // Unity objects — ==
		{
			domains.StatusProjection.ApplyBody(body);
		}
	}

	public void ApplyLimbStatusProjection(Body body, Limb limb)
	{
		if (body == domains.Run.LocalBody) // Unity objects — ==
		{
			domains.StatusProjection.ApplyLimb(body, limb);
		}
	}
	public void ApplyBodyCirculationPrefix(Body body)
	{
		if (body == domains.Run.LocalBody) // Unity objects — ==
		{
			domains.StatusProjection.ApplyCirculationPrefix(body);
		}
	}

	public void ApplyBodyCirculationPostfix(Body body)
	{
		if (body == domains.Run.LocalBody) // Unity objects — ==
		{
			domains.StatusProjection.ApplyCirculationPostfix(body);
		}
	}

	public void ApplyModMoodles(MoodleManager manager, bool importantRow)
	{
		if (manager == MoodleManager.main) // Unity object — ==
		{
			domains.MoodleProjection.ApplyModMoodles(manager, importantRow);
		}
	}



	public void OnLimbStateEvent(Limb limb) => domains.CharacterDataSync.ReportLimbStateEvent(limb);

	public bool OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool switchSound, bool force) =>
		domains.WorldTimeSync.OnTimeScaleSetRequested(speed, switchSound, force);

	public void OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed, bool switchSound, bool force) =>
		domains.WorldTimeSync.OnLocalTimeScaleChanged(speed, switchSound, force);

	public ulong OnHeaterCookBegin(Item item) => domains.HeaterCookSync.OnCookCandidate(item);

	public void OnHeaterCookCompleted(ulong sourceItemId, Item cookedItem, float sourceCondition, Vector2 sourcePosition) =>
		domains.HeaterCookSync.OnCookCompleted(sourceItemId, cookedItem, sourceCondition, sourcePosition);

	public void OnHeaterCookCaptureFailed(ulong sourceItemId) => domains.HeaterCookSync.OnCaptureFailed(sourceItemId);

	private static ulong GetRemoteProxyItemId(Item item)
	{
		var marker = item.GetComponent<RemoteInventoryItemId>();
		if (marker != null && marker.Id != 0) // Unity object — ==
		{
			return marker.Id;
		}

		// Legacy fallback: extremely old clone renders may still carry a domain
		// item id from before the id-free display boundary.
		return item.GetComponent<ItemInstanceId>()?.Id ?? 0;
	}

	/// <summary>The death receipt's menu button (the ruling's second entrance): a dead player on the receipt enters
	/// the spectator mode instead of the menu load that follows the click.</summary>
	public bool TryEnterFromDeathScreen()
	{
		var camera = PlayerCamera.main;
		if (camera == null || camera.body == null || camera.body.alive || !camera.didDeathScreen) // Unity objects — ==
		{
			return false;
		}

		return domains.Spectator.EnterSpectatorMode();
	}
}
