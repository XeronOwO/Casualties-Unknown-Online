using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
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
	private EntitySyncService _entities = null!;
	private IGameAdapter? _adapter;
	private ConfigEntry<string> _targetLobbyId = null!;
	private ulong? _pendingJoinLobbyId;
	private string? _lastJoinError;

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
			_entities = _services.GetRequiredService<EntitySyncService>();
			_adapter = _services.GetService<IGameAdapter>();
			_cuoServices = [.. _services.GetServices<ICuoService>()];

			// Publish the container on the static diagnostics seam (HotRepl etc.).
			CuoBootstrap.Services = _services;

			// Multiplayer games must keep running when the window loses focus.
			Application.runInBackground = true;

			_targetLobbyId = Config.Bind("Session", "TargetLobbyId", "",
				"Lobby ID to join with F9 (printed by the host on F8). Leave empty to host only.");

			// Steam friends "Join Game" with the game not running launches it
			// with "+connect_lobby <id>" on the command line. GameLobbyJoinRequested_t
			// also fires once Steam initializes, but the command line is
			// timing-independent — join from it directly (consumed after Steam
			// init below) and keep the callback as the already-running fallback.
			_pendingJoinLobbyId = ParseConnectLobbyArg();
			if (_pendingJoinLobbyId is not null)
			{
				// Right-click "Join Game": the menu's content-warning/intro screen
				// is skipped (the follow-host pump then starts the run as soon as
				// PreRunScript exists instead of waiting for the player to click
				// through the intro).
				GameAdapterImpl.SkipIntro = true;
				_log.LogInformation("+connect_lobby {LobbyId} on the command line.", _pendingJoinLobbyId.Value);
			}

			// Wire events BEFORE Initialize — callbacks may fire immediately.
			_steam.LobbyCreated += lobbyId => _log.LogInformation("Lobby created: {LobbyId}", lobbyId);
			_steam.LobbyEntered += lobbyId =>
			{
				_lastJoinError = null;
				_log.LogInformation("Lobby entered: {LobbyId}", lobbyId);
			};
			// Steam friends "Join Game" (right-click → join) fires
			// GameLobbyJoinRequested_t — auto-join, no TargetLobbyId config needed.
			_steam.JoinRequested += lobbyId =>
			{
				_log.LogInformation("Join requested via Steam friends — joining lobby {LobbyId}.", lobbyId);
				_steam.JoinLobby(lobbyId);
			};
			// Join failures (lobby gone, full, ...) surface on the test HUD;
			// before this they were silent (LobbyEnter_t carries failures only
			// in its response code, which we now surface).
			_steam.LobbyJoinFailed += (lobbyId, reason) =>
			{
				_log.LogWarning("Lobby {LobbyId} join failed: {Reason}", lobbyId, reason);
				_lastJoinError = $"Join {lobbyId} failed: {reason}";
			};

			// Forward Unity log messages into CUO's own log so runtime errors
			// (which BepInEx's DiskLogListener may not capture) are visible.
			Application.logMessageReceived += OnUnityLogMessage;

			foreach (var service in _cuoServices)
			{
				RunLifecycle(service, "Initialize", s => s.Initialize());
			}

			// Consume +connect_lobby now that Steam is up. If initialization
			// failed, F8 retries it and joins the pending lobby then (Update).
			if (_pendingJoinLobbyId is not null && _steam.IsInitialized)
			{
				_steam.JoinLobby(_pendingJoinLobbyId.Value);
				_pendingJoinLobbyId = null;
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
				// F8 re-attempts initialization, then creates the lobby — or
				// joins the +connect_lobby target that was pending.
				if (EnsureSteamReady(steam))
				{
					if (_pendingJoinLobbyId is { } pending)
					{
						_pendingJoinLobbyId = null;
						steam.JoinLobby(pending);
					}
					else
					{
						steam.CreateLobby();
					}
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

	// Steam launches the game with "+connect_lobby <id>" when the user clicks
	// a friend's "Join Game" while the game is not running. Parse the lobby ID
	// from the command line so the join works without waiting for the
	// GameLobbyJoinRequested_t callback (whose IPC delivery can lag or fail).
	private static ulong? ParseConnectLobbyArg()
	{
		var args = Environment.GetCommandLineArgs();
		for (var i = 0; i < args.Length - 1; i++)
		{
			if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase)
				&& ulong.TryParse(args[i + 1], out var lobbyId))
			{
				return lobbyId;
			}
		}

		return null;
	}

	// Phase-1 test HUD (IMGUI, temporary): replace with real UI in later phases.
	private void OnGUI()
	{
		if (_adapter is { IsWaitingForReady: true })
		{
			DrawWaitingOverlay();
			return; // the HUD is hidden behind the gate overlay
		}

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
			+ $"entity sync: {(_entities.EntitySyncActive ? "ON" : "off")}");
		foreach (var remote in _entities.RemotePlayers)
		{
			Line($"Remote: {remote.SteamId:X}  pos: ({remote.Position.X:F1}, {remote.Position.Y:F1})  "
				+ $"inWorld: {_session.IsRemoteInWorld(remote.SteamId)}");
		}

		Line(_session.LastRttMs >= 0f ? $"Last RTT: {_session.LastRttMs:F1} ms" : "No ping yet");
		if (_lastJoinError is not null)
		{
			Line(_lastJoinError);
		}

		Line("F8 create lobby / F9 join from config / F7 ping peer");

		void Line(string text)
		{
			GUI.Label(new Rect(10f, y, 900f, 20f), text);
			y += 20f;
		}
	}

	/// <summary>Start-gate overlay: the waiting text over the LIVE frozen world —
	/// NO full-screen blackout (the gate freezes the world behind it, the world
	/// is visible and reads as "waiting to start"; a black texture made the wait
	/// read as a black screen, "the black-screen wait").</summary>
	private void DrawWaitingOverlay()
	{
		var style = new GUIStyle(GUI.skin.label) { fontSize = 28 };
		GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f), _adapter!.WaitingText, style);
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

	// Unity broadcasts OnApplicationQuit BEFORE the scene teardown — the world
	// items' OnDestroy would otherwise report as player-operation destroys
	// while the session still looks alive (the echo wiped the host's world
	// items when a guest quit, #191).
	private void OnApplicationQuit() => _adapter?.OnApplicationQuit();

	// SteamManager guidance: never do Steamworks work in OnDestroy (execution
	// order is not guaranteed); OnDisable is the safe teardown point.
	private void OnDisable()
	{
		Application.logMessageReceived -= OnUnityLogMessage;

		// Stop in reverse registration order, then release the container — it
		// disposes every IDisposable singleton it created (ICuoService :
		// IDisposable), and disposing the LoggerFactory flushes latest.log.
		// NOTE: array.Reverse() would bind to System.MemoryExtensions.Reverse
		// (Span, returns void) instead of LINQ's Enumerable.Reverse — System.Memory
		// hijacks it. An explicit reverse-index loop avoids the ambiguity.
		for (var i = _cuoServices.Length - 1; i >= 0; i--)
		{
			RunLifecycle(_cuoServices[i], "Stop", s => s.Stop());
		}

		_services?.Dispose();
	}
}
