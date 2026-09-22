using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;
// The CUO Application layer is a sibling namespace inside CasualtiesUnknownOnline,
// and a namespace member beats any using-alias, so bare `Application` here binds to
// that namespace instead of Unity's type; the alias renames the Unity type.
using UnityApplication = UnityEngine.Application;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The BepInEx 5 entry: host configuration, the Unity lifecycle forwarded into the
/// container's <c>ICuoService</c> pump, and the Steam callbacks on the launch path.
/// Registration belongs to <see cref="PluginDependencyRegistrar"/> (which calls the
/// adapter's own composition), the Online UI to <see cref="OnlineUiHost"/>, and the
/// lobby-switch policy to <see cref="LobbySwitchActions"/> — the shell itself names
/// no adapter type, only the capability ports it consumes.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger = null!;

	private ServiceProvider _services = null!;
	private ICuoService[] _cuoServices = [];
	private ILogger<Plugin> _log = null!;
	private SteamService _steam = null!;
	private LobbySwitchActions _lobby = null!;
	private OnlineUiHost _onlineUi = null!;
	// The capability ports (review/adapter-capability-ports.md): the shell resolves
	// the port whose capability it calls and never the concrete adapter.
	private IGameIntegrationLifecycle? _lifecycle;
	private ICarryPresentationPump? _carryPump;
	private IJoinFlowPresentation? _joinFlow;
	private IAdapterCapabilityQuery? _capabilityQuery;
	private ConfigEntry<string> _interactionPanelKey = null!;
	private ulong? _pendingJoinLobbyId;

	private void Awake()
	{
		Logger = base.Logger;
		NativeLibraryPreloader.Preload(Logger);

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
				// The host's per-mod state saves persist under BepInEx/config;
				modStateFile: Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.mod-state.bin"),
				// The host's ban list persists in the same config directory;
				// it is written only by the host's HostBanService.
				hostBanFile: Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.host-bans.bin"),
				// The CUO world archive lives under the game's persistent-data root,
				// never in the install folder and never in save.sv (decisions 164/165).
				savesRoot: Path.Combine(UnityApplication.persistentDataPath, "cuo", SaveArchiveFormat.SavesFolderName),
				gameBuild: UnityApplication.version,
				extraRegistrations: services => PluginDependencyRegistrar.Apply(Config, services));

			_log = _services.GetRequiredService<ILogger<Plugin>>();
			_steam = _services.GetRequiredService<SteamService>();
			_lifecycle = _services.GetService<IGameIntegrationLifecycle>();
			_carryPump = _services.GetService<ICarryPresentationPump>();
			_joinFlow = _services.GetService<IJoinFlowPresentation>();
			_capabilityQuery = _services.GetService<IAdapterCapabilityQuery>();

			// Publish the container on the static diagnostics seam (HotRepl etc.).
			CuoBootstrap.Services = _services;

			// Multiplayer games must keep running when the window loses focus.
			UnityApplication.runInBackground = true;

			// The legacy F8/F9/F7 session hotkeys and TargetLobbyId were retired
			// in favor of the visual Online UI. The F6 quick-panel toggle remains
			// configurable.
			_interactionPanelKey = Config.Bind("Session", "InteractionPanelKey", "F6",
				"Hotkey to toggle the standalone player-interaction quick panel. See UnityEngine.KeyCode names.");

			_lobby = new LobbySwitchActions(
				_steam,
				_services.GetRequiredService<CuoNetworkRouter>(),
				_services.GetRequiredService<SessionService>(),
				_services.GetService<IWorldPresenceQuery>(),
				_services.GetRequiredService<ILocalizationService>(),
				_services.GetRequiredService<ILogger<LobbySwitchActions>>());
			_onlineUi = new OnlineUiHost(_services, _interactionPanelKey, _lobby);

			_cuoServices = [.. _services.GetServices<ICuoService>()];

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
				// through the intro). The shell states the intent; how the intro is
				// presented stays the adapter's business.
				_joinFlow?.PrepareForDirectJoin();
				_log.LogInformation("+connect_lobby {LobbyId} on the command line.", _pendingJoinLobbyId.Value);
			}

			// Wire events BEFORE Initialize — callbacks may fire immediately.
			_steam.LobbyCreated += lobbyId => _log.LogInformation("Lobby created: {LobbyId}", lobbyId);
			_steam.LobbyEntered += lobbyId =>
			{
				_lobby.LastError = null;
				_log.LogInformation("Lobby entered: {LobbyId}", lobbyId);
			};
			// Steam friends "Join Game" (right-click → join) fires
			// GameLobbyJoinRequested_t — auto-join, no TargetLobbyId config needed.
			_steam.JoinRequested += lobbyId =>
			{
				_log.LogInformation("Join requested via Steam friends — joining lobby {LobbyId}.", lobbyId);
				if (_lobby.CanJoin())
				{
					_steam.JoinLobby(lobbyId);
				}
			};
			// Join failures (lobby gone, full, ...) surface on the test HUD;
			// before this they were silent (LobbyEnter_t carries failures only
			// in its response code, which we now surface).
			_steam.LobbyJoinFailed += (lobbyId, reason) =>
			{
				_log.LogWarning("Lobby {LobbyId} join failed: {Reason}", lobbyId, reason);
				_lobby.LastError = $"Join {lobbyId} failed: {reason}";
			};

			// Forward Unity log messages into CUO's own log so runtime errors
			// (which BepInEx's DiskLogListener may not capture) are visible.
			UnityApplication.logMessageReceived += OnUnityLogMessage;

			foreach (var service in _cuoServices)
			{
				RunLifecycle(service, "Initialize", s => s.Initialize());
			}

			// Consume +connect_lobby now that Steam is up. If initialization
			// failed, F8 retries it and joins the pending lobby then (Update).
			if (_pendingJoinLobbyId is not null && _steam.IsInitialized)
			{
				if (_lobby.CanJoin())
				{
					_steam.JoinLobby(_pendingJoinLobbyId.Value);
				}

				_pendingJoinLobbyId = null;
			}

			foreach (var service in _cuoServices)
			{
				RunLifecycle(service, "Start", s => s.Start());
			}

			_log.LogInformation("Plugin {PluginGuid} is loaded!", MyPluginInfo.PLUGIN_GUID);
			if (_capabilityQuery is not null)
			{
				_log.LogInformation("Game Adapter: {Report}", _capabilityQuery.CapabilityReport);
			}

			if (_steam.IsInitialized)
			{
				_log.LogInformation("CUO Online UI: Steam lobby controls are available in the top-right Online UI.");
			}
			else
			{
				_log.LogWarning("CUO: Steam not initialized — lobby features unavailable. Use the Online UI to retry.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError($"CUO startup failed: {ex}");
		}
	}

	// The Unity lifecycle half: every stage forwards to the container's services,
	// which is all this shell does with the frame.
	private void Update()
	{
		foreach (var service in _cuoServices)
		{
			RunLifecycle(service, "Update", s => s.Update());
		}

		_onlineUi.Update();
	}

	// The carry presentation must be pinned after every frame's Update phase (game
	// Body updates, CUO renderer, Body.Update postfix). The only Unity phase after
	// Update and before render is LateUpdate, so that is where the final
	// local-carrier/rider attach pass belongs.
	private void LateUpdate() => _carryPump?.PinCarriedPresentation();

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

	// The Online UI overlay (IMGUI): lobby create/join panel, member status,
	// nameplates and off-screen arrows — composed and drawn by OnlineUiHost.
	private void OnGUI() => _onlineUi.Draw();

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
	private void OnApplicationQuit() => _lifecycle?.OnApplicationQuit();

	// SteamManager guidance: never do Steamworks work in OnDestroy (execution
	// order is not guaranteed); OnDisable is the safe teardown point.
	private void OnDisable()
	{
		UnityApplication.logMessageReceived -= OnUnityLogMessage;

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
