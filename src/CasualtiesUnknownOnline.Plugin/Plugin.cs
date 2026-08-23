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
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
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
	private CuoNetworkRouter _router = null!;
	private IpDirectSteamService _ipSteam = null!;
	private IpDirectConfigEditor _ipConfig = null!;
	private IpDirectActions _ipActions = null!;
	private SessionService _session = null!;
	private IHostBanService _hostBan = null!;
	private IHostRules _hostRules = null!;
	private ILocalizationService _localization = null!;
	private HostRulesConfigEditor _rulesEditor = null!;
	private LoggingConfigEditor _loggingEditor = null!;
	private LocalizationConfigEditor _languageEditor = null!;
	private EntitySyncService _entities = null!;
	private RemoteVitalsService _remoteVitals = null!;
	private RemoteInventoryService _remoteInventory = null!;
	private PlayerInteractionService _playerInteraction = null!;
	private IModUiControl _modUiControl = null!;
	private IGameAdapter? _adapter;
	private ConfigEntry<string> _targetLobbyId = null!;
	private ulong? _pendingJoinLobbyId;
	private string? _lastJoinError;
	private OnlineUiOverlay _onlineUi = null!;

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
				// The host's guest-character saves persist under BepInEx/config —
				// a host restart (continue-run) restores reconnecting guests from
				// this file; a NEW run deletes it (RunCoordinator).
				characterDataFile: Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.character-data.bin"),
				// The host's per-mod state saves persist in the same config
				// directory; guests never write it (host is the only save
				// authority, enforced by ModService.State).
				modStateFile: Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.mod-state.bin"),
				// The host's ban list persists in the same config directory;
				// it is written only by the host's HostBanService.
				hostBanFile: Path.Combine(Paths.ConfigPath, "CasualtiesUnknownOnline.host-bans.bin"),
				extraRegistrations: services => PluginDependencyRegistrar.Apply(Config, services));

			_log = _services.GetRequiredService<ILogger<Plugin>>();
			_steam = _services.GetRequiredService<SteamService>();
			_router = _services.GetRequiredService<CuoNetworkRouter>();
			_ipSteam = _router.IpDirectSteam;
			_ipConfig = _services.GetRequiredService<IpDirectConfigEditor>();
			_ipSteam.SetDisplayName(_ipConfig.DisplayName);
			_session = _services.GetRequiredService<SessionService>();
			_hostBan = _services.GetRequiredService<IHostBanService>();
			_hostRules = _services.GetRequiredService<IHostRules>();
			_localization = _services.GetRequiredService<ILocalizationService>();
			_rulesEditor = _services.GetRequiredService<HostRulesConfigEditor>();
			_loggingEditor = _services.GetRequiredService<LoggingConfigEditor>();
			_languageEditor = _services.GetRequiredService<LocalizationConfigEditor>();
			_entities = _services.GetRequiredService<EntitySyncService>();
			_remoteVitals = _services.GetRequiredService<RemoteVitalsService>();
			_remoteInventory = _services.GetRequiredService<RemoteInventoryService>();
			_playerInteraction = _services.GetRequiredService<PlayerInteractionService>();
			_modUiControl = _services.GetRequiredService<IModUiControl>();
			_adapter = _services.GetService<IGameAdapter>();
			_ipActions = new IpDirectActions(
				_router,
				_ipSteam,
				_ipConfig,
				_session,
				_adapter,
				_localization,
				_services.GetRequiredService<ILogger<IpDirectActions>>());
			_cuoServices = [.. _services.GetServices<ICuoService>()];
			_onlineUi = new OnlineUiOverlay
			{
				// The UI delegates are the same guarded paths the F8/F9 hotkeys
				// use — one lobby-switch policy, two entry points.
				JoinLobby = TryJoinLobbyFromUi,
				CreateLobby = TryCreateLobbyFromUi,
				LeaveLobby = TryLeaveLobbyFromUi,
				CreateIpHost = _ipActions.CreateHost,
				JoinIp = _ipActions.Join,
				LeaveIp = _ipActions.Leave,
				IpConfig = _ipConfig,
				TakeItem = TryTakeItemFromRemote,
				CarryRemote = TryCarryRemoteFromUi,
				DropCarried = TryDropCarryFromUi,
				HealRemote = TryHealRemoteFromUi,
				HasHealItem = () => _adapter?.HasLocalHealItem() == true,
				HealWithItem = TryHealWithItemFromUi,
				GetLocalHealItems = () => _adapter?.GetLocalHealItems() ?? [],
				RecruitPlayer = TryRequestTraderRecruitFromUi,
				KickMember = TryKickMemberFromUi,
				BanMember = TryBanMemberFromUi,
				UnbanMember = TryUnbanMemberFromUi,
			};

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
				if (CanSwitchLobbyForJoin())
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
				if (CanSwitchLobbyForJoin())
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

		// Keep the game's background UI input suppressed while the Online UI
		// modal window is open (IMGUI does not participate in UGUI input).
		if (_adapter is { } adapter)
		{
			adapter.SetOnlineUiModal(_onlineUi.IsWindowVisible);
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
						if (CanSwitchLobbyForJoin())
						{
							steam.JoinLobby(pending);
						}
					}
					else if (CanSwitchLobbyForCreate())
					{
						steam.CreateLobby();
					}
				}
			}
			else if (Input.GetKeyDown(KeyCode.F9))
			{
				if (EnsureSteamReady(steam)
					&& CanSwitchLobbyForJoin()
					&& ulong.TryParse(_targetLobbyId.Value, out var lobbyId))
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

	/// <summary>Join policy: a lobby join always changes identity, so any active world/generation blocks it. The reason is visible on the test HUD.</summary>
	private bool CanSwitchLobbyForJoin()
	{
		if (_ipSteam.IsActive)
		{
			_lastJoinError = _localization.T("ip.blocked_active_session");
			_log.LogWarning("Steam lobby join refused: an IP-direct session is active.");
			return false;
		}

		if (_adapter is not { IsInWorldOrGenerating: true })
		{
			return true;
		}

		_lastJoinError = _localization.T("lobby.join_blocked_in_world");
		_log.LogWarning("Lobby join refused: a world is running or generating.");
		return false;
	}

	/// <summary>Create policy: menu is always allowed; in a world only the solo->host conversion is (no session, no identity change away from another host).</summary>
	private bool CanSwitchLobbyForCreate()
	{
		if (_ipSteam.IsActive)
		{
			_lastJoinError = _localization.T("ip.blocked_active_session");
			_log.LogWarning("Steam lobby create refused: an IP-direct session is active.");
			return false;
		}

		if (_adapter is not { IsInWorldOrGenerating: true })
		{
			return true;
		}

		if (LobbySwitchGuard.CanCreateLobby(_session.Role, _session.SessionActive, worldFlowActive: true))
		{
			return true;
		}

		_lastJoinError = _localization.T("lobby.join_blocked_in_world");
		_log.LogWarning("Lobby create refused: a sessioned world is running or generating.");
		return false;
	}

	/// <summary>Online UI Join button path — same guards as the F9 hotkey, with
	/// the lobby id coming from the text field instead of the config.</summary>
	private bool TryJoinLobbyFromUi(string lobbyId)
	{
		if (!EnsureSteamReady(_steam) || !CanSwitchLobbyForJoin() || !ulong.TryParse(lobbyId, out var lobbyIdValue))
		{
			return false;
		}

		_steam.JoinLobby(lobbyIdValue);
		return true;
	}

	/// <summary>Online UI Create button path — same policy as the F8 hotkey.</summary>
	private bool TryCreateLobbyFromUi()
	{
		if (!EnsureSteamReady(_steam) || !CanSwitchLobbyForCreate())
		{
			return false;
		}

		_steam.CreateLobby();
		return true;
	}

	/// <summary>Online UI Leave Lobby / Close Room path.</summary>
	private bool TryLeaveLobbyFromUi()
	{
		if (!EnsureSteamReady(_steam))
		{
			return false;
		}

		_steam.LeaveLobby();
		return true;
	}

	/// <summary>Online UI Take button path — forward to the host-authoritative player-interaction domain (guests send the request; the host handles it locally).</summary>
	private bool TryTakeItemFromRemote(ulong ownerSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendTakeRequest(ownerSteamId, itemInstanceId);
		return true;
	}

	/// <summary>Online UI Carry button path — forward to the host-authoritative carry domain (guests send the request; the host handles it locally).</summary>
	private bool TryCarryRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendCarryStartRequest(targetSteamId);
		return true;
	}

	/// <summary>Online UI Drop button path — forward to the host-authoritative carry domain.</summary>
	private bool TryDropCarryFromUi(ulong carriedSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendCarryStopRequest(carriedSteamId);
		return true;
	}

	/// <summary>Online UI Heal button path — forward to the host-authoritative heal domain (item instance 0 = host auto-select).</summary>
	private bool TryHealRemoteFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		_playerInteraction.SendHealRequest(targetSteamId, 0);
		return true;
	}

	/// <summary>Online UI explicit heal-item path — forward the chosen instance id to the host-authoritative heal domain.</summary>
	private bool TryHealWithItemFromUi(ulong targetSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive || itemInstanceId == 0)
		{
			return false;
		}

		_playerInteraction.SendHealRequest(targetSteamId, itemInstanceId);
		return true;
	}

	/// <summary>Online UI Recruit path — forward to the Game Adapter's trader-recruit coordinator (the host remains the authority).</summary>
	private bool TryRequestTraderRecruitFromUi(ulong targetSteamId)
	{
		if (!_session.SessionActive)
		{
			return false;
		}

		return _adapter?.TryRequestTraderRecruit(targetSteamId) == true;
	}

	/// <summary>Online UI Kick path — host-only session removal (the target receives a dedicated Kicked message).</summary>
	private bool TryKickMemberFromUi(ulong targetSteamId) => _session.KickMember(targetSteamId, "kicked by host");

	/// <summary>Online UI Ban path — host-only permanent removal (the target receives a dedicated Banned message and the SteamID is persisted on the host).</summary>
	private bool TryBanMemberFromUi(ulong targetSteamId) => _hostBan.Ban(targetSteamId, "banned by host");

	/// <summary>Online UI Unban path — host-only removal from the persisted ban list.</summary>
	private bool TryUnbanMemberFromUi(ulong targetSteamId) => _hostBan.Unban(targetSteamId);


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
	// nameplates and off-screen arrows — see OnlineUiOverlay.cs.
	private void OnGUI()
	{
		if (_adapter is { IsWaitingForReady: true })
		{
			DrawWaitingOverlay();
			return; // the HUD is hidden behind the gate overlay
		}

		_onlineUi.IpDirectActive = _router.IsIpDirectActive;
		if (_ipActions.LastError is not null)
		{
			_lastJoinError = _ipActions.LastError;
		}

		_onlineUi.Draw(_steam, _session, _entities, _remoteVitals, _remoteInventory, _playerInteraction, _hostBan, _hostRules, _adapter, _localization, _rulesEditor, _loggingEditor, _languageEditor, _lastJoinError);
		ModUiDrawing.DrawAll(_modUiControl, e => _log.LogError(e, "Mod UI window threw while drawing."));
	}

	/// <summary>
	/// Start-gate overlay: the waiting text in a translucent panel pinned to
	/// the BOTTOM-RIGHT corner (the loading screen's own info slot, #87) over
	/// the LIVE frozen world. No full-screen blackout: the gate freezes the
	/// world behind it and the panel keeps the wait readable without turning
	/// the wait into "a black screen" (the original black-texture attempt).
	/// </summary>
	private void DrawWaitingOverlay()
	{
		const float margin = 24f;
		const float height = 64f;
		var width = Screen.width - (margin * 2f);
		if (width < 1f)
		{
			return;
		}

		width = Mathf.Min(width, 520f);
		var rect = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);

		var previous = GUI.color;
		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.Box(rect, string.Empty);
		GUI.color = previous;

		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 20,
			alignment = TextAnchor.MiddleRight,
			padding = new RectOffset(0, 18, 0, 0),
		};
		style.normal.textColor = Color.white;
		GUI.Label(rect, _adapter!.WaitingText, style);
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
