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
	IModContentControl contentControl) : IModContext
{
	private readonly ModManifest _manifest = manifest;
	private readonly SessionService _sessionService = sessionService;
	private readonly ILogger _frameworkLog = frameworkLog;
	private readonly ModNetworkAdapter _network = new(channel, manifest, frameworkLog);
	private readonly ModCommandService.ModCommandAdapter _commands = commands.CreateAdapter(manifest);
	private readonly ModConsoleCommandAdapter _consoleCommands = new(consoleCommands, manifest, sessionService, frameworkLog);
	private readonly IModState _state = stateStore.CreateStateAdapter(manifest, sessionService);
	private readonly IModData _data = dataStore.CreateDataAdapter(manifest, sessionService);
	private readonly IModStatusRuntime _statusRuntime = statusStore.CreateStatusAdapter(manifest, sessionService);
	private readonly IModMoodleRuntime _moodleRuntime = new ModStatusMoodleRuntimeAdapter(statusStore, manifest, frameworkLog);
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

	public ILogger Logger { get; } = logger;

	public IModNetwork Network => _network;

	public IModCommands Commands => _commands;

	public IModConsoleCommands ConsoleCommands => _consoleCommands;

	public IModState State => _state;

	public IModData Data => _data;

	public IModStatusRuntime StatusRuntime => _statusRuntime;

	public IModStatusTransport StatusTransport =>
		_statusTransport ??= new(_statusRuntime, _network, _manifest, _sessionService, _frameworkLog);

	public IModMoodleRuntime MoodleRuntime => _moodleRuntime;

	public IModUi Ui => _ui;

	public IModContent Content => _content;

	public IModContentOwnerQuery ContentOwners => _contentOwners;

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
		[.. _content.Definitions.Select(d => new ModContentRegistration(_manifest.Id, d))];

	// Events are only +=/-=-able from outside the declaring type — the
	// lifecycle fires through these.
	internal void FireSessionActivated() => SessionActivated?.Invoke();

	internal void FireSessionEnded() => SessionEnded?.Invoke();

	internal void FirePlayerJoined(ulong steamId) => PlayerJoined?.Invoke(steamId);

	internal void FirePlayerLeft(ulong steamId) => PlayerLeft?.Invoke(steamId);

	internal void FireMessageReceived(ulong sender, byte[] payload) => _network.FireMessageReceived(sender, payload);

	internal void FailPendingCommands(string reason) => _commands.FailPending(reason);

	// ---- Nested per-mod adapters (private — part of the context) ----

	/// <summary>The per-mod send surface — every call routes through the channel with the mod's own id. SendNetworkMessage is checked here (undeclared messages are refused).</summary>
	private sealed class ModNetworkAdapter(ModChannel channel, ModManifest manifest, ILogger log) : IModNetwork
	{
		public void SendToHost(byte[] payload)
		{
			if (CanSend()) { channel.SendToHost(manifest.Id, payload); }
		}

		public void SendToPeer(ulong steamId, byte[] payload)
		{
			if (CanSend()) { channel.SendToPeer(manifest.Id, steamId, payload); }
		}

		public void Broadcast(byte[] payload)
		{
			if (CanSend()) { channel.SendToAll(manifest.Id, payload); }
		}

		public event Action<ulong, byte[]>? MessageReceived;

		public void FireMessageReceived(ulong sender, byte[] payload) => MessageReceived?.Invoke(sender, payload);

		private bool CanSend()
		{
			if (ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage))
			{
				return true;
			}

			log.LogWarning("[Mods] {ModId} does not declare {Permission} — the call is refused.", manifest.Id, "SendNetworkMessage");
			return false;
		}
	}

	/// <summary>
	/// The per-mod content registry: a small definition list scoped by
	/// construction to one mod id. Registration failures are logged and refused
	/// (missing permission, invalid id/kind/data, duplicate id, count cap); the
	/// stored payloads are defensive copies and every read returns another copy.
	/// </summary>
	private sealed class ModContentAdapter(ModManifest manifest, ILogger log) : IModContent
	{
		private readonly List<ModContentDefinition> _definitions = [];

		public bool CanRegister => ModPermissionGate.HasPermission(manifest, ModPermission.RegisterContent);

		public int Count => _definitions.Count;

		public IReadOnlyCollection<ModContentDefinition> Definitions => [.. _definitions];

		public bool TryRegister(string id, string kind, byte[] data) =>
			TryRegister(id, kind, data, 1);

		public bool TryRegister(string id, string kind, byte[] data, int schemaVersion)
		{
			if (!ModPermissionGate.Try(log, manifest, ModPermission.RegisterContent))
			{
				return false;
			}

			if (!ModContentPolicy.IsValidId(id))
			{
				log.LogWarning("[Mods] {ModId} tried to register content with an invalid id {Id} — refused.",
					manifest.Id, id);
				return false;
			}

			if (!ModContentPolicy.IsValidKind(kind))
			{
				log.LogWarning("[Mods] {ModId} tried to register content {Id} with an invalid kind {Kind} — refused.",
					manifest.Id, id, kind);
				return false;
			}

			if (!ModContentPolicy.IsValidSchemaVersion(schemaVersion))
			{
				log.LogWarning("[Mods] {ModId} tried to register content {Id} with invalid schema version {SchemaVersion} — refused.",
					manifest.Id, id, schemaVersion);
				return false;
			}

			if (!ModContentPolicy.IsValidData(data))
			{
				log.LogWarning("[Mods] {ModId} tried to register content {Id} with a {Length}-byte payload; the cap is {Cap} bytes — refused.",
					manifest.Id, id, data?.Length ?? 0, ModContentPolicy.MaxDefinitionBytes);
				return false;
			}

			if (_definitions.Any(d => d.Id == id))
			{
				log.LogWarning("[Mods] {ModId}/{Id} is already registered as content — the duplicate is refused.",
					manifest.Id, id);
				return false;
			}

			if (!ModContentPolicy.CanAdd(_definitions.Count))
			{
				log.LogWarning("[Mods] {ModId} reached the {Cap}-definition content cap — {Id} refused.",
					manifest.Id, ModContentPolicy.MaxDefinitionsPerMod, id);
				return false;
			}

			var definition = new ModContentDefinition(id, kind, data, schemaVersion);
			_definitions.Add(definition);
			log.LogInformation("[Mods] {ModId} registered content {Id} ({Kind}, schema {SchemaVersion}, {Length} bytes).",
				manifest.Id, id, kind, definition.SchemaVersion, data.Length);
			return true;
		}

		public bool TryUnregister(string id)
		{
			var index = _definitions.FindIndex(d => d.Id == id);
			if (index < 0)
			{
				return false;
			}

			_definitions.RemoveAt(index);
			return true;
		}

		public bool IsRegistered(string id) => _definitions.Any(d => d.Id == id);
	}

	/// <summary>
	/// The per-mod UI registry: a tiny immediate-mode window list. Register
	/// failures are logged and refused (empty id/title, null draw handler,
	/// duplicate id); the mod id is scoped by construction because the adapter
	/// belongs to exactly one mod context.
	/// </summary>
	private sealed class ModUiAdapter(ModManifest manifest, ILogger log) : IModUi
	{
		private readonly List<ModUiRegistration> _windows = [];

		public bool Register(string id, string title, Action<IModUiWindow> draw)
		{
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || draw is null)
			{
				log.LogWarning("[Mods] {ModId} tried to register an invalid mod UI window (empty id/title or null draw) — refused.",
					manifest.Id);
				return false;
			}

			if (_windows.Any(w => w.Id == id))
			{
				log.LogWarning("[Mods] {ModId}/{Id} is already registered as a UI window — the duplicate is refused.",
					manifest.Id, id);
				return false;
			}

			_windows.Add(new ModUiRegistration(id, title, draw));
			return true;
		}

		public bool Unregister(string id)
		{
			var index = _windows.FindIndex(w => w.Id == id);
			if (index < 0)
			{
				return false;
			}

			_windows.RemoveAt(index);
			return true;
		}

		public bool IsRegistered(string id) => _windows.Any(w => w.Id == id);

		public IReadOnlyCollection<string> WindowIds => [.. _windows.Select(w => w.Id)];

		internal IReadOnlyList<ModUiRegistration> Windows => _windows;

		internal sealed record ModUiRegistration(string Id, string Title, Action<IModUiWindow> Draw);
	}

	/// <summary>
	/// The per-mod entity-spawn adapter. Permission, session/world state and
	/// request-shape checks happen here; the actual prefab creation happens on
	/// the other side of <see cref="IModEntitySpawner"/>.
	/// </summary>
	private sealed class ModEntitySpawnAdapter(ModManifest manifest, SessionService session, IModEntitySpawner entitySpawner, ILogger log) : IModEntitySpawn
	{
		public bool CanSpawn => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

		public bool TrySpawn(string prefabId, float x, float y, float rotation)
		{
			if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
			{
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPrefabId(prefabId))
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an entity with an invalid prefab id {PrefabId} — refused.",
					manifest.Id, prefabId);
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPosition(x, y) || !ModEntitySpawnPolicy.IsValidRotation(rotation))
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an entity with a non-finite position/rotation — refused.",
					manifest.Id);
				return false;
			}

			if (!session.SessionActive || !session.LocalInWorld)
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an entity outside an active in-world session — refused.",
					manifest.Id);
				return false;
			}

			if (!entitySpawner.TrySpawnEntity(prefabId, x, y, rotation))
			{
				log.LogWarning("[Mods] {ModId} could not spawn entity {PrefabId} at ({X:F1},{Y:F1}) — the Game Adapter did not create a BuildingEntity.",
					manifest.Id, prefabId, x, y);
				return false;
			}

			log.LogInformation("[Mods] {ModId} spawned entity {PrefabId} at ({X:F1},{Y:F1}) rotation {Rotation:F1}.",
				manifest.Id, prefabId, x, y, rotation);
			return true;
		}
	}

	/// <summary>
	/// The per-mod item-spawn adapter. Permission, session/world state and
	/// request-shape checks happen here; the actual item creation happens on the
	/// other side of <see cref="IModItemSpawner"/>.
	/// </summary>
	private sealed class ModItemSpawnAdapter(ModManifest manifest, SessionService session, IModItemSpawner itemSpawner, ILogger log) : IModItemSpawn
	{
		public bool CanSpawn => ModPermissionGate.HasPermission(manifest, ModPermission.SpawnEntity);

		public bool TrySpawn(string itemId, float x, float y, float rotation)
		{
			if (!ModPermissionGate.Try(log, manifest, ModPermission.SpawnEntity))
			{
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPrefabId(itemId))
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an item with an invalid id {ItemId} — refused.",
					manifest.Id, itemId);
				return false;
			}

			if (!ModEntitySpawnPolicy.IsValidPosition(x, y) || !ModEntitySpawnPolicy.IsValidRotation(rotation))
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an item with a non-finite position/rotation — refused.",
					manifest.Id);
				return false;
			}

			if (!session.SessionActive || !session.LocalInWorld)
			{
				log.LogWarning("[Mods] {ModId} tried to spawn an item outside an active in-world session — refused.",
					manifest.Id);
				return false;
			}

			if (!itemSpawner.TrySpawnItem(itemId, x, y, rotation))
			{
				log.LogWarning("[Mods] {ModId} could not spawn item {ItemId} at ({X:F1},{Y:F1}) — the Game Adapter did not create an Item.",
					manifest.Id, itemId, x, y);
				return false;
			}

			log.LogInformation("[Mods] {ModId} spawned item {ItemId} at ({X:F1},{Y:F1}) rotation {Rotation:F1}.",
				manifest.Id, itemId, x, y, rotation);
			return true;
		}
	}

	/// <summary>
	/// The per-mod read-only game-state adapter. It reads from the same
	/// session-scoped remote-vitals/remote-inventory projections the Online UI
	/// uses, so a mod sees the same facts as the built-in UI without a second
	/// source of truth.
	/// </summary>
	private sealed class ModGameStateAdapter(
		ModManifest manifest,
		SessionService session,
		RemoteVitalsService vitals,
		RemoteInventoryService inventories,
		ILogger log) : IModGameState
	{
		public bool CanRead => ModPermissionGate.HasPermission(manifest, ModPermission.ReadGameState);

		public bool TryGetPlayer(ulong steamId, out IModPlayerState player)
		{
			if (!ModPermissionGate.Try(log, manifest, ModPermission.ReadGameState))
			{
				player = null!;
				return false;
			}

			vitals.TryGet(steamId, out var vitalsSnapshot);
			inventories.TryGet(steamId, out var inventorySnapshot);
			if (vitalsSnapshot is null && inventorySnapshot is null)
			{
				player = null!;
				return false;
			}

			var inWorld = steamId == session.LocalSteamId
				? session.LocalInWorld
				: session.IsRemoteInWorld(steamId);

			player = new ModPlayerState(
				steamId,
				inWorld,
				vitalsSnapshot is null ? null : new ModPlayerVitals(vitalsSnapshot),
				inventorySnapshot is null ? null : new ModPlayerInventory(inventorySnapshot));
			return true;
		}
	}

	private sealed record ModPlayerState(
		ulong SteamId,
		bool InWorld,
		IModPlayerVitals? Vitals,
		IModPlayerInventory? Inventory) : IModPlayerState;

	private sealed record ModPlayerVitals(
		float BrainHealth,
		float Hunger,
		float Thirst,
		float Stamina,
		float Energy,
		float Temperature,
		bool Alive,
		bool Conscious) : IModPlayerVitals
	{
		internal ModPlayerVitals(RemoteVitalsSnapshot source)
			: this(
				source.BrainHealth,
				source.Hunger,
				source.Thirst,
				source.Stamina,
				source.Energy,
				source.Temperature,
				source.Alive,
				source.Conscious)
		{
		}
	}

	private sealed record ModPlayerInventory(
		IReadOnlyList<IModInventoryEntry> Items,
		int HandSlot) : IModPlayerInventory
	{
		public int Count => Items.Count;

		internal ModPlayerInventory(RemoteInventorySnapshot source)
			: this([.. source.Items.Select(Project)], source.HandSlot)
		{
		}

		private static IModInventoryEntry Project(RemoteInventoryEntry entry) =>
			new ModInventoryEntry(
				entry.InstanceId,
				entry.ItemId,
				entry.SlotIndex,
				entry.Condition,
				entry.Favourited,
				[.. entry.Contents.Select(Project)]);
	}

	private sealed record ModInventoryEntry(
		ulong InstanceId,
		string ItemId,
		int SlotIndex,
		float Condition,
		bool Favourited,
		IReadOnlyList<IModInventoryEntry> Contents) : IModInventoryEntry;

	/// <summary>
	/// The per-mod native-API adapter. The common read-only projection
	/// (<see cref="ModNativeApiOperations.LocalPlayerState"/>) is exposed both
	/// through the generic operation registry and as a typed convenience.
	/// </summary>
	private sealed class ModNativeApiAdapter(ModManifest manifest, IModNativeApiProvider nativeApiProvider, ILogger log) : IModNativeApi
	{
		public bool CanAccess => ModPermissionGate.HasPermission(manifest, ModPermission.AccessNativeApi);

		public bool CanInvoke(string operation)
		{
			if (!CanAccess || !ModNativeApiPolicy.IsValidOperation(operation))
			{
				return false;
			}

			return nativeApiProvider.IsRegistered(operation);
		}

		public bool TryInvoke(string operation, object?[] arguments, out object? result)
		{
			result = null;

			if (!ModPermissionGate.Try(log, manifest, ModPermission.AccessNativeApi))
			{
				return false;
			}

			if (!ModNativeApiPolicy.IsValidOperation(operation))
			{
				log.LogWarning("[Mods] {ModId} tried to invoke a native operation with an invalid id '{Operation}' — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!ModNativeApiPolicy.IsValidArguments(arguments))
			{
				log.LogWarning("[Mods] {ModId} tried to invoke native operation {Operation} with unsafe/over-cap arguments — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!nativeApiProvider.TryInvoke(operation, arguments, out var nativeResult))
			{
				log.LogWarning("[Mods] {ModId} native operation {Operation} is not available or was refused by the Game Adapter — refused.",
					manifest.Id, operation);
				return false;
			}

			if (!ModNativeApiPolicy.IsSafeResult(nativeResult))
			{
				log.LogWarning("[Mods] {ModId} native operation {Operation} returned an unsafe value type {ValueType} — refused.",
					manifest.Id, operation, nativeResult?.GetType().FullName ?? "null");
				return false;
			}

			result = nativeResult;
			log.LogInformation("[Mods] {ModId} invoked native operation {Operation} ({ArgumentCount} argument(s)).",
				manifest.Id, operation, arguments.Length);
			return true;
		}

		public bool TryGetLocalPlayerState(out IModNativeLocalPlayerState state)
		{
			state = null!;

			if (TryInvoke(ModNativeApiOperations.LocalPlayerState, [], out var result)
				&& result is IModNativeLocalPlayerState localState)
			{
				state = localState;
				return true;
			}

			return false;
		}
	}
}
