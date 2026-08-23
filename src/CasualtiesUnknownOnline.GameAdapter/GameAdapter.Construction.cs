using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.GameAdapter.Run;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Construction half of <see cref="GameAdapter"/> (partial split for
/// readability): the adapter's owned state and the constructor dependency
/// wiring. The coordinator file keeps only the lifecycle pump, session wiring
/// and thin IPatchBridge forwards. Every field remains owned by this partial
/// declaration — the domains are still constructed directly in the constructor
/// (readonly fields can only be assigned there), with the existing domain
/// ordering and comments preserved. No factory, no late wiring, no state
/// ownership change.
/// </summary>
public sealed partial class GameAdapter
{
	private readonly SessionService _session;
	private readonly ItemService _items;
	private readonly EntitySyncService _entities;
	private readonly ILogger<GameAdapter> _log;
	private Harmony? _harmony;

	private readonly CloneFactTable _factTable;
	private readonly CharacterDataSync _characterDataSync;
	private readonly RemotePlayerRenderer _renderer;
	private readonly DropProtectionGuard _dropGuard;
	private readonly ItemApplication _itemApplication;
	private readonly ItemReconcile _itemReconcile;
	private readonly ItemWorldSync _itemWorldSync;
	private readonly PickupSync _pickupSync;
	private readonly ContainerItemSync _containerSync;
	private readonly ItemUseSync _itemUseSync;
	private readonly GunStateSync _gunStateSync;
	private readonly ItemSlotSync _itemSlotSync;
	private readonly OperationTrace _operationTrace;
	private readonly ItemPositionAuthority _itemPositionAuthority;
	private readonly ItemPositionFollow _itemPositionFollow;
	private readonly BlockBreakSync _blockBreakSync;
	private readonly WorldEventSync _worldEventSync;
	private readonly LifePodPresentation _lifePod;
	private readonly RunCoordinator _run;
	private readonly WorldParamsService _worldParams;
	private readonly StartGateCoordinator _gate;
	private readonly GuestMenuGuard _guestMenu;
	private readonly GeneratedItemAuthority _genItemAuthority;
	private readonly GeneratedItemApplication _genItemApplication;
	private readonly TrapLayoutScanner _trapLayoutScanner;
	private readonly TrapLayoutApplication _trapLayoutApplication;
	private readonly LayerModifierSync _layerModifierSync;
	private readonly ItemIdAllocator _itemIds;
	private readonly CarriedInventoryReporter _carriedInventoryReporter;
	private readonly EntityEventSync _entityEventSync;
	private readonly DynamiteExplosionSync _dynamiteExplosionSync;
	private readonly EntitySpawnSync _entitySpawnSync;
	private readonly GeyserStateSync _geyserStateSync;
	private readonly RadiationLineSync _radiationLineSync;
	private readonly FluidWorldSync _fluidSync;
	private readonly TradeStateSync _tradeSync;
	private readonly SpeechSync _speechSync;
	private readonly CraftingSync _craftingSync;
	private readonly RecipeUnlockApply _recipeUnlockApply;
	private readonly EnemySyncCoordinator _enemySync;
	private readonly EnemyCombatDirector _enemyCombat;
	private readonly EnemyProximitySync _enemyProximity;
	private readonly TutorialClawSync _tutorialClawSync;
	private readonly PlayerInteractionService _playerInteraction;
	private Body? _lastLocalBody; // Unity object — == (the world-entry edge for the destroy-suppression reset)

	public GameAdapter(SessionService session, EntitySyncService entities, CharacterDataStore characterData,
		WorldService world, ItemService items, ICraftControl craft, ItemArbitration arbitration,
		EnemySyncService enemies, IWorldTimeControl worldTime, PlayerInteractionService playerInteraction,
		ITutorialClawControl tutorialClaw,
		ILogger<GameAdapter> log, IMapper mapper, ILoggerFactory loggerFactory)
	{
		_session = session;
		_items = items;
		_entities = entities;
		_playerInteraction = playerInteraction;
		_log = log;
		// Domains (state belongs to its owner; the coordinator forwards, never holds).
		// Construction order follows the dependencies: item domains (guard → world →
		// application → authority/follow, one-way), run → gate (the gate reads the
		// run's phase machine).
		ItemStateCodec.BindLog(log);
		WorldGenRandomIsolation.Log = msg => _log.LogInformation(msg); // generation-stream segment fingerprints (peer log comparison)
		LayerModifierApplyPatch.Log = msg => _log.LogInformation(msg); // layer-modifier decision trace (diagnostic)
		_factTable = new CloneFactTable(loggerFactory.CreateLogger<CloneFactTable>());
		_characterDataSync = new CharacterDataSync(session, characterData, mapper,
			new CloneInventoryRenderer(loggerFactory.CreateLogger<CloneInventoryRenderer>()),
			_factTable,
			loggerFactory.CreateLogger<CharacterDataSync>());
		_renderer = new RemotePlayerRenderer(session, entities, _characterDataSync, new CloneLimbRenderer(loggerFactory.CreateLogger<CloneLimbRenderer>()), loggerFactory.CreateLogger<RemotePlayerRenderer>());
		_dropGuard = new DropProtectionGuard();
		_itemApplication = new ItemApplication(items, session, loggerFactory.CreateLogger<ItemApplication>());
		_itemReconcile = new ItemReconcile(items, _itemApplication, _dropGuard, loggerFactory.CreateLogger<ItemReconcile>());
		_operationTrace = new OperationTrace(loggerFactory.CreateLogger<OperationTrace>());
		var itemReports = new ItemReportCommitter(items, _operationTrace, loggerFactory.CreateLogger<ItemReportCommitter>());
		_itemIds = new ItemIdAllocator(session, items, loggerFactory.CreateLogger<ItemIdAllocator>()); // ids are (counter, SteamId) — the counter reports the high-water mark and resumes from the host's grant on join/reconnect
		var itemDropState = new ItemDropState();
		var blockBreakState = new BlockBreakPendingState();
		_itemWorldSync = new ItemWorldSync(session, items, _dropGuard, itemDropState, blockBreakState, _operationTrace, itemReports, _itemIds, loggerFactory.CreateLogger<ItemWorldSync>());
		_itemSlotSync = new ItemSlotSync(items, session, _itemIds, loggerFactory.CreateLogger<ItemSlotSync>());
		_pickupSync = new PickupSync(items, session, _itemApplication, itemDropState, _itemIds, _operationTrace, itemReports, _itemSlotSync);
		_containerSync = new ContainerItemSync(items, itemDropState, _itemIds, _operationTrace, itemReports, session, loggerFactory.CreateLogger<ContainerItemSync>());
		_itemUseSync = new ItemUseSync(items, session, _itemIds, loggerFactory.CreateLogger<ItemUseSync>());
		_gunStateSync = new GunStateSync(_itemUseSync, loggerFactory.CreateLogger<GunStateSync>());
		_itemPositionAuthority = new ItemPositionAuthority(items);
		_itemPositionFollow = new ItemPositionFollow(items, _dropGuard, session, loggerFactory.CreateLogger<ItemPositionFollow>());
		_genItemAuthority = new GeneratedItemAuthority(session, items, _itemIds, loggerFactory.CreateLogger<GeneratedItemAuthority>());
		_genItemApplication = new GeneratedItemApplication(items, _itemApplication, loggerFactory.CreateLogger<GeneratedItemApplication>());
		_trapLayoutScanner = new TrapLayoutScanner(session, world, loggerFactory.CreateLogger<TrapLayoutScanner>());
		_trapLayoutApplication = new TrapLayoutApplication(world, loggerFactory.CreateLogger<TrapLayoutApplication>());
		_layerModifierSync = new LayerModifierSync(items, loggerFactory.CreateLogger<LayerModifierSync>());
		_carriedInventoryReporter = new CarriedInventoryReporter(session, items, _itemIds, loggerFactory.CreateLogger<CarriedInventoryReporter>());
		LayerModifierApplyPatch.IsModifierAuthority = () => _session.Role != SessionRole.Guest; // the host/solo side rolls the world's modifier; guests replay it locally and fall back to the snapshot
		LayerModifierApplyPatch.ReportLocalDecision = _layerModifierSync.OnLocalDecision; // the guest's local replay — the adapter defers Initialize until the generation finished
		MineScriptPatches.ShouldShieldItems = () => _session.Role == SessionRole.Guest; // a locally simulated item must not trip a mine on the guest side (the trigger checks only !isKinematic)
		_blockBreakSync = new BlockBreakSync(session, world, items, blockBreakState, _operationTrace, loggerFactory.CreateLogger<BlockBreakSync>());
		_worldEventSync = new WorldEventSync(session, world, _blockBreakSync, _operationTrace, loggerFactory.CreateLogger<WorldEventSync>());
		var trapVisualReplay = new TrapVisualReplay(loggerFactory.CreateLogger<TrapVisualReplay>());
		_entityEventSync = new EntityEventSync(world, session,
			new TrapEffectApplier(loggerFactory.CreateLogger<TrapEffectApplier>()),
			trapVisualReplay,
			loggerFactory.CreateLogger<EntityEventSync>());
		_dynamiteExplosionSync = new DynamiteExplosionSync(world, session, trapVisualReplay,
			loggerFactory.CreateLogger<DynamiteExplosionSync>());
		_entitySpawnSync = new EntitySpawnSync(world, session, loggerFactory.CreateLogger<EntitySpawnSync>());
		_geyserStateSync = new GeyserStateSync(world, session, loggerFactory.CreateLogger<GeyserStateSync>());
		_radiationLineSync = new RadiationLineSync(world, session, loggerFactory.CreateLogger<RadiationLineSync>());
		_fluidSync = new FluidWorldSync(world, session, entities, loggerFactory);
		_tradeSync = new TradeStateSync(world, session, new TradeExecutor(), loggerFactory.CreateLogger<TradeStateSync>());
		_speechSync = new SpeechSync(world, session, loggerFactory.CreateLogger<SpeechSync>());
		_craftingSync = new CraftingSync(craft, _itemIds, itemReports, _operationTrace, loggerFactory.CreateLogger<CraftingSync>());
		_recipeUnlockApply = new RecipeUnlockApply(craft, loggerFactory.CreateLogger<RecipeUnlockApply>());
		_heaterCookSync = new HeaterCookSync(items, _itemIds, itemReports, _operationTrace, loggerFactory.CreateLogger<HeaterCookSync>());
		_enemySync = new EnemySyncCoordinator(session, enemies, mapper, _characterDataSync, loggerFactory.CreateLogger<EnemySyncCoordinator>());
		_enemyCombat = new EnemyCombatDirector(session, entities, enemies, _enemySync, _renderer, loggerFactory.CreateLogger<EnemyCombatDirector>());
		_enemyProximity = new EnemyProximitySync(session, enemies, _characterDataSync, loggerFactory.CreateLogger<EnemyProximitySync>());
		_tutorialClawSync = new TutorialClawSync(tutorialClaw, session, loggerFactory.CreateLogger<TutorialClawSync>());
		_characterSoundSync = new CharacterSoundSync(characterData, session, _renderer, loggerFactory.CreateLogger<CharacterSoundSync>());
		_lifePod = new LifePodPresentation(loggerFactory.CreateLogger<LifePodPresentation>());
		_guestMenu = new GuestMenuGuard(session, loggerFactory.CreateLogger<GuestMenuGuard>());
		_worldParams = new WorldParamsService(world, loggerFactory.CreateLogger<WorldParamsService>());
		_run = new RunCoordinator(session, world, entities, _characterDataSync, _guestMenu, _worldParams, arbitration, loggerFactory.CreateLogger<RunCoordinator>());
		_gate = new StartGateCoordinator(session, world, _lifePod, _run, loggerFactory.CreateLogger<StartGateCoordinator>());
		_worldTimeSync = new WorldTimeSync(session, entities, characterData, _run, _gate, worldTime, loggerFactory.CreateLogger<WorldTimeSync>());
		PatchBridge.Bind(this); // the only static seam — Harmony patches read the narrow surface, never this instance
	}
}
