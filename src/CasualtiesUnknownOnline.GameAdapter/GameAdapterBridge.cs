using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The IPatchBridge implementation. Harmony patches are static and cannot
/// receive constructor injection, so the DI-owned <see cref="GameAdapter"/>
/// binds this object once at construction; the patches read only this narrow
/// surface. Keeping the bridge in its own top-level class removes the entire
/// forwarding half from the adapter coordinator.
/// </summary>
internal sealed class GameAdapterBridge(GameAdapterDomains domains) : IPatchBridge
{
	public bool IsWorldGenIsolated => true;

	public bool IsWaitingForReady => domains.Gate.WaitingForReady;

	public bool IsOnlineUiModalOpen => domains.MenuInput.IsModal;

	public bool IsInGateWindow => domains.Run.IsInGateWindow;

	public bool IsSessionActive => domains.Session.SessionActive;

	public bool IsHostMode => domains.Session.Role == SessionRole.Host && domains.Session.SessionActive;

	public bool IsReplayingLifePodSound => domains.LifePod.IsReplayingSound;

	public bool IsHeaterCookAuthority =>
		domains.Session.Role != SessionRole.Guest || !domains.Session.SessionActive;

	public bool TryDeferStartGateAlert(string text, bool important) => domains.Gate.DeferAlert(text, important);

	public void OnWorldGenerate()
	{
		domains.Run.OnWorldGenerate();
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

	public void OnBlockDamaged(Vector2 pos, float dmg, bool bonusMetal) => domains.BlockBreakSync.OnBlockDamaged(pos, dmg, bonusMetal);

	public void OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound)
	{
		domains.WorldEventSync.OnBuildingEntityDamaged(entity, damage, playHitSound);
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
		// Enemy copies freeze at their spawn position before any AI/physics
		// moves them; the generic spawn channel then reports the creation.
		if (entity.animal)
		{
			domains.EnemySync.OnAnimalInstantiated(entity);
		}

		domains.EntitySpawnSync.OnEntityInstantiated(entity); // the spawn-channel report (runtime creations; creation-time data rides the same message, #128)
	}

	public void DeferLifePodSound() => domains.LifePod.DeferSound();

	public void DeferLifePodShake() => domains.LifePod.DeferShake();

	public bool OnGuestStartAttempt() => domains.GuestMenu.OnGuestStartAttempt();

	public void OnWorldJoinRequested(bool isTutorial) => domains.Run.OnWorldJoinRequested(isTutorial);

	public void OnSceneLoadBegin() => domains.ItemWorldSync.SuppressDestroys();

	public void OnInventoryChanged() => domains.CharacterDataSync.ReportInventoryChanged(domains.Run.LocalBody);

	public float GetCarriedEncumbrance(Body body)
	{
		var local = domains.Run.LocalBody;
		if (local == null || local != body) // Unity objects — ==
		{
			return 0f;
		}

		if (!domains.PlayerInteraction.TryGetCarried(domains.Session.LocalSteamId, out var carried))
		{
			return 0f;
		}

		if (!domains.CharacterDataSync.CloneData.TryGetValue(carried, out var data))
		{
			domains.Log.LogDebug("[CarryWeight] no character snapshot for carried {Carried} — no weight added.", carried);
			return 0f;
		}

		var full = CarriedEncumbranceCalculator.ComputeFullEncumbrance(data);
		var contribution = CarriedEncumbranceCalculator.ApplyMultiplier(full, domains.HostRules.PiggybackWeightMultiplier);
		domains.Log.LogDebug("[CarryWeight] carrier {Carrier} gains {Contribution:F2} from {Carried} (full {Full:F2}).",
			domains.Session.LocalSteamId, contribution, carried, full);
		return contribution;
	}

	public bool EnsureGuestWorldParams() => domains.WorldParams.EnsureGuestApplied();

	public void ResetGenStreamToBaseline() => domains.WorldParams.ResetGenStreamToBaseline();

	public void OnEarthquakeStarted(float duration, float nextDelay) =>
		domains.WorldEventSync.OnEarthquakeStarted(duration, nextDelay);

	public void OnDarkenSkipped() =>
		domains.Log.LogInformation("[Gate] fade skipped while the gate window holds.");

	public void OnPickupCheckFailed(string itemId, float distance, bool blocked) =>
		domains.Log.LogWarning("[PickupCheck] {Item} refused — distance {Distance:F1}, line-of-sight blocked {Blocked}.", itemId, distance, blocked);

	public void OnDragReleasedToWorld() =>
		domains.Log.LogWarning("[DragFlow] release fell through to the WORLD path (no UI target hit).");

	public bool TryHandleDraggedItemUseOnRemote(Item dragItem, Body localBody) =>
		domains.DragUse.TryHandleRelease(dragItem, localBody);

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

	public void OnItemDestroyed(Item item) => domains.ItemWorldSync.OnItemDestroyed(item);

	public void OnItemPickupStart(Item item) => domains.PickupSync.OnPickupStart(item);

	public void OnItemPickedUp(Item item) => domains.PickupSync.OnPickedUp(item);

	public void OnItemDropped(Item item) => domains.ItemWorldSync.OnItemDropped(item);

	public void OnItemThrown(Item item) => domains.ItemWorldSync.OnItemThrown(item);

	public void OnItemLoadedIntoContainer(Item item, bool wasWorldItem) =>
		domains.ContainerSync.OnLoadedIntoContainer(item, wasWorldItem);

	public void OnItemUnloadedFromContainer(Item item) => domains.ContainerSync.OnUnloadedFromContainer(item);

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

	public void OnFluidDrinkReported(Vector2Int pos) => domains.FluidSync.OnDrinkReported(pos);

	public void OnTraderActionReported(TraderScript trader, TraderActionKind action, string itemId, int itemValue, Item? purchaseItem) =>
		domains.TradeSync.OnTraderActionReported(trader, action, itemId, itemValue, purchaseItem);

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


	public void OnLimbStateEvent(Limb limb) => domains.CharacterDataSync.ReportLimbStateEvent(limb);

	public bool OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool force) =>
		domains.WorldTimeSync.OnTimeScaleSetRequested(speed, force);

	public void OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed) =>
		domains.WorldTimeSync.OnLocalTimeScaleChanged(speed);

	public ulong OnHeaterCookBegin(Item item) => domains.HeaterCookSync.OnCookCandidate(item);

	public void OnHeaterCookCompleted(ulong sourceItemId, Item cookedItem, float sourceCondition, Vector2 sourcePosition) =>
		domains.HeaterCookSync.OnCookCompleted(sourceItemId, cookedItem, sourceCondition, sourcePosition);

	public void OnHeaterCookCaptureFailed(ulong sourceItemId) => domains.HeaterCookSync.OnCaptureFailed(sourceItemId);
}
