using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
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
	internal readonly IEntitySyncControl Entities;
	internal readonly ILogger<GameAdapter> Log;

	internal readonly CloneFactTable FactTable;
	internal readonly CharacterDataSync CharacterDataSync;
	internal readonly RemotePlayerRenderer Renderer;
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
	internal readonly WorldEventSync WorldEventSync;
	internal readonly LifePodPresentation LifePod;
	internal readonly RunCoordinator Run;
	internal readonly WorldParamsService WorldParams;
	internal readonly StartGateCoordinator Gate;
	internal readonly GuestMenuGuard GuestMenu;
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
	internal readonly CharacterSoundSync CharacterSoundSync;
	internal readonly CharacterAttackAnimSync CharacterAttackAnimSync;
	internal readonly CharacterLandingVisualSync CharacterLandingVisualSync;
	internal readonly HeaterCookSync HeaterCookSync;
	internal readonly WorldTimeSync WorldTimeSync;

	public GameAdapterDomains(
		ISessionControl session,
		IEntitySyncControl entities,
		ICharacterDataControl characterData,
		IWorldControl world,
		IItemControl items,
		ICraftControl craft,
		ItemArbitration arbitration,
		EnemySyncService enemies,
		IWorldTimeControl worldTime,
		IPlayerInteractionControl playerInteraction,
		ITutorialClawControl tutorialClaw,
		IOptionsMonitor<RespawnOptions> respawnOptions,
		ILogger<GameAdapter> log,
		IMapper mapper,
		ILoggerFactory loggerFactory)
	{
		Session = session;
		Items = items;
		Entities = entities;
		PlayerInteraction = playerInteraction;
		Log = log;
		// Domains (state belongs to its owner; the coordinator forwards, never holds).
		// Construction order follows the dependencies: item domains (guard → world →
		// application → authority/follow, one-way), run → gate (the gate reads the
		// run's phase machine).
		ItemStateCodec.BindLog(log);
		WorldGenRandomIsolation.Log = msg => Log.LogInformation(msg); // generation-stream segment fingerprints (peer log comparison)
		LayerModifierApplyPatch.Log = msg => Log.LogInformation(msg); // layer-modifier decision trace (diagnostic)
		FactTable = new CloneFactTable(loggerFactory.CreateLogger<CloneFactTable>());
		CharacterDataSync = new CharacterDataSync(session, characterData, mapper,
			new CloneInventoryRenderer(loggerFactory.CreateLogger<CloneInventoryRenderer>()),
			FactTable,
			loggerFactory.CreateLogger<CharacterDataSync>());
		Renderer = new RemotePlayerRenderer(session, entities, CharacterDataSync, new CloneLimbRenderer(loggerFactory.CreateLogger<CloneLimbRenderer>()), loggerFactory.CreateLogger<RemotePlayerRenderer>());
		DropGuard = new DropProtectionGuard();
		ItemApplication = new ItemApplication(items, session, loggerFactory.CreateLogger<ItemApplication>());
		ItemReconcile = new ItemReconcile(items, ItemApplication, DropGuard, loggerFactory.CreateLogger<ItemReconcile>());
		OperationTrace = new OperationTrace(loggerFactory.CreateLogger<OperationTrace>());
		var itemReports = new ItemReportCommitter(items, OperationTrace, loggerFactory.CreateLogger<ItemReportCommitter>());
		ItemIds = new ItemIdAllocator(session, items, loggerFactory.CreateLogger<ItemIdAllocator>()); // ids are (counter, SteamId) — the counter reports the high-water mark and resumes from the host's grant on join/reconnect
		var itemDropState = new ItemDropState();
		var blockBreakState = new BlockBreakPendingState();
		ItemWorldSync = new ItemWorldSync(session, items, DropGuard, itemDropState, blockBreakState, OperationTrace, itemReports, ItemIds, loggerFactory.CreateLogger<ItemWorldSync>());
		ItemSlotSync = new ItemSlotSync(items, session, ItemIds, loggerFactory.CreateLogger<ItemSlotSync>());
		PickupSync = new PickupSync(items, session, ItemApplication, itemDropState, ItemIds, OperationTrace, itemReports, ItemSlotSync);
		ContainerSync = new ContainerItemSync(items, itemDropState, ItemIds, OperationTrace, itemReports, session, loggerFactory.CreateLogger<ContainerItemSync>());
		ItemUseSync = new ItemUseSync(items, session, ItemIds, loggerFactory.CreateLogger<ItemUseSync>());
		GunStateSync = new GunStateSync(ItemUseSync, loggerFactory.CreateLogger<GunStateSync>());
		ItemPositionAuthority = new ItemPositionAuthority(items);
		ItemPositionFollow = new ItemPositionFollow(items, DropGuard, session, loggerFactory.CreateLogger<ItemPositionFollow>());
		GenItemAuthority = new GeneratedItemAuthority(session, items, ItemIds, loggerFactory.CreateLogger<GeneratedItemAuthority>());
		GenItemApplication = new GeneratedItemApplication(items, ItemApplication, loggerFactory.CreateLogger<GeneratedItemApplication>());
		TrapLayoutScanner = new TrapLayoutScanner(session, world, loggerFactory.CreateLogger<TrapLayoutScanner>());
		TrapLayoutApplication = new TrapLayoutApplication(world, loggerFactory.CreateLogger<TrapLayoutApplication>());
		LayerModifierSync = new LayerModifierSync(items, loggerFactory.CreateLogger<LayerModifierSync>());
		CarriedInventoryReporter = new CarriedInventoryReporter(session, items, ItemIds, loggerFactory.CreateLogger<CarriedInventoryReporter>());
		LayerModifierApplyPatch.IsModifierAuthority = () => Session.Role != SessionRole.Guest; // the host/solo side rolls the world's modifier; guests replay it locally and fall back to the snapshot
		LayerModifierApplyPatch.ReportLocalDecision = LayerModifierSync.OnLocalDecision; // the guest's local replay — the adapter defers Initialize until the generation finished
		MineScriptPatches.ShouldShieldItems = () => Session.Role == SessionRole.Guest; // a locally simulated item must not trip a mine on the guest side (the trigger checks only !isKinematic)
		BlockBreakSync = new BlockBreakSync(session, world, items, blockBreakState, OperationTrace, loggerFactory.CreateLogger<BlockBreakSync>());
		WorldEventSync = new WorldEventSync(session, world, BlockBreakSync, OperationTrace, loggerFactory.CreateLogger<WorldEventSync>());
		var trapVisualReplay = new TrapVisualReplay(loggerFactory.CreateLogger<TrapVisualReplay>());
		EntityEventSync = new EntityEventSync(world, session,
			new TrapEffectApplier(loggerFactory.CreateLogger<TrapEffectApplier>()),
			trapVisualReplay,
			loggerFactory.CreateLogger<EntityEventSync>());
		DynamiteExplosionSync = new DynamiteExplosionSync(world, session, trapVisualReplay,
			loggerFactory.CreateLogger<DynamiteExplosionSync>());
		EntitySpawnSync = new EntitySpawnSync(world, session, loggerFactory.CreateLogger<EntitySpawnSync>());
		GeyserStateSync = new GeyserStateSync(world, session, loggerFactory.CreateLogger<GeyserStateSync>());
		RadiationLineSync = new RadiationLineSync(world, session, entities, loggerFactory.CreateLogger<RadiationLineSync>());
		FluidSync = new FluidWorldSync(world, session, entities, loggerFactory);
		TradeSync = new TradeStateSync(world, session, new TradeExecutor(), loggerFactory.CreateLogger<TradeStateSync>());
		TraderSwingSync = new TraderSwingSync(world, session, loggerFactory.CreateLogger<TraderSwingSync>());
		TraderRecruit = new TraderRecruitCoordinator(session, world, characterData, CharacterDataSync, respawnOptions, items, ItemIds, loggerFactory.CreateLogger<TraderRecruitCoordinator>());
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
		LifePod = new LifePodPresentation(loggerFactory.CreateLogger<LifePodPresentation>());
		GuestMenu = new GuestMenuGuard(session, loggerFactory.CreateLogger<GuestMenuGuard>());
		MenuInput = new OnlineMenuInputGuard(session, loggerFactory.CreateLogger<OnlineMenuInputGuard>());
		WorldParams = new WorldParamsService(world, loggerFactory.CreateLogger<WorldParamsService>());
		Run = new RunCoordinator(session, world, entities, CharacterDataSync, GuestMenu, WorldParams, arbitration, loggerFactory.CreateLogger<RunCoordinator>());
		Gate = new StartGateCoordinator(session, world, LifePod, Run, loggerFactory.CreateLogger<StartGateCoordinator>());
		WorldTimeSync = new WorldTimeSync(session, entities, characterData, Run, Gate, worldTime, loggerFactory.CreateLogger<WorldTimeSync>());
	}
}
