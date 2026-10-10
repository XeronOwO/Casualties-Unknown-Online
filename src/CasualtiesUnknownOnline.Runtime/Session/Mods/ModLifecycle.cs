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
	ModContentStore contentStore,
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
	private readonly ModContentDeclarationScanner _contentDeclarations = new(log);
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
	private readonly ModContentStore _contentStore = contentStore;
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

	/// <summary>The framework-wide content view: the store owns the entries, so this is the same set every other reader sees.</summary>
	internal IReadOnlyList<ModContentRegistration> Entries => _contentStore.Entries;

	internal ISessionInfo BuildSessionSnapshot() => ModSessionSnapshot.Capture(_session);

	// ---- Discovery + lifecycle ----

	private void DiscoverAndLoad()
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		var discovered = _registry.Discover(assemblies);

		// The content census runs before the load loop, so the [ModContent] classes
		// of an assembly NO mod of which loads are still reported once.
		_contentDeclarations.Census(assemblies);
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
					_contentStore,
					_resourceCompletionStages);
				RegisterDeclarations(d, context);
				instance.Bind(context);
				instance.Initialize();
				instance.Start();
				_catalog.Add(new LoadedMod(d.Manifest, instance, context));
				loadedIds.Add(d.Manifest.Id);
				_log.LogInformation("[Mods] {Id} loaded (bind + initialize + start in the discovery frame).", d.Manifest.Id);
			}
			catch (Exception e)
			{
				// A failed load takes its content with it: the entries were filed into the
				// framework-wide store BEFORE this throw (the declaration scan runs first, and a
				// code registration happens inside Bind), and a mod that never loaded must not
				// appear in the view the console, the ownership query and the content
				// fingerprints read.
				var withdrawn = _contentStore.RemoveMod(d.Manifest.Id);
				if (withdrawn > 0)
				{
					_log.LogDebug("[Mods] {Id} failed to load; {Count} content entry(ies) it had registered were withdrawn.", d.Manifest.Id, withdrawn);
				}

				_log.LogError(e, "[Mods] {Id} failed to load — skipped, the other mods continue.", d.Manifest.Id);
			}
		}
	}

	/// <summary>
	/// The attribute half of content registration, run BEFORE the mod's own
	/// <see cref="ICuoMod.Bind"/>: the declarations written next to the mod land
	/// first, so a bind that also registers by code sees them, and a duplicate id
	/// is refused on the code side instead of on the declared one. The scan owns
	/// the refusals and their logging — this is the per-mod call and its summary.
	/// </summary>
	private void RegisterDeclarations(DiscoveredMod mod, ModContext context)
	{
		var registered = _contentDeclarations.Register(mod.Type, context.Content);
		if (registered > 0)
		{
			_log.LogInformation("[Mods] {Id} registered {Count} content declaration(s) declared by [ModContent].",
				mod.Manifest.Id, registered);
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

		// The frame is CUO's own encoding of the mod's value, and this is the
		// one place it becomes typed again: a payload that is not exactly one
		// value inside the budgets is dropped with the refusal, before any
		// mod-authored callback runs. The raw bytes stay alongside it for the
		// relay below — the host forwards the frame it received, never a
		// re-encoding of what a handler made of its copy (it cannot make
		// anything of it: a value is immutable).
		if (!ModValueCodec.TryDecode(payload, ModChannel.MaxPayloadBytes, out var value, out var refusal))
		{
			_log.LogWarning("[Mods] {Sender} sent a value for {ModId} this framework cannot decode — {Reason}",
				sender, msg.ModId, refusal);
			return;
		}

		var packetId = msg.PacketId ?? string.Empty;
		if (packetId.Length == 0)
		{
			SafeRun(mod, "MessageReceived", () => mod.Context.FireMessageReceived(sender, value!));
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
		SafeRun(mod, $"packet {packetId}", () => route = mod.Context.RoutePacket(sender, packetId, value!));
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
