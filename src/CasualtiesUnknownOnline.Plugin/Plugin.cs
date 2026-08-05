using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	private const byte MsgPing = 0;
	private const byte MsgPong = 1;

	internal static new ManualLogSource Logger = null!;

	private const float AutoPingIntervalSeconds = 5f;
	private const float MemberLogIntervalSeconds = 10f;

	private ServiceProvider _services = null!;
	private ICuoService[] _cuoServices = Array.Empty<ICuoService>();
	private ILogger<Plugin> _log = null!;
	private SteamService _steam = null!;
	private SteamTransport _transport = null!;
	private ConfigEntry<string> _targetLobbyId = null!;
	private float _lastRttMs = -1f;
	private float _nextAutoPingTime;
	private float _nextMemberLogTime;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	// Loads steam_api64.dll from this plugin's folder so DllImport in
	// Steamworks.NET resolves it (DllImport only searches the exe dir,
	// system dirs and PATH by default). Must run before any Steam call.
	private static void PreloadNativeLibrary()
	{
		try
		{
			var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var path = Path.Combine(dir ?? "", "steam_api64.dll");
			if (LoadLibrary(path) == IntPtr.Zero)
				Logger.LogWarning($"CUO: LoadLibrary failed for {path} (Win32 error {Marshal.GetLastWin32Error()})");
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"CUO: native library preload failed: {ex.Message}");
		}
	}

	private void Awake()
	{
		Logger = base.Logger;
		PreloadNativeLibrary();

		try
		{
			// DI owns construction; BepInEx/Unity own the lifecycle. The plugin
			// forwards lifecycle notifications into ICuoService (architecture.md §5.5).
			_services = CuoBootstrap.BuildServiceProvider(
				Logger,
				Path.Combine(Paths.BepInExRootPath, "CUO", "logs"),
				legacyLogPath: Path.Combine(Paths.BepInExRootPath, "CUO.log"));

			_log = _services.GetRequiredService<ILogger<Plugin>>();
			_steam = _services.GetRequiredService<SteamService>();
			_transport = _services.GetRequiredService<SteamTransport>();
			_cuoServices = _services.GetServices<ICuoService>().ToArray();

			// Multiplayer games must keep running when the window loses focus.
			Application.runInBackground = true;

			_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
				"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");

			// Wire events BEFORE Initialize — callbacks may fire immediately.
			_steam.LobbyCreated += lobbyId => _log.LogInformation("Lobby created: {LobbyId}", lobbyId);
			_steam.LobbyEntered += lobbyId => _log.LogInformation("Lobby entered: {LobbyId}", lobbyId);
			_transport.MessageReceived += OnMessageReceived;

			// Forward Unity log messages into CUO's own log so runtime errors
			// (which BepInEx's DiskLogListener may not capture) are visible.
			Application.logMessageReceived += OnUnityLogMessage;

			foreach (var service in _cuoServices)
				RunLifecycle(service, "Initialize", s => s.Initialize());
			foreach (var service in _cuoServices)
				RunLifecycle(service, "Start", s => s.Start());

			_log.LogInformation("Plugin {PluginGuid} is loaded!", MyPluginInfo.PLUGIN_GUID);

			// Initialize Steam at load time (same as KrokMP's CheckSteam on
			// startup): the lobby UI later keys off IsInitialized.
			if (_steam.IsInitialized)
			{
				_log.LogInformation("CUO Phase 0 test keys: F8 = create lobby, F9 = join lobby from config, F7 = ping first peer.");
			}
			else
			{
				_log.LogWarning("CUO: Steam not initialized — lobby features unavailable. F8 can retry.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError($"CUO startup failed: {ex}");
		}
	}

	private void Update()
	{
		foreach (var service in _cuoServices)
			RunLifecycle(service, "Update", s => s.Update());

		// Phase-0 auto-ping: while in a lobby with a peer, ping every few
		// seconds without requiring key input (window focus breaks Input
		// during dual-instance testing). Keeps connection diagnostics flowing.
		if (_steam.IsInitialized && _steam.CurrentLobbyId != 0)
		{
			if (Time.unscaledTime >= _nextMemberLogTime)
			{
				_nextMemberLogTime = Time.unscaledTime + MemberLogIntervalSeconds;
				var members = _steam.GetLobbyMembers();
				_log.LogInformation("Lobby {LobbyId}: {MemberCount} member(s){Peer}",
					_steam.CurrentLobbyId, members.Length,
					members.Length > 1 ? $" — peer {members.FirstOrDefault(m => m != _steam.LocalSteamId)}" : "");
			}

			if (Time.unscaledTime >= _nextAutoPingTime)
			{
				_nextAutoPingTime = Time.unscaledTime + AutoPingIntervalSeconds;
				if (_steam.GetLobbyMembers().Length > 1)
					SendPing();
			}
		}

		if (_steam is { } steam)
		{
			if (Input.GetKeyDown(KeyCode.F8))
			{
				// Retry path: if load-time init failed (Steam not running yet),
				// F8 re-attempts initialization, then creates the lobby.
				if (EnsureSteamReady(steam))
					steam.CreateLobby();
			}
			else if (Input.GetKeyDown(KeyCode.F9))
			{
				if (EnsureSteamReady(steam) && ulong.TryParse(_targetLobbyId.Value, out var lobbyId))
					steam.JoinLobby(lobbyId);
			}
			else if (Input.GetKeyDown(KeyCode.F7))
			{
				SendPing();
			}
		}
	}

	// Forwards one lifecycle stage to a service; a failing service is logged
	// and never allowed to break the frame loop or the shutdown sequence.
	private void RunLifecycle(ICuoService service, string stage, Action<ICuoService> call)
	{
		try
		{
			call(service);
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "ICuoService.{Stage} failed for {ServiceType}", stage, service.GetType().Name);
		}
	}

	private bool EnsureSteamReady(SteamService steam) => steam.Initialize();

	private void SendPing()
	{
		if (!_steam.IsInitialized)
		{
			_log.LogWarning("CUO: Steam not initialized — press F8 first.");
			return;
		}

		var target = _steam.GetLobbyMembers().FirstOrDefault(m => m != _steam.LocalSteamId);
		if (target == 0)
		{
			_log.LogWarning("CUO: no peer in the lobby to ping.");
			return;
		}

		var payload = new byte[9];
		payload[0] = MsgPing;
		BitConverter.GetBytes(DateTime.UtcNow.Ticks).CopyTo(payload, 1);
		var sent = _transport.SendTo(target, payload, reliable: true);
		_log.LogInformation(sent ? "CUO: ping -> {Target}" : "CUO: ping to {Target} FAILED", target);
	}

	private void OnMessageReceived(ulong sender, byte[] data)
	{
		if (data.Length == 0)
			return;

		switch (data[0])
		{
			case MsgPing when data.Length >= 9:
				// Echo the sender's timestamp back in the pong so RTT is
				// computable on their side (pong needs the full 9-byte frame).
				var pong = (byte[])data.Clone();
				pong[0] = MsgPong;
				_transport.SendTo(sender, pong, reliable: true);
				_log.LogInformation("CUO: ping from {Sender} — pong sent.", sender);
				break;

			case MsgPong when data.Length >= 9:
				var sentTicks = BitConverter.ToInt64(data, 1);
				_lastRttMs = (DateTime.UtcNow.Ticks - sentTicks) / 10_000.0f;
				_log.LogInformation("CUO: pong from {Sender} — RTT {RttMs:F1} ms", sender, _lastRttMs);
				break;
		}
	}

	// Phase-0 test HUD (IMGUI, temporary): replace with real UI in later phases.
	private void OnGUI()
	{
		var y = 10f;
		Line("CUO Phase 0 — Steam: " + (_steam.IsInitialized ? "initialized" : "not initialized"));
		if (_steam.IsInitialized)
		{
			Line($"SteamID: {_steam.LocalSteamId}");
			Line($"Lobby: {_steam.CurrentLobbyId}  Members: {_steam.GetLobbyMembers().Length}");
		}

		Line(_lastRttMs >= 0f ? $"Last RTT: {_lastRttMs:F1} ms" : "No ping yet");
		Line("F8 create lobby / F9 join from config / F7 ping peer");

		void Line(string text)
		{
			GUI.Label(new Rect(10f, y, 900f, 20f), text);
			y += 20f;
		}
	}

	private void OnUnityLogMessage(string message, string stackTrace, LogType type)
	{
		switch (type)
		{
			case LogType.Error:
			case LogType.Exception:
			case LogType.Assert:
				_log.LogError("[Unity:{Type}] {Message}\n{StackTrace}", type, message, stackTrace);
				break;
			case LogType.Warning:
				_log.LogWarning("[Unity] {Message}", message);
				break;
		}
	}

	// SteamManager guidance: never do Steamworks work in OnDestroy (execution
	// order is not guaranteed); OnDisable is the safe teardown point.
	private void OnDisable()
	{
		Application.logMessageReceived -= OnUnityLogMessage;

		// Stop then dispose in reverse registration order, then release the
		// container — disposing the LoggerFactory flushes latest.log.
		// NOTE: array.Reverse() would bind to System.MemoryExtensions.Reverse
		// (Span, returns void) instead of LINQ's Enumerable.Reverse — System.Memory
		// hijacks it. An explicit reverse-index loop avoids the ambiguity.
		for (var i = _cuoServices.Length - 1; i >= 0; i--)
			RunLifecycle(_cuoServices[i], "Stop", s => s.Stop());
		for (var i = _cuoServices.Length - 1; i >= 0; i--)
			RunLifecycle(_cuoServices[i], "Dispose", s => s.Dispose());

		if (_services != null)
			_services.Dispose();
	}
}
