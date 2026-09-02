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
	IModNativeApiProvider nativeApiProvider)
{
	private readonly ModCatalog _catalog = catalog;
	private readonly ModCommandService _commands = commands;
	private readonly ConsoleCommandRegistry _consoleCommands = consoleCommands;
	private readonly ModStateStore _stateStore = stateStore;
	private readonly SessionService _session = session;
	private readonly ModChannel _channel = channel;
	private readonly ModRegistry _registry = registry;
	private readonly ITimeSource _time = time;
	private readonly ILoggerFactory _loggerFactory = loggerFactory;
	private readonly ILogger _log = log;
	private readonly RemoteVitalsService _remoteVitals = remoteVitals;
	private readonly RemoteInventoryService _remoteInventory = remoteInventory;
	private readonly IModEntitySpawner _entitySpawner = entitySpawner;
	private readonly IModItemSpawner _itemSpawner = itemSpawner;
	private readonly IModNativeApiProvider _nativeApiProvider = nativeApiProvider;
	private readonly Dictionary<ulong, ModRateLimiter> _messageRateLimiters = [];
	private bool _discovered;
	private bool _disposed;

	internal void Initialize()
	{
		// The event bridge is subscribed here (construction-time wiring, not late
		// attachment), forwarded to every mod context discovered later.
		_session.SessionActivated += OnSessionActivated;
		_session.SessionEnded += OnSessionEnded;
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
		_session.SessionEnded -= OnSessionEnded;
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
					_commands,
					_consoleCommands,
					_remoteVitals,
					_remoteInventory,
					_entitySpawner,
					_itemSpawner,
					_nativeApiProvider);
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

	private void OnSessionEnded()
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

		if (msg.Payload.Length > ModChannel.MaxPayloadBytes)
		{
			_log.LogWarning("[Mods] {Sender} sent an over-cap {Length}-byte payload for {ModId} — dropped.",
				sender, msg.Payload.Length, msg.ModId);
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

		SafeRun(mod, "MessageReceived", () => mod.Context.FireMessageReceived(sender, msg.Payload));
	}

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
