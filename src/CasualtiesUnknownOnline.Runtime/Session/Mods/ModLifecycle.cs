using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod lifecycle pump and session-event bridge. It owns the discovery
/// scan, the loaded-mod update/stop/dispose pump, the session event fan-out
/// and the received mod-frame routing. Every per-mod API surface lives in
/// <see cref="ModContext"/>, and the host-command / mod-state domains live in
/// <see cref="ModCommandService"/> / <see cref="ModStateStore"/>; this class is
/// deliberately only the orchestration half of the mod domain.
/// </summary>
internal sealed class ModLifecycle(
	ModCatalog catalog,
	ModCommandService commands,
	ConsoleCommandRegistry consoleCommands,
	ModStateStore stateStore,
	ModDataStore dataStore,
	ModStatusStore statusStore,
	ModBuildingRuntimeStore buildingRuntime,
	SessionService session,
	ModChannel channel,
	ModRegistry registry,
	ITimeSource time,
	ILoggerFactory loggerFactory,
	ILogger log,
	RemoteVitalsService remoteVitals,
	RemoteInventoryService remoteInventory,
	IModEntitySpawner entitySpawner,
	IModItemSpawner itemSpawner,
	IModTilePlacer tilePlacer,
	IModStructurePlacer structurePlacer,
	IModLiquidPlacer liquidPlacer,
	IModNativeApiProvider nativeApiProvider,
	IModContentControl contentControl,
	ModResourceCompletionStore resourceCompletionStages) : ISessionReset
{
	private readonly ModCatalog _catalog = catalog;
	private readonly ModCommandService _commands = commands;
	private readonly ConsoleCommandRegistry _consoleCommands = consoleCommands;
	private readonly ModStateStore _stateStore = stateStore;
	private readonly ModDataStore _dataStore = dataStore;
	private readonly ModStatusStore _statusStore = statusStore;
	private readonly ModBuildingRuntimeStore _buildingRuntime = buildingRuntime;
	private readonly SessionService _session = session;
	private readonly ModChannel _channel = channel;
	private readonly ModRegistry _registry = registry;
	private readonly ITimeSource _time = time;
	private readonly ILoggerFactory _loggerFactory = loggerFactory;
	private readonly ILogger _log = log;
	private readonly RemoteVitalsService _remoteVitals = remoteVitals;
	private readonly RemoteInventoryService _remoteInventory = remoteInventory;
	private readonly IModEntitySpawner _entitySpawner = entitySpawner;
	private readonly IModTilePlacer _tilePlacer = tilePlacer;
	private readonly IModItemSpawner _itemSpawner = itemSpawner;
	private readonly IModStructurePlacer _structurePlacer = structurePlacer;
	private readonly IModLiquidPlacer _liquidPlacer = liquidPlacer;
	private readonly IModNativeApiProvider _nativeApiProvider = nativeApiProvider;
	private readonly IModContentControl _contentControl = contentControl;
	private readonly ModResourceCompletionStore _resourceCompletionStages = resourceCompletionStages;
	private readonly Dictionary<ulong, ModRateLimiter> _messageRateLimiters = [];
	private bool _discovered;
	private bool _disposed;

	internal void Initialize()
	{
		// The event bridge is subscribed here (construction-time wiring, not late
		// attachment), forwarded to every mod context discovered later.
		_session.SessionActivated += OnSessionActivated;
		_session.SessionEnded += ResetSessionState;
		((ISessionControl)_session).MemberAdded += OnMemberAdded;
		((ISessionControl)_session).MemberRemoved += OnMemberRemoved;
		_channel.ModMessageReceived += OnModMessageReceived;
	}

	internal void Update()
	{
		if (!_discovered)
		{
			_discovered = true; // once: the first-frame discovery scan
			DiscoverAndLoad();
		}

		_commands.PumpPendingTimeouts(); // settle stale guest requests before the mods see this frame

		foreach (var mod in _catalog.Mods)
		{
			SafeRun(mod, "Update", mod.Instance.Update);
		}
	}

	internal void Stop()
	{
		foreach (var mod in _catalog.Mods.AsEnumerable().Reverse())
		{
			SafeRun(mod, "Stop", mod.Instance.Stop);
		}
	}

	internal void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_session.SessionActivated -= OnSessionActivated;
		_session.SessionEnded -= ResetSessionState;
		((ISessionControl)_session).MemberAdded -= OnMemberAdded;
		((ISessionControl)_session).MemberRemoved -= OnMemberRemoved;
		_channel.ModMessageReceived -= OnModMessageReceived;

		foreach (var mod in _catalog.Mods.AsEnumerable().Reverse())
		{
			SafeRun(mod, "Dispose", mod.Instance.Dispose);
		}
	}

	internal void FireModMessageReceived(ulong sender, ModMessageMsg msg) => _channel.FireModMessageReceived(sender, msg);

	internal IReadOnlyList<ModManifest> CurrentModManifests => _catalog.CurrentManifests;

	internal bool IsDiscoveryComplete => _discovered;

	internal IReadOnlyList<ICuoMod> LoadedMods => _catalog.LoadedInstances;

	internal IReadOnlyList<ModUiWindow> Windows =>
		[.. _catalog.Mods.SelectMany(m => m.Context.UiWindows)];

	internal IReadOnlyList<ModContentRegistration> Entries =>
		[.. _catalog.Mods.SelectMany(m => m.Context.ContentRegistrations)];

	internal ISessionInfo BuildSessionSnapshot() => ModSessionSnapshot.Capture(_session);

	// ---- Discovery + lifecycle ----

	private void DiscoverAndLoad()
	{
		var discovered = _registry.Discover(AppDomain.CurrentDomain.GetAssemblies());
		var loadedIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var d in discovered)
		{
			if (d.Manifest.Dependencies.Any(dep => !loadedIds.Contains(dep)))
			{
				_log.LogWarning("[Mods] {Id} dependency did not load — skipped.", d.Manifest.Id);
				continue;
			}

			try
			{
				var instance = (ICuoMod)Activator.CreateInstance(d.Type)!;
				var context = new ModContext(
					d.Manifest,
					_loggerFactory.CreateLogger($"Mod:{d.Manifest.Id}"),
					_log,
					BuildSessionSnapshot(),
					_session,
					_channel,
					_stateStore,
					_dataStore,
					_statusStore,
					_buildingRuntime,
					_commands,
					_consoleCommands,
					_remoteVitals,
					_remoteInventory,
					_entitySpawner,
					_itemSpawner,
					_tilePlacer,
					_structurePlacer,
					_liquidPlacer,
					_nativeApiProvider,
					_contentControl,
					_resourceCompletionStages);
				instance.Bind(context);
				instance.Initialize();
				instance.Start();
				_catalog.Add(new LoadedMod(d.Manifest, instance, context));
				loadedIds.Add(d.Manifest.Id);
				_log.LogInformation("[Mods] {Id} loaded (bind + initialize + start in the discovery frame).", d.Manifest.Id);
			}
			catch (Exception e)
			{
				_log.LogError(e, "[Mods] {Id} failed to load — skipped, the other mods continue.", d.Manifest.Id);
			}
		}
	}

	private void SafeRun(LoadedMod mod, string stage, Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			_log.LogError(e, "[Mods] {Id} threw in {Stage} — isolated, the pump continues.", mod.Manifest.Id, stage);
		}
	}

	// ---- Event bridge (session → mod contexts) ----

	private void OnSessionActivated()
	{
		foreach (var mod in _catalog.Mods)
		{
			SafeRun(mod, "SessionActivated", mod.Context.FireSessionActivated);
		}
	}

	public void ResetSessionState()
	{
		_commands.FailAllPending("session ended");
		foreach (var mod in _catalog.Mods)
		{
			SafeRun(mod, "SessionEnded", mod.Context.FireSessionEnded);
		}
	}

	private void OnMemberAdded(ulong steamId)
	{
		foreach (var mod in _catalog.Mods)
		{
			SafeRun(mod, "PlayerJoined", () => mod.Context.FirePlayerJoined(steamId));
		}
	}

	private void OnMemberRemoved(ulong steamId)
	{
		foreach (var mod in _catalog.Mods)
		{
			SafeRun(mod, "PlayerLeft", () => mod.Context.FirePlayerLeft(steamId));
		}
	}

	private void OnModMessageReceived(ulong sender, ModMessageMsg msg)
	{
		if (!TryConsumeModMessage(sender))
		{
			return;
		}

		var payload = msg.Payload;
		if (payload is null || payload.Length > ModChannel.MaxPayloadBytes)
		{
			_log.LogWarning("[Mods] {Sender} sent a null or over-cap {Length}-byte payload for {ModId} — dropped.",
				sender, payload?.Length ?? 0, msg.ModId);
			return;
		}

		if (!IsModMessageSender(sender))
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} — neither this local peer nor a handshaken member, dropped.",
				msg.ModId, sender);
			return;
		}

		var mod = _catalog.Find(msg.ModId);
		if (mod is null)
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} — no local mod with that id, dropped.", msg.ModId, sender);
			return;
		}

		if (!ModPermissionGate.HasPermission(mod.Manifest, ModPermission.SendNetworkMessage))
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} — the local mod does not declare SendNetworkMessage, dropped.", msg.ModId, sender);
			return;
		}

		var packetId = msg.PacketId ?? string.Empty;
		if (packetId.Length == 0)
		{
			SafeRun(mod, "MessageReceived", () => mod.Context.FireMessageReceived(sender, payload));
			return;
		}

		// The packet id is mod-authored text that a peer chose: bound it by the
		// registration grammar BEFORE it reaches a log line, so an
		// attacker-sized id is named by its length instead of being echoed.
		if (!ModPacketPolicy.IsValidId(packetId))
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} carries an invalid {Length}-character packet id — dropped.",
				msg.ModId, sender, packetId.Length);
			return;
		}

		// A declared packet: the receiving copy's own declaration decides what
		// runs and whether the host still owes the other members a relay. The
		// chain isolates its own handlers; the isolation here covers the rest of
		// the routing, so no mod-authored declaration can wedge the receive path.
		var route = ModPacketRoute.UnknownPacket;
		SafeRun(mod, $"packet {packetId}", () => route = mod.Context.RoutePacket(sender, packetId, payload));
		if (route == ModPacketRoute.Relay)
		{
			_channel.RelayPacket(mod.Manifest.Id, sender, packetId, payload);
		}
	}

	/// <summary>
	/// Mod traffic is member traffic: a frame is accepted from the local peer
	/// (the host's own local fire) or from a handshaken member — the same gate
	/// the host-command path applies. A peer that completed no handshake reaches
	/// no mod callback.
	/// </summary>
	private bool IsModMessageSender(ulong sender) =>
		sender == _session.LocalSteamId
		|| (((ISessionControl)_session).TryGetMember(sender, out var member) && member.Handshaken);

	private bool TryConsumeModMessage(ulong sender)
	{
		if (!_messageRateLimiters.TryGetValue(sender, out var limiter))
		{
			limiter = new ModRateLimiter(ModRateLimitPolicy.ModMessagesPerSecond, ModRateLimitPolicy.ModMessageBurst);
			_messageRateLimiters[sender] = limiter;
		}

		if (limiter.TryConsume(_time.NowMs))
		{
			return true;
		}

		_log.LogWarning("[Mods] mod-message rate limit hit for {Sender} — frame dropped.", sender);
		return false;
	}
}
