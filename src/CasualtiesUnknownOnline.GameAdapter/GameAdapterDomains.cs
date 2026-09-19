using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.ModStatus;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The Game Adapter's owned domain set. This type is a composition container
/// for the deep sync modules; it holds the fields and constructor wiring that
/// used to live in the <c>GameAdapter.Construction.cs</c> partial so the
/// adapter facade, the patch bridge and the interaction applier can share one
/// state-owner without one god class. It is internal owned state, not a DI
/// service.
/// </summary>
internal sealed class GameAdapterDomains
{
	internal readonly ISessionControl Session;
	internal readonly IItemControl Items;
	internal readonly GameAdapterItemContentProvider ItemContent;
	internal readonly GameAdapterBuildingContentProvider BuildingContent;
	internal readonly GameAdapterTileContentProvider TileContent;
	internal readonly GameAdapterLiquidTileContentProvider LiquidTileContent;
	internal readonly GameAdapterStructureContentProvider StructureContent;
	internal readonly StructureWorldGenDistribution StructureWorldGen;
	internal readonly TileWorldGenDistribution TileWorldGen;
	internal readonly LiquidTileWorldGenDistribution LiquidTileWorldGen;
	internal readonly ItemWorldGenDistribution ItemWorldGen;
	internal readonly BuildingWorldGenDistribution BuildingWorldGen;
	internal readonly LiquidTileRender LiquidTileRender;
	internal readonly LiquidTileBodyTouch LiquidTileBodyTouch;
	internal readonly LiquidTileDrink LiquidTileDrink;
	internal readonly LiquidTilePlacement LiquidTilePlacement;
	internal readonly ModStatusVanillaProjection StatusProjection;
	internal readonly ModStatusMoodleProjection MoodleProjection;
	internal readonly IEntitySyncControl Entities;
	internal readonly IHostRules HostRules;
	internal readonly IPlayerInteractionVisibility InteractionVisibility;
	internal readonly ILogger<GameAdapter> Log;

	internal readonly CloneFactTable FactTable;
	internal readonly CharacterDataSync CharacterDataSync;
	internal readonly StartingSupplyCoordinator StartingSupplies;
	internal readonly RemotePlayerRenderer Renderer;
	internal readonly RemoteBackpackCoordinator RemoteBackpack;
	internal readonly RemoteMedicalCoordinator RemoteMedical;
	internal readonly DropProtectionGuard DropGuard;
	internal readonly ItemApplication ItemApplication;
	internal readonly ItemReconcile ItemReconcile;
	internal readonly ItemWorldSync ItemWorldSync;
	internal readonly PickupSync PickupSync;
	internal readonly ContainerItemSync ContainerSync;
	internal readonly ItemUseSync ItemUseSync;
	internal readonly GunStateSync GunStateSync;
	internal readonly ItemSlotSync ItemSlotSync;
	internal readonly OperationTrace OperationTrace;
	internal readonly ItemPositionAuthority ItemPositionAuthority;
	internal readonly ItemPositionFollow ItemPositionFollow;
	internal readonly BlockBreakSync BlockBreakSync;
	internal readonly NativeWorldFacts NativeWorldFacts;
	internal readonly RestoredWorldFactReplay RestoredWorldFactReplay;
	internal readonly TrapDropPendingState TrapDrops;
	internal readonly WorldEventSync WorldEventSync;
	internal readonly RunClockFactsSync RunClockFacts;
	internal readonly LifePodPresentation LifePod;
	internal readonly RunCoordinator Run;
	internal readonly SaveCutSeam SaveCutSeam;
	internal readonly WorldParamsService WorldParams;
	internal readonly StartGateCoordinator Gate;
	internal readonly GuestMenuGuard GuestMenu;
	internal readonly RunSettingsRangeService RunSettingsRange;
	internal readonly OnlineMenuInputGuard MenuInput;
	internal readonly GeneratedItemAuthority GenItemAuthority;
	internal readonly GeneratedItemApplication GenItemApplication;
	internal readonly TrapLayoutScanner TrapLayoutScanner;
	internal readonly TrapLayoutApplication TrapLayoutApplication;
	internal readonly LayerModifierSync LayerModifierSync;
	internal readonly ItemIdAllocator ItemIds;
	internal readonly CarriedInventoryReporter CarriedInventoryReporter;
	internal readonly EntityEventSync EntityEventSync;
	internal readonly DynamiteExplosionSync DynamiteExplosionSync;
	internal readonly EntitySpawnSync EntitySpawnSync;
	internal readonly GeyserStateSync GeyserStateSync;
	internal readonly RadiationLineSync RadiationLineSync;
	internal readonly FluidWorldSync FluidSync;
	internal readonly TradeStateSync TradeSync;
	internal readonly TraderSwingSync TraderSwingSync;
	internal readonly TraderRecruitCoordinator TraderRecruit;
	internal readonly RespawnCoordinator Respawn;
	internal readonly SpeechSync SpeechSync;
	internal readonly CraftingSync CraftingSync;
	internal readonly RecipeUnlockApply RecipeUnlockApply;
	internal readonly EnemySyncCoordinator EnemySync;
	internal readonly EnemyCombatDirector EnemyCombat;
	internal readonly EnemyProximitySync EnemyProximity;
	internal readonly TutorialClawSync TutorialClawSync;
	internal readonly IPlayerInteractionControl PlayerInteraction;
	internal readonly CrossPlayerDragUse DragUse;

	/// <summary>The deferred menu-return record — read by the scene-load interception (<c>PlayerCamera.ToMainMenu</c>) and by the bridge's deferral decision.</summary>
	internal readonly RunMenuReturnCoordinator MenuReturn;

	/// <summary>The CUO world archive's control surface — the host's save authority (decision 164).</summary>
	internal readonly IWorldSaveControl WorldSaves;
	internal readonly CharacterSoundSync CharacterSoundSync;
	internal readonly CharacterAttackAnimSync CharacterAttackAnimSync;
	internal readonly CharacterLandingVisualSync CharacterLandingVisualSync;
	internal readonly CharacterRagdollSync CharacterRagdollSync;
	internal readonly WorldBloodSync WorldBloodSync;
	internal readonly HeaterCookSync HeaterCookSync;
	internal readonly WorldTimeSync WorldTimeSync;

	public GameAdapterDomains(
		ISessionControl session,
		AdaptiveStreamRateService adaptiveRates,
		IEntitySyncControl entities,
		ICharacterDataControl characterData,
		IWorldControl world,
		IWorldFactSource worldFacts,
		NativeWorldFacts nativeWorldFacts,
		IItemControl items,
		ICraftControl craft,
		ItemArbitration arbitration,
		EnemySyncService enemies,
		IWorldTimeControl worldTime,
		IPlayerInteractionControl playerInteraction,
		ITutorialClawControl tutorialClaw,
		IWorldSaveControl worldSaves,
		WorldRestoreAudit restoreAudit,
		IStartingSupplyPublisher startingSupplies,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		IHostRules hostRules,
		WorldEntityKernelProjection worldEntityKernel,
		WorldEntryFanout worldBackfill,
		ILogger<GameAdapter> log,
		IMapper mapper,
		ILoggerFactory loggerFactory,
		GameAdapterItemContentProvider itemContent,
		GameAdapterBuildingContentProvider buildingContent,
		GameAdapterTileContentProvider tileContent,
		GameAdapterLiquidTileContentProvider liquidTileContent,
		GameAdapterStructureContentProvider structureContent,
		GameAdapterStatusContentProvider statusContent,
		GameAdapterMoodleContentProvider moodleContent,
		ModStatusStore modStatusStore,
		ModStatusProjectionReadModel modStatusProjectionReadModel)
	{
		Session = session;
		Items = items;
		ItemContent = itemContent;
		BuildingContent = buildingContent;
		TileContent = tileContent;
		LiquidTileContent = liquidTileContent;
		StructureContent = structureContent;
		StructureWorldGen = new StructureWorldGenDistribution(
			structureContent, tileContent, loggerFactory.CreateLogger<StructureWorldGenDistribution>());
		TileWorldGen = new TileWorldGenDistribution(
			tileContent, loggerFactory.CreateLogger<TileWorldGenDistribution>());
		LiquidTileWorldGen = new LiquidTileWorldGenDistribution(
			liquidTileContent, loggerFactory.CreateLogger<LiquidTileWorldGenDistribution>());
		ItemWorldGen = new ItemWorldGenDistribution(
			itemContent, loggerFactory.CreateLogger<ItemWorldGenDistribution>());
		BuildingWorldGen = new BuildingWorldGenDistribution(
			buildingContent, loggerFactory.CreateLogger<BuildingWorldGenDistribution>());
		LiquidTileRender = new LiquidTileRender(
			liquidTileContent, loggerFactory.CreateLogger<LiquidTileRender>());
		LiquidTileBodyTouch = new LiquidTileBodyTouch(
			liquidTileContent, loggerFactory.CreateLogger<LiquidTileBodyTouch>());
		LiquidTileDrink = new LiquidTileDrink(
			liquidTileContent, loggerFactory.CreateLogger<LiquidTileDrink>());
		LiquidTilePlacement = new LiquidTilePlacement(
			liquidTileContent, session, loggerFactory.CreateLogger<LiquidTilePlacement>());
		StatusProjection = new ModStatusVanillaProjection(
			modStatusStore, modStatusProjectionReadModel, loggerFactory.CreateLogger<ModStatusVanillaProjection>());
		MoodleProjection = new ModStatusMoodleProjection(
			modStatusStore, modStatusProjectionReadModel, session, statusContent, moodleContent,
			loggerFactory.CreateLogger<ModStatusMoodleProjection>());
		Entities = entities;
		HostRules = hostRules;
		PlayerInteraction = playerInteraction;
		WorldSaves = worldSaves;
		InteractionVisibility = new PlayerInteractionVisibility(session, entities, loggerFactory.CreateLogger<PlayerInteractionVisibility>());
		Log = log;
		// Domains (state belongs to its owner; the coordinator forwards, never holds).
		// Construction order follows the dependencies: item domains (guard → world →
		// application → authority/follow, one-way), run → gate (the gate reads the
		// run's phase machine).
		ItemStateCodec.BindLog(log);
		WorldGenRandomIsolation.Log = msg => Log.LogInformation(msg); // generation-stream segment fingerprints (peer log comparison)
		LayerModifierApplyPatch.Log = msg => Log.LogInformation(msg); // layer-modifier decision trace (diagnostic)
		FactTable = new CloneFactTable(loggerFactory.CreateLogger<CloneFactTable>());
		// The restore's write half and the wearable write are constructed FIRST and
		// injected: the coordinator owns when a restore runs, they own what it does.
		var wearables = new WearableRestorer(loggerFactory.CreateLogger<WearableRestorer>());
		var nativeCharacterSystem = CharacterNativeFields.LiveSystem.Instance; // the two game statics (PlayerCamera.main, WoundView.view), behind the port the capture/apply logic is tested through
																			   // One queue, two readers: the character domain owns when a restore is applied, and
																			   // the starting-supplies decision reads the same instance to answer "does this world
																			   // have a character for me?" (S4.3).
		var localRestore = new LocalCharacterRestoreQueue();
		var restoreApplier = new CharacterRestoreApplier(mapper, wearables, nativeCharacterSystem, loggerFactory.CreateLogger<CharacterRestoreApplier>());
		CharacterDataSync = new CharacterDataSync(session, characterData, mapper,
			new CloneInventoryRenderer(loggerFactory.CreateLogger<CloneInventoryRenderer>()),
			FactTable,
			restoreApplier,
			wearables,
			nativeCharacterSystem,
			localRestore,
			loggerFactory.CreateLogger<CharacterDataSync>());
		// The starting-supplies grant (S4.3): a player the world has no character for gets
		// the run's startingsupplies once per body. The shared restore queue above is its
		// input — a queued character restore means this player is NOT new.
		StartingSupplies = new StartingSupplyCoordinator(
			session,
			world,
			localRestore,
			startingSupplies,
			new GameStartingSupplyTarget(),
			loggerFactory.CreateLogger<StartingSupplyCoordinator>());
		Renderer = new RemotePlayerRenderer(session, entities, CharacterDataSync, new CloneLimbRenderer(loggerFactory.CreateLogger<CloneLimbRenderer>()), playerInteraction, loggerFactory.CreateLogger<RemotePlayerRenderer>());
		RemoteBackpack = new RemoteBackpackCoordinator(session, Renderer, InteractionVisibility, loggerFactory.CreateLogger<RemoteBackpackCoordinator>());
		RemoteMedical = new RemoteMedicalCoordinator(session, CharacterDataSync, mapper, loggerFactory.CreateLogger<RemoteMedicalCoordinator>());
		DropGuard = new DropProtectionGuard(world.IsBreakDropPending);
		ItemApplication = new ItemApplication(items, world, session, loggerFactory.CreateLogger<ItemApplication>());
		ItemReconcile = new ItemReconcile(items, ItemApplication, DropGuard, loggerFactory.CreateLogger<ItemReconcile>());
		OperationTrace = new OperationTrace(loggerFactory.CreateLogger<OperationTrace>());
		var itemReports = new ItemReportCommitter(items, OperationTrace, loggerFactory.CreateLogger<ItemReportCommitter>());
		ItemIds = new ItemIdAllocator(session, items, loggerFactory.CreateLogger<ItemIdAllocator>()); // ids are (counter, SteamId) — the counter reports the high-water mark and resumes from the host's grant on join/reconnect
		var itemDropState = new ItemDropState();
		var blockBreakState = new BlockBreakPendingState();
		TrapDrops = new TrapDropPendingState();
		ItemWorldSync = new ItemWorldSync(session, items, DropGuard, itemDropState, blockBreakState, TrapDrops, OperationTrace, itemReports, ItemIds, loggerFactory.CreateLogger<ItemWorldSync>());
		ItemSlotSync = new ItemSlotSync(items, session, ItemIds, loggerFactory.CreateLogger<ItemSlotSync>());
		PickupSync = new PickupSync(items, session, ItemApplication, itemDropState, ItemIds, OperationTrace, itemReports, ItemSlotSync);
		ContainerSync = new ContainerItemSync(items, itemDropState, ItemIds, OperationTrace, itemReports, session, loggerFactory.CreateLogger<ContainerItemSync>());
		ItemUseSync = new ItemUseSync(items, session, ItemIds, loggerFactory.CreateLogger<ItemUseSync>());
		GunStateSync = new GunStateSync(ItemUseSync, loggerFactory.CreateLogger<GunStateSync>());
		ItemPositionAuthority = new ItemPositionAuthority(items, session, adaptiveRates);
		ItemPositionFollow = new ItemPositionFollow(items, DropGuard, session, loggerFactory.CreateLogger<ItemPositionFollow>());
		// One bind/materialize/drop algorithm, two authorities: the guest applies the
		// host's generation snapshot with it, the host applies a restored cut's item set
		// to the layer it just regenerated (GeneratedItemAuthority).
		var itemReconcile = new GeneratedItemReconcile(ItemApplication, loggerFactory.CreateLogger<GeneratedItemReconcile>());
		GenItemAuthority = new GeneratedItemAuthority(session, items, ItemIds, itemReconcile, loggerFactory.CreateLogger<GeneratedItemAuthority>());
		GenItemApplication = new GeneratedItemApplication(items, itemReconcile, loggerFactory.CreateLogger<GeneratedItemApplication>());
		TrapLayoutScanner = new TrapLayoutScanner(session, world, loggerFactory.CreateLogger<TrapLayoutScanner>());
		TrapLayoutApplication = new TrapLayoutApplication(world, loggerFactory.CreateLogger<TrapLayoutApplication>());
		LayerModifierSync = new LayerModifierSync(items, loggerFactory.CreateLogger<LayerModifierSync>());
		CarriedInventoryReporter = new CarriedInventoryReporter(items, ItemIds);
		LayerModifierApplyPatch.IsModifierAuthority = () => Session.Role != SessionRole.Guest; // the host/solo side rolls the world's modifier; guests replay it locally and fall back to the snapshot
		LayerModifierApplyPatch.ReportLocalDecision = LayerModifierSync.OnLocalDecision; // the guest's local replay — the adapter defers Initialize until the generation finished
		MineScriptPatches.ShouldShieldItems = () => Session.Role == SessionRole.Guest; // a locally simulated item must not trip a mine on the guest side (the trigger checks only !isKinematic)
		var buildingEntities = new WorldBuildingEntitySync(session, world, OperationTrace, loggerFactory.CreateLogger<WorldEventSync>());
		BlockBreakSync = new BlockBreakSync(session, world, items, blockBreakState, buildingEntities, OperationTrace, loggerFactory.CreateLogger<BlockBreakSync>());
		// The restored-world replay is wired BEFORE the world-event domain: the
		// world-entry seam it hangs off is WorldEventSync's baseline capture, and
		// the values it writes come from the Runtime's world-fact tables plus the
		// adapter's own INativeWorldFacts handover. The replay itself is the
		// Runtime's (it owns the order, the accounting and the pending handover);
		// this adapter only supplies the game-typed sink.
		NativeWorldFacts = nativeWorldFacts;
		// The trap replay is built FIRST because both sides of the world-entity
		// domain share it: the live relay (EntityEventSync) and the restored cut's
		// world-entry write (GameRestoredWorldFactSink) must replay a trap fact the
		// same way, or a restore would present facts the live path never would.
		var trapVisualReplay = new TrapVisualReplay(loggerFactory.CreateLogger<TrapVisualReplay>());
		EntityEventSync = new EntityEventSync(world, session,
			new TrapEffectApplier(loggerFactory.CreateLogger<TrapEffectApplier>()),
			trapVisualReplay,
			worldEntityKernel,
			TrapDrops,
			ItemApplication,
			loggerFactory.CreateLogger<EntityEventSync>());
		var restoredWorldFactSink = new GameRestoredWorldFactSink(
			BlockBreakSync,
			buildingEntities,
			EntityEventSync,
			loggerFactory.CreateLogger<GameRestoredWorldFactSink>());
		RestoredWorldFactReplay = new RestoredWorldFactReplay(
			worldFacts, nativeWorldFacts, restoredWorldFactSink, loggerFactory.CreateLogger<RestoredWorldFactReplay>(), restoreAudit, worldEntityKernel, items);
		WorldEventSync = new WorldEventSync(session, world, BlockBreakSync, RestoredWorldFactReplay, OperationTrace, worldEntityKernel, worldBackfill, loggerFactory.CreateLogger<WorldEventSync>());
		DynamiteExplosionSync = new DynamiteExplosionSync(world, session, trapVisualReplay,
			loggerFactory.CreateLogger<DynamiteExplosionSync>());
		EntitySpawnSync = new EntitySpawnSync(world, session, loggerFactory.CreateLogger<EntitySpawnSync>());
		GeyserStateSync = new GeyserStateSync(world, session, loggerFactory.CreateLogger<GeyserStateSync>());
		RadiationLineSync = new RadiationLineSync(world, session, entities, loggerFactory.CreateLogger<RadiationLineSync>());
		// The run clock base and the layer timer are this process's own statics: the domain
		// that owns sending them to members and taking a member's own is one small class.
		RunClockFacts = new RunClockFactsSync(world, NativeWorldFacts, loggerFactory.CreateLogger<RunClockFactsSync>());
		FluidSync = new FluidWorldSync(world, session, entities, adaptiveRates, loggerFactory);
		TradeSync = new TradeStateSync(world, session, new TradeExecutor(), adaptiveRates, loggerFactory.CreateLogger<TradeStateSync>());
		TraderSwingSync = new TraderSwingSync(world, session, loggerFactory.CreateLogger<TraderSwingSync>());
		TraderRecruit = new TraderRecruitCoordinator(session, world, characterData, CharacterDataSync, respawnOptions, items, ItemIds, InteractionVisibility, loggerFactory.CreateLogger<TraderRecruitCoordinator>());
		Respawn = new RespawnCoordinator(session, world, characterData, CharacterDataSync, respawnOptions, loggerFactory.CreateLogger<RespawnCoordinator>());
		SpeechSync = new SpeechSync(world, session, loggerFactory.CreateLogger<SpeechSync>());
		CraftingSync = new CraftingSync(craft, ItemIds, itemReports, OperationTrace, loggerFactory.CreateLogger<CraftingSync>());
		RecipeUnlockApply = new RecipeUnlockApply(craft, loggerFactory.CreateLogger<RecipeUnlockApply>());
		HeaterCookSync = new HeaterCookSync(items, ItemIds, itemReports, OperationTrace, loggerFactory.CreateLogger<HeaterCookSync>());
		EnemySync = new EnemySyncCoordinator(session, enemies, mapper, CharacterDataSync, loggerFactory.CreateLogger<EnemySyncCoordinator>());
		EnemyCombat = new EnemyCombatDirector(session, entities, enemies, EnemySync, Renderer, loggerFactory.CreateLogger<EnemyCombatDirector>());
		EnemyProximity = new EnemyProximitySync(session, enemies, CharacterDataSync, loggerFactory.CreateLogger<EnemyProximitySync>());
		TutorialClawSync = new TutorialClawSync(tutorialClaw, session, loggerFactory.CreateLogger<TutorialClawSync>());
		CharacterSoundSync = new CharacterSoundSync(characterData, session, Renderer, loggerFactory.CreateLogger<CharacterSoundSync>());
		CharacterAttackAnimSync = new CharacterAttackAnimSync(characterData, session, Renderer, loggerFactory.CreateLogger<CharacterAttackAnimSync>());
		CharacterLandingVisualSync = new CharacterLandingVisualSync(characterData, session, Renderer, loggerFactory.CreateLogger<CharacterLandingVisualSync>());
		CharacterRagdollSync = new CharacterRagdollSync(characterData, session, Renderer, loggerFactory.CreateLogger<CharacterRagdollSync>());
		WorldBloodSync = new WorldBloodSync(world, session, loggerFactory.CreateLogger<WorldBloodSync>());
		LifePod = new LifePodPresentation(loggerFactory.CreateLogger<LifePodPresentation>());
		GuestMenu = new GuestMenuGuard(session, loggerFactory.CreateLogger<GuestMenuGuard>());
		RunSettingsRange = new RunSettingsRangeService(session, hostRules, loggerFactory.CreateLogger<RunSettingsRangeService>());
		MenuInput = new OnlineMenuInputGuard(session, loggerFactory.CreateLogger<OnlineMenuInputGuard>());
		WorldParams = new WorldParamsService(world, NativeWorldFacts, RunClockFacts, loggerFactory.CreateLogger<WorldParamsService>());
		MenuReturn = new RunMenuReturnCoordinator(loggerFactory.CreateLogger<RunMenuReturnCoordinator>());
		Run = new RunCoordinator(session, world, entities, CharacterDataSync, GuestMenu, WorldParams, arbitration, playerInteraction, worldSaves, items, restoreAudit, StartingSupplies, MenuReturn, loggerFactory.CreateLogger<RunCoordinator>());
		// The frame-end cut seam is the LAST domain of the pump: it is where every
		// cut trigger is taken (the armed /save request and the deliberate menu
		// return of a host or a solo player), after every domain has finished this
		// frame's work.
		SaveCutSeam = new SaveCutSeam(
			session,
			worldSaves,
			Run,
			MenuReturn,
			blockBreakState,
			TrapDrops,
			itemDropState,
			loggerFactory.CreateLogger<SaveCutSeam>());
		Gate = new StartGateCoordinator(session, world, LifePod, Run, loggerFactory.CreateLogger<StartGateCoordinator>());
		WorldTimeSync = new WorldTimeSync(session, entities, characterData, Run, Gate, worldTime, loggerFactory.CreateLogger<WorldTimeSync>());
		DragUse = new CrossPlayerDragUse(this);
	}
}
