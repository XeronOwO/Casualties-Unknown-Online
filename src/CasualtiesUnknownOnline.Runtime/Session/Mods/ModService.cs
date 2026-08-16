using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod domain coordinator (Phase 4 Mod API). Owns the loaded-mod table
/// (manifest + instance + context — internal state, never DI services) and
/// the lifecycle pump: the FIRST update frame runs the discovery scan
/// (BepInEx loads plugins one by one, load-then-Awake, so the framework's own
/// Awake would miss plugins loaded after it), then each discovered mod goes
/// Bind → Initialize → Start in that same frame and Update/Stop/Dispose from
/// then on — every stage exception-isolated (one broken mod never kills the
/// pump or its siblings, mirroring Plugin.RunLifecycle). Also bridges the
/// session events into the mod contexts (with the bind-time SNAPSHOT covering
/// the events that fire before discovery and the host-side SessionActivated
/// that never fires at all) and routes received mod frames by id to the local
/// copy of the mod (unknown ids and over-cap payloads dropped with a log).
/// </summary>
public sealed partial class ModService(SessionService session, ModChannel channel, ModRegistry registry,
	PacketSender sender, ITimeSource time, ILoggerFactory loggerFactory, ILogger<ModService> log) : ICuoService, IModsControl
{
	private readonly SessionService _session = session;
	private readonly ModChannel _channel = channel;
	private readonly ModRegistry _registry = registry;
	private readonly ILoggerFactory _loggerFactory = loggerFactory;
	private readonly ILogger<ModService> _log = log;
	private readonly PacketSender _sender = sender;
	private readonly ITimeSource _time = time;
	private readonly Dictionary<ulong, ModRateLimiter> _messageRateLimiters = [];
	private readonly Dictionary<ulong, ModRateLimiter> _commandRateLimiters = [];
	private readonly List<LoadedMod> _mods = [];
	private bool _discovered;
	private bool _disposed; // the container may dispose the same singleton once per registration (3.1 behaviour) — the ICuoService contract requires idempotent dispose

	// ---- ICuoService ----

	public void Initialize()
	{
		// The event bridge — subscribed here (construction-time wiring, not late
		// attachment), forwarded to every mod context discovered later.
		_session.SessionActivated += OnSessionActivated;
		_session.SessionEnded += OnSessionEnded;
		((ISessionControl)_session).MemberAdded += OnMemberAdded;
		((ISessionControl)_session).MemberRemoved += OnMemberRemoved;
		_channel.ModMessageReceived += OnModMessageReceived;
	}

	public void Start()
	{
	}

	public void Update()
	{
		if (!_discovered)
		{
			_discovered = true; // once: the first-frame discovery scan
			DiscoverAndLoad();
		}

		foreach (var mod in _mods)
		{
			SafeRun(mod, "Update", mod.Instance.Update);
		}
	}

	public void Stop()
	{
		foreach (var mod in _mods.AsEnumerable().Reverse())
		{
			SafeRun(mod, "Stop", mod.Instance.Stop);
		}
	}

	public void Dispose()
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

		FailAllPendingCommands("framework shutdown");

		foreach (var mod in _mods.AsEnumerable().Reverse())
		{
			SafeRun(mod, "Dispose", mod.Instance.Dispose);
		}
	}

	// ---- IModsControl (the sixth handler/context control surface) ----

	public void FireModMessageReceived(ulong sender, ModMessageMsg msg) => _channel.FireModMessageReceived(sender, msg);

	public IReadOnlyList<ModManifest> CurrentModManifests => [.. _mods.Select(m => m.Manifest)];

	public bool IsDiscoveryComplete => _discovered;

	/// <summary>Test seam (InternalsVisibleTo): the loaded instances, in discovery order — the lifecycle tests assert on them.</summary>
	internal IReadOnlyList<ICuoMod> LoadedMods => [.. _mods.Select(m => m.Instance)];

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
				var context = new ModContext(this, d.Manifest, _loggerFactory.CreateLogger($"Mod:{d.Manifest.Id}"));
				instance.Bind(context);
				instance.Initialize();
				instance.Start();
				_mods.Add(new LoadedMod(d.Manifest, instance, context));
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
		foreach (var mod in _mods)
		{
			SafeRun(mod, "SessionActivated", mod.Context.FireSessionActivated);
		}
	}

	private void OnSessionEnded()
	{
		FailAllPendingCommands("session ended");
		foreach (var mod in _mods)
		{
			SafeRun(mod, "SessionEnded", mod.Context.FireSessionEnded);
		}
	}

	private void OnMemberAdded(ulong steamId)
	{
		foreach (var mod in _mods)
		{
			SafeRun(mod, "PlayerJoined", () => mod.Context.FirePlayerJoined(steamId));
		}
	}

	private void OnMemberRemoved(ulong steamId)
	{
		foreach (var mod in _mods)
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

		var mod = _mods.FirstOrDefault(m => m.Manifest.Id == msg.ModId);
		if (mod is null)
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} — no local mod with that id, dropped.", msg.ModId, sender);
			return;
		}

		if (!HasPermission(mod, ModPermission.SendNetworkMessage))
		{
			_log.LogWarning("[Mods] message for {ModId} from {Sender} — the local mod does not declare SendNetworkMessage, dropped.", msg.ModId, sender);
			return;
		}

		SafeRun(mod, "MessageReceived", () => mod.Context.NetworkAdapter.FireMessageReceived(sender, msg.Payload));
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

	private bool TryConsumeCommandRequest(ulong sender)
	{
		if (!_commandRateLimiters.TryGetValue(sender, out var limiter))
		{
			limiter = new ModRateLimiter(ModRateLimitPolicy.CommandRequestsPerSecond, ModRateLimitPolicy.CommandRequestBurst);
			_commandRateLimiters[sender] = limiter;
		}

		if (limiter.TryConsume(_time.NowMs))
		{
			return true;
		}

		_log.LogWarning("[Mods] command-request rate limit hit for {Sender} — request dropped.", sender);
		return false;
	}

	private ISessionInfo BuildSessionSnapshot() => new SessionSnapshot(
		_session.Role == SessionRole.Host,
		_session.SessionActive,
		_session.LocalSteamId,
		_session.HostSteamId,
		[.. _session.Members.Select(m => m.SteamId)]);

	// ---- Nested types (private — part of the container, one top-level type per file) ----

	/// <summary>One loaded mod: manifest + instance + its context (the snapshot is taken at bind time).</summary>
	private sealed record LoadedMod(ModManifest Manifest, ICuoMod Instance, ModContext Context);

	/// <summary>
	/// The per-mod framework surface. Session is a snapshot at bind time (the
	/// host never fires SessionActivated and pre-discovery events are lost —
	/// the snapshot is the only reliable "current state"); the events are the
	/// increments after that. Network routes through the mod's own id.
	/// </summary>
	private sealed class ModContext : IModContext
	{
		private readonly ModNetworkAdapter _network;
		private readonly ModCommandAdapter _commands;

		internal ModContext(ModService owner, ModManifest manifest, ILogger logger)
		{
			Logger = logger;
			_network = new ModNetworkAdapter(owner, manifest);
			_commands = new ModCommandAdapter(owner, manifest);
			Session = owner.BuildSessionSnapshot();
		}

		public ILogger Logger { get; }

		public IModNetwork Network => _network;

		public IModCommands Commands => _commands;

		public ISessionInfo Session { get; }

		public event Action? SessionActivated;

		public event Action<ulong>? PlayerJoined;

		public event Action<ulong>? PlayerLeft;

		public event Action? SessionEnded;

		internal ModNetworkAdapter NetworkAdapter => _network;

		internal ModCommandAdapter CommandAdapter => _commands;

		// Events are only +=/-=-able from outside the declaring type — the
		// owner fires through these.
		internal void FireSessionActivated() => SessionActivated?.Invoke();

		internal void FireSessionEnded() => SessionEnded?.Invoke();

		internal void FirePlayerJoined(ulong steamId) => PlayerJoined?.Invoke(steamId);

		internal void FirePlayerLeft(ulong steamId) => PlayerLeft?.Invoke(steamId);
	}

	/// <summary>The per-mod send surface — every call routes through the channel with the mod's own id. SendNetworkMessage is checked here (undeclared messages are refused).</summary>
	private sealed class ModNetworkAdapter(ModService owner, ModManifest manifest) : IModNetwork
	{
		public void SendToHost(byte[] payload)
		{
			if (CanSend()) { owner._channel.SendToHost(manifest.Id, payload); }
		}

		public void SendToPeer(ulong steamId, byte[] payload)
		{
			if (CanSend()) { owner._channel.SendToPeer(manifest.Id, steamId, payload); }
		}

		public void Broadcast(byte[] payload)
		{
			if (CanSend()) { owner._channel.SendToAll(manifest.Id, payload); }
		}

		public event Action<ulong, byte[]>? MessageReceived;

		internal void FireMessageReceived(ulong sender, byte[] payload) => MessageReceived?.Invoke(sender, payload);

		private bool CanSend()
		{
			if (ModService.HasPermission(manifest, ModPermission.SendNetworkMessage))
			{
				return true;
			}

			owner.LogMissingPermission(manifest.Id, "SendNetworkMessage");
			return false;
		}
	}

	/// <summary>The read-only bind-time session snapshot (ISessionInfo).</summary>
	private sealed class SessionSnapshot : ISessionInfo
	{
		private readonly ulong[] _members;

		internal SessionSnapshot(bool isHost, bool active, ulong local, ulong host, ulong[] members)
		{
			IsHost = isHost;
			SessionActive = active;
			LocalSteamId = local;
			HostSteamId = host;
			_members = members;
		}

		public bool IsHost { get; }

		public bool SessionActive { get; }

		public ulong LocalSteamId { get; }

		public ulong HostSteamId { get; }

		public IReadOnlyList<ulong> MemberSteamIds => _members;
	}
}
