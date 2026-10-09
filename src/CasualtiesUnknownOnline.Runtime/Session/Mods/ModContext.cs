using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's framework surface. The session is a snapshot at bind time (the
/// host never fires SessionActivated and pre-discovery events are lost — the
/// snapshot is the only reliable "current state"); the events are the
/// increments after that. Network routes through the mod's own id.
///
/// Every per-mod adapter is a type of its own (<see cref="ModNetworkAdapter"/>,
/// <see cref="ModPacketsAdapter"/>, <see cref="ModContentAdapter"/>, …): this
/// class is the wiring point that gives each of them the mod's manifest and
/// hands them to the mod, not the place their logic lives.
/// </summary>
internal sealed class ModContext(
	ModManifest manifest,
	ILogger logger,
	ILogger frameworkLog,
	ISessionInfo session,
	SessionService sessionService,
	ModChannel channel,
	ModStateStore stateStore,
	ModDataStore dataStore,
	ModStatusStore statusStore,
	ModBuildingRuntimeStore buildingRuntime,
	ModCommandService commands,
	ConsoleCommandRegistry consoleCommands,
	RemoteVitalsService remoteVitals,
	RemoteInventoryService remoteInventory,
	IModEntitySpawner entitySpawner,
	IModItemSpawner itemSpawner,
	IModTilePlacer tilePlacer,
	IModStructurePlacer structurePlacer,
	IModLiquidPlacer liquidPlacer,
	IModNativeApiProvider nativeApiProvider,
	IModContentControl contentControl,
	ModResourceCompletionStore resourceCompletionStore) : IModContext
{
	private readonly ModManifest _manifest = manifest;
	private readonly SessionService _sessionService = sessionService;
	private readonly ILogger _frameworkLog = frameworkLog;
	private readonly ModNetworkAdapter _network = new(channel, manifest, frameworkLog);
	private readonly ModPacketsAdapter _packets = new(channel, manifest, sessionService, frameworkLog);
	private readonly ModCommandService.ModCommandAdapter _commands = commands.CreateAdapter(manifest);
	private readonly ModConsoleCommandAdapter _consoleCommands = new(consoleCommands, manifest, sessionService, frameworkLog);
	private readonly IModState _state = stateStore.CreateStateAdapter(manifest, sessionService);
	private readonly IModData _data = dataStore.CreateDataAdapter(manifest, sessionService);
	private readonly IModStatusRuntime _statusRuntime = statusStore.CreateStatusAdapter(manifest, sessionService);
	private readonly IModMoodleRuntime _moodleRuntime = new ModStatusMoodleRuntimeAdapter(statusStore, manifest, frameworkLog);
	private readonly IModBuildingRuntime _buildingRuntime = new ModBuildingRuntimeAdapter(buildingRuntime, manifest, frameworkLog);
	private ModStatusTransport? _statusTransport;
	private readonly ModUiAdapter _ui = new(manifest, frameworkLog);
	private readonly ModContentAdapter _content = new(manifest, frameworkLog);
	private readonly ModGameStateAdapter _gameState = new(manifest, sessionService, remoteVitals, remoteInventory, frameworkLog);
	private readonly ModEntitySpawnAdapter _entitySpawn = new(manifest, sessionService, entitySpawner, frameworkLog);
	private readonly ModItemSpawnAdapter _itemSpawn = new(manifest, sessionService, itemSpawner, frameworkLog);
	private readonly ModTilePlacementAdapter _tilePlacement = new(manifest, sessionService, tilePlacer, frameworkLog);
	private readonly ModStructurePlacementAdapter _structurePlacement = new(manifest, sessionService, structurePlacer, frameworkLog);
	private readonly ModLiquidPlacementAdapter _liquidPlacement = new(manifest, sessionService, liquidPlacer, frameworkLog);
	private readonly ModNativeApiAdapter _nativeApi = new(manifest, nativeApiProvider, frameworkLog);
	private readonly IModContentOwnerQuery _contentOwners = new ModContentOwnerQueryAdapter(contentControl);
	private readonly IModResourceCompletion _resourceCompletion = new ModResourceCompletionAdapter(resourceCompletionStore, manifest, frameworkLog);

	public ILogger Logger { get; } = logger;

	public IModNetwork Network => _network;

	public IModPackets Packets => _packets;

	public IModCommands Commands => _commands;

	public IModConsoleCommands ConsoleCommands => _consoleCommands;

	public IModState State => _state;

	public IModData Data => _data;

	public IModStatusRuntime StatusRuntime => _statusRuntime;

	public IModStatusTransport StatusTransport =>
		_statusTransport ??= new(_statusRuntime, _network, _manifest, _sessionService, _frameworkLog);

	public IModMoodleRuntime MoodleRuntime => _moodleRuntime;

	public IModBuildingRuntime BuildingRuntime => _buildingRuntime;

	public IModUi Ui => _ui;

	public IModContent Content => _content;

	public IModContentOwnerQuery ContentOwners => _contentOwners;

	public IModResourceCompletion ResourceCompletion => _resourceCompletion;

	public IModGameState GameState => _gameState;

	public IModEntitySpawn EntitySpawn => _entitySpawn;

	public IModItemSpawn ItemSpawn => _itemSpawn;
	public IModTilePlacement TilePlacement => _tilePlacement;
	public IModStructurePlacement StructurePlacement => _structurePlacement;
	public IModLiquidPlacement LiquidPlacement => _liquidPlacement;

	public IModNativeApi NativeApi => _nativeApi;

	public ISessionInfo Session { get; } = session;

	public event Action? SessionActivated;

	public event Action<ulong>? PlayerJoined;

	public event Action<ulong>? PlayerLeft;

	public event Action? SessionEnded;

	internal ModCommandService.ModCommandAdapter CommandAdapter => _commands;

	internal IReadOnlyList<ModUiWindow> UiWindows =>
		[.. _ui.Windows.Select(w => new ModUiWindow(_manifest.Id, w.Id, w.Title, w.Draw))];

	internal IReadOnlyList<ModContentRegistration> ContentRegistrations =>
		[.. _content.Definitions.Select(d => new ModContentRegistration(_manifest.Id, d, _manifest.Namespace))];

	// Events are only +=/-=-able from outside the declaring type — the
	// lifecycle fires through these.
	internal void FireSessionActivated() => SessionActivated?.Invoke();

	internal void FireSessionEnded() => SessionEnded?.Invoke();

	internal void FirePlayerJoined(ulong steamId) => PlayerJoined?.Invoke(steamId);

	internal void FirePlayerLeft(ulong steamId) => PlayerLeft?.Invoke(steamId);

	internal void FireMessageReceived(ulong sender, ModValue value) => _network.FireMessageReceived(sender, value);

	/// <summary>Route one received declared-packet frame on this copy (the mod domain's receive path calls this).</summary>
	internal ModPacketRoute RoutePacket(ulong sender, string packetId, ModValue value) =>
		_packets.Route(sender, packetId, value);

	internal void FailPendingCommands(string reason) => _commands.FailPending(reason);

	/// <summary>Settle every pending command request at or past its deadline (the per-frame pump).</summary>
	internal void PumpPendingCommands(long nowMs) => _commands.PumpPending(nowMs);
}
