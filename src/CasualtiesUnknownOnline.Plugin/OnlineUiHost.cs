using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI's presentation host: the IMGUI overlay, its action delegates and
/// the frame-time input/modal rules that keep the overlay and the game's own input
/// from fighting, assembled once from the container. <see cref="Plugin"/> forwards
/// the Unity callbacks into <see cref="Update"/> and <see cref="Draw"/>; the shell
/// itself holds no widget, delegate or modal state.
/// </summary>
internal sealed class OnlineUiHost
{
	private readonly ILogger<OnlineUiHost> _log;
	private readonly LobbySwitchActions _lobby;
	private readonly ConfigEntry<string> _quickPanelKey;
	private readonly OnlineUiOverlay _onlineUi;
	private readonly IpDirectActions _ipActions;
	private readonly LocationPingInputHandler _locationPingInput;
	private readonly CuoNetworkRouter _router;
	private readonly SteamService _steam;
	private readonly SessionService _session;
	private readonly EntitySyncService _entities;
	private readonly RemoteVitalsService _remoteVitals;
	private readonly RemoteInventoryService _remoteInventory;
	private readonly PlayerInteractionService _playerInteraction;
	private readonly IPlayerInteractionVisibility _interactionVisibility;
	private readonly IHostBanService _hostBan;
	private readonly IHostRules _hostRules;
	private readonly ICommandControl _commands;
	private readonly ILocationPingControl _locationPings;
	private readonly ITimeSource _time;
	private readonly IModUiControl _modUiControl;
	private readonly ILocalizationService _localization;
	private readonly HostRulesConfigEditor _rulesEditor;
	private readonly LoggingConfigEditor _loggingEditor;
	private readonly LocalizationConfigEditor _languageEditor;
	private readonly INativeInputBlocker? _inputBlocker;
	private readonly IPlayerAnchorQuery? _anchorQuery;
	private readonly IWorldPresenceQuery? _worldPresence;
	private readonly IWorldLibrary? _worldLibrary;
	private readonly IStartGateState? _gateState;
	private readonly CuoEscCloseSuppression _escCloseSuppression = new();

	internal OnlineUiHost(IServiceProvider services, ConfigEntry<string> quickPanelKey, LobbySwitchActions lobby)
	{
		_lobby = lobby;
		_quickPanelKey = quickPanelKey;
		_log = services.GetRequiredService<ILogger<OnlineUiHost>>();
		_router = services.GetRequiredService<CuoNetworkRouter>();
		_steam = services.GetRequiredService<SteamService>();
		_session = services.GetRequiredService<SessionService>();
		_entities = services.GetRequiredService<EntitySyncService>();
		_remoteVitals = services.GetRequiredService<RemoteVitalsService>();
		_remoteInventory = services.GetRequiredService<RemoteInventoryService>();
		// Ensure the registry-backed remote-presentation domain is alive and
		// subscribed to the character-data stream even before any UI opens.
		_ = services.GetRequiredService<RemoteCharacterPresentationStore>();
		_playerInteraction = services.GetRequiredService<PlayerInteractionService>();
		_interactionVisibility = services.GetRequiredService<IPlayerInteractionVisibility>();
		_hostBan = services.GetRequiredService<IHostBanService>();
		_hostRules = services.GetRequiredService<IHostRules>();
		_commands = services.GetRequiredService<ICommandControl>();
		_locationPings = services.GetRequiredService<ILocationPingControl>();
		_time = services.GetRequiredService<ITimeSource>();
		_modUiControl = services.GetRequiredService<IModUiControl>();
		_localization = services.GetRequiredService<ILocalizationService>();
		_rulesEditor = services.GetRequiredService<HostRulesConfigEditor>();
		_loggingEditor = services.GetRequiredService<LoggingConfigEditor>();
		_languageEditor = services.GetRequiredService<LocalizationConfigEditor>();
		// The capability ports are optional here exactly as they are in the
		// container: a composition without an adapter draws the UI without them.
		_inputBlocker = services.GetService<INativeInputBlocker>();
		_anchorQuery = services.GetService<IPlayerAnchorQuery>();
		_worldPresence = services.GetService<IWorldPresenceQuery>();
		// The world library the Worlds page drives (decision 198): optional, exactly like the
		// adapter — a composition without a world repository has nothing to manage, and the
		// page says so instead of throwing.
		_worldLibrary = services.GetService<IWorldLibrary>();
		_gateState = services.GetService<IStartGateState>();

		var ipConfig = services.GetRequiredService<IpDirectConfigEditor>();
		var colorConfig = services.GetRequiredService<PlayerColorConfigEditor>();
		var ipSteam = _router.IpDirectSteam;
		ipSteam.SetDisplayName(ipConfig.DisplayName);
		_router.SetLocalPlayerColor(colorConfig.CurrentColor);

		var uiActions = new OnlineUiActions(
			_session,
			_hostBan,
			_playerInteraction,
			services.GetService<IRemoteInventoryPresentation>(),
			services.GetService<IRemoteMedicalPresentation>(),
			services.GetService<ITraderRecruitRequest>(),
			services.GetService<ILocalHealItemQuery>());
		_ipActions = new IpDirectActions(
			_router,
			ipSteam,
			ipConfig,
			_session,
			_worldPresence,
			_localization,
			services.GetRequiredService<ILogger<IpDirectActions>>());
		_onlineUi = new OnlineUiOverlay(services.GetRequiredService<ConsoleInputSession>())
		{
			// The UI delegates are the same guarded paths the Steam callbacks use
			// — one lobby-switch policy (_lobby), two entry points.
			JoinLobby = _lobby.TryJoin,
			CreateLobby = _lobby.TryCreate,
			LeaveLobby = _lobby.TryLeave,
			CreateIpHost = _ipActions.CreateHost,
			JoinIp = _ipActions.Join,
			LeaveIp = _ipActions.Leave,
			IpConfig = ipConfig,
			ColorConfig = colorConfig,
			ChangePlayerColor = index =>
			{
				colorConfig.SetColorIndex(index);
				var color = colorConfig.CurrentColor;
				_router.SetLocalPlayerColor(color);
				_session.ReportLocalPlayerColor(color);
			},
			Profiles = services.GetRequiredService<ConfigurationProfileStore>(),
			TakeItem = uiActions.TakeItemFromRemote,
			OpenRemoteBackpack = (id, name) => OpenNativeSurface(uiActions.OpenRemoteBackpackFromUi(id, name)),
			OpenRemoteMedical = (id, name) => OpenNativeSurface(uiActions.OpenRemoteMedicalFromUi(id, name)),
			CarryRemote = uiActions.CarryRemoteFromUi,
			PiggybackRemote = uiActions.PiggybackRemoteFromUi,
			CarryOnBackRemote = uiActions.CarryOnBackRemoteFromUi,
			DropCarried = uiActions.DropCarryFromUi,
			HealRemote = uiActions.HealRemoteFromUi,
			HasHealItem = uiActions.HasLocalHealItem,
			HealWithItem = uiActions.HealWithItemFromUi,
			GetLocalHealItems = uiActions.GetLocalHealItems,
			PushRemote = uiActions.PushRemoteFromUi,
			RecruitPlayer = uiActions.RecruitPlayerFromUi,
			KickMember = uiActions.KickMemberFromUi,
			BanMember = uiActions.BanMemberFromUi,
			UnbanMember = uiActions.UnbanMemberFromUi,
		};

		_locationPingInput = new LocationPingInputHandler(
			_session,
			_locationPings,
			_onlineUi,
			services.GetRequiredService<ILogger<LocationPingInputHandler>>());
	}

	/// <summary>
	/// The frame-time half of the presentation: the console and quick-panel hotkeys,
	/// the location-ping input, and the native input modal the game's own pause input
	/// must not see while a CUO surface owns the frame.
	/// </summary>
	internal void Update()
	{
		// `/` opens the standalone command console directly in game. While it is
		// open the same modal guard blocks background UI and game input so the
		// input box stays the only interactive surface.
		if (!_onlineUi.IsCommandConsoleOpen && Input.GetKeyDown(KeyCode.Slash))
		{
			_onlineUi.OpenCommandConsole();
		}

		_locationPingInput.TryHandle();

		// The command console, Online UI window, and quick panel can all close
		// on ESC inside OnGUI, while the game's native pause input runs in
		// Update. Depending on Unity event ordering, this Update may observe a
		// surface as already closed in the same frame the ESC is still active.
		// Keep the modal guard active for that first closed frame so
		// PlayerCamera.HandleInput cannot see the same ESC and open the pause
		// menu; the next frame clears it.
		var consoleOpen = _onlineUi.IsCommandConsoleOpen;
		var windowVisible = _onlineUi.IsWindowVisible;
		var quickPanelVisible = _onlineUi.IsQuickPanelVisible;
		var escCloseFrame = _escCloseSuppression.Update(consoleOpen, windowVisible, quickPanelVisible);
		if (escCloseFrame)
		{
			_log.LogInformation("CUO ESC-closing surface closed this frame — keeping native input modal for one frame to swallow the closing ESC.");
		}

		// Keep the game's background UI input suppressed while the Online UI
		// modal window or the standalone command console is open (IMGUI does
		// not participate in UGUI input). A non-modal quick panel is not in the
		// modal guard while open, but its pause-toggle suppression is set below;
		// its close frame is covered by escCloseFrame.
		if (_inputBlocker is { } inputBlocker)
		{
			inputBlocker.SetOnlineUiModal(windowVisible || consoleOpen || escCloseFrame);
			inputBlocker.SetOnlineUiEscapeSurfaceVisible(quickPanelVisible);
		}

		if (!_onlineUi.IsCommandConsoleOpen && HotkeyPressed(_quickPanelKey))
		{
			_onlineUi.ToggleQuickPanel();
		}
	}

	/// <summary>
	/// The IMGUI pass: the start gate owns the frame while it holds the player,
	/// otherwise the Online UI draws and every mod window follows it.
	/// </summary>
	internal void Draw()
	{
		if (_gateState is { IsWaitingForReady: true })
		{
			StartGateOverlay.Draw(_gateState.WaitingText);
			return; // the HUD is hidden behind the gate overlay
		}

		_onlineUi.IpDirectActive = _router.IsIpDirectActive;
		if (_ipActions.LastError is not null)
		{
			_lobby.LastError = _ipActions.LastError;
		}

		_onlineUi.Draw(_steam, _session, _entities, _remoteVitals, _remoteInventory, _playerInteraction, _interactionVisibility, _hostBan, _hostRules, _commands, _locationPings, _time, _inputBlocker, _anchorQuery, _worldPresence, _worldLibrary, _localization, _rulesEditor, _loggingEditor, _languageEditor, _lobby.LastError);
		ModUiDrawing.DrawAll(_modUiControl, e => _log.LogError(e, "Mod UI window threw while drawing."));
	}

	/// <summary>Gets the IMGUI window out of the way when a native remote surface opened.</summary>
	private bool OpenNativeSurface(bool opened)
	{
		if (opened)
		{
			_onlineUi.CloseWindow();
			_onlineUi.CloseQuickPanel();
		}

		return opened;
	}

	/// <summary>Returns true when the configured hotkey string maps to a valid Unity KeyCode and that key was pressed this frame.</summary>
	private static bool HotkeyPressed(ConfigEntry<string> entry)
	{
		return Enum.TryParse<KeyCode>(entry.Value, ignoreCase: true, out var key)
			&& Enum.IsDefined(typeof(KeyCode), key)
			&& Input.GetKeyDown(key);
	}
}
