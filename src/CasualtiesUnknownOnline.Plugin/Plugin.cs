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
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;
using GameAdapterImpl = CasualtiesUnknownOnline.GameAdapter.GameAdapter;

namespace CasualtiesUnknownOnline;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger = null!;

	private ServiceProvider _services = null!;
	private ICuoService[] _cuoServices = [];
	private ILogger<Plugin> _log = null!;
	private SteamService _steam = null!;
	private SessionService _session = null!;
	private IGameAdapter? _adapter;
	private ConfigEntry<string> _targetLobbyId = null!;

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
			{
				Logger.LogWarning($"CUO: LoadLibrary failed for {path} (Win32 error {Marshal.GetLastWin32Error()})");
			}
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
			// The Game Adapter registers itself last so it resolves last (it binds
			// session events on Initialize).
			_services = CuoBootstrap.BuildServiceProvider(
				Logger,
				Path.Combine(Paths.BepInExRootPath, "logs"),
				legacyLogPath: Path.Combine(Paths.BepInExRootPath, "CUO.log"),
				extraRegistrations: services =>
				{
					// Character-data mapping (Mapster). Mapster 6.0.0 core ships
					// IMapper/Mapper — registered directly, no DI package needed
					// (Mapster.DependencyInjection 10.x requires net6+).
					services.AddSingleton<MapsterMapper.IMapper>(
						new MapsterMapper.Mapper(Mapster.TypeAdapterConfig.GlobalSettings));
					services.AddSingleton<GameAdapterImpl>();
					services.AddSingleton<IGameAdapter>(p => p.GetRequiredService<GameAdapterImpl>());
					services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterImpl>());
				});

			_log = _services.GetRequiredService<ILogger<Plugin>>();
			_steam = _services.GetRequiredService<SteamService>();
			_session = _services.GetRequiredService<SessionService>();
			_adapter = _services.GetService<IGameAdapter>();
			_cuoServices = _services.GetServices<ICuoService>().ToArray();

			// Multiplayer games must keep running when the window loses focus.
			Application.runInBackground = true;

			_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
				"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");

			// Wire events BEFORE Initialize — callbacks may fire immediately.
			_steam.LobbyCreated += lobbyId => _log.LogInformation("Lobby created: {LobbyId}", lobbyId);
			_steam.LobbyEntered += lobbyId => _log.LogInformation("Lobby entered: {LobbyId}", lobbyId);
			// Steam friends "Join Game" (right-click → join) fires
			// GameLobbyJoinRequested_t — auto-join, no TargetLobbyId config needed.
			_steam.JoinRequested += lobbyId =>
			{
				_log.LogInformation("Join requested via Steam friends — joining lobby {LobbyId}.", lobbyId);
				_steam.JoinLobby(lobbyId);
			};

			// Forward Unity log messages into CUO's own log so runtime errors
			// (which BepInEx's DiskLogListener may not capture) are visible.
			Application.logMessageReceived += OnUnityLogMessage;

			foreach (var service in _cuoServices)
			{
				RunLifecycle(service, "Initialize", s => s.Initialize());
			}

			foreach (var service in _cuoServices)
			{
				RunLifecycle(service, "Start", s => s.Start());
			}

			_log.LogInformation("Plugin {PluginGuid} is loaded!", MyPluginInfo.PLUGIN_GUID);
			if (_adapter is not null)
			{
				_log.LogInformation("Game Adapter: {Report}", _adapter.CapabilityReport);
			}

			if (_steam.IsInitialized)
			{
				_log.LogInformation("CUO Phase 1 test keys: F8 = create lobby, F9 = join lobby from config, F7 = ping peer.");
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
		{
			RunLifecycle(service, "Update", s => s.Update());
		}

		if (_steam is { } steam)
		{
			if (Input.GetKeyDown(KeyCode.F8))
			{
				// Retry path: if load-time init failed (Steam not running yet),
				// F8 re-attempts initialization, then creates the lobby.
				if (EnsureSteamReady(steam))
				{
					steam.CreateLobby();
				}
			}
			else if (Input.GetKeyDown(KeyCode.F9))
			{
				if (EnsureSteamReady(steam) && ulong.TryParse(_targetLobbyId.Value, out var lobbyId))
				{
					steam.JoinLobby(lobbyId);
				}
			}
			else if (Input.GetKeyDown(KeyCode.F7))
			{
				_session.RequestPing();
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

	// Phase-1 test HUD (IMGUI, temporary): replace with real UI in later phases.
	private void OnGUI()
	{
		var y = 10f;
		Line("CUO Phase 1 — Steam: " + (_steam.IsInitialized ? "initialized" : "not initialized"));
		if (_steam.IsInitialized)
		{
			Line($"SteamID: {_steam.LocalSteamId}");
			Line($"Lobby: {_steam.CurrentLobbyId}  Members: {_steam.GetLobbyMembers().Length}");
		}

		var role = _session.Role == SessionRole.Host ? "HOST"
			: _session.Role == SessionRole.Guest ? "GUEST" : "—";
		Line($"Session: {role}  handshake: {(_session.SessionActive ? "yes" : "no")}  "
			+ $"entity sync: {(_session.EntitySyncActive ? "ON" : "off")}");
		var remote = _session.RemotePlayer;
		if (remote is not null)
		{
			Line($"Remote: {remote.SteamId:X}  pos: ({remote.Position.X:F1}, {remote.Position.Y:F1})  inWorld: {remote.InWorld}");
		}

		Line(_session.LastRttMs >= 0f ? $"Last RTT: {_session.LastRttMs:F1} ms" : "No ping yet");
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
		{
			RunLifecycle(_cuoServices[i], "Stop", s => s.Stop());
		}

		for (var i = _cuoServices.Length - 1; i >= 0; i--)
		{
			RunLifecycle(_cuoServices[i], "Dispose", s => s.Dispose());
		}

		_services?.Dispose();
	}
}
