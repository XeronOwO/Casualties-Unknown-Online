using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
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
	private readonly ConfigurationProfileStore _profiles;
	private readonly INativeInputBlocker? _inputBlocker;
	private readonly IPlayerAnchorQuery? _anchorQuery;
	private readonly IWorldPresenceQuery? _worldPresence;
	private readonly IWorldLibrary? _worldLibrary;
	private readonly IStartGateState? _gateState;
	private readonly CuoEscCloseSuppression _escCloseSuppression = new();
	// The one-shot read-only probe of the game's own UI (S1 of the Online UI overhaul) — the adapter
	// reads, this class only decides nothing and the Runtime's policy decides when to stop.
	private readonly OnlineUiNativeFactsProbe _nativeFacts;
	// The LIVE native surface (S2a): the game's own launcher control, which this class drives with a
	// frame per update and reads back as intents. Optional like every adapter port — without one the
	// Online UI is simply unreachable from the game's UI.
	private readonly IOnlineUiSurface? _surface;
	private readonly OnlineUiLauncherFade _launcherFade = new();

	// This frame's context, assembled once in Update and read again by the IMGUI pass (OnGUI runs after
	// Update in the same frame), so the window's model and the surfaces drawn here see one set of facts.
	private OnlineUiContext? _context;

	private bool _launcherHovered;
	private string _launcherCaption = "";
	private bool _launcherLabelOpen;
	private string? _launcherLabel;

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
		_profiles = services.GetRequiredService<ConfigurationProfileStore>();
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
		_surface = services.GetService<IOnlineUiSurface>();
		_nativeFacts = new OnlineUiNativeFactsProbe(
			services.GetRequiredService<ILogger<OnlineUiNativeFactsProbe>>(),
			services.GetService<IOnlineUiNativeFactsQuery>());

		var ipConfig = services.GetRequiredService<IpDirectConfigEditor>();
		var colorConfig = services.GetRequiredService<PlayerColorConfigEditor>();
		var ipSteam = _router.IpDirectSteam;
		ipSteam.SetDisplayName(ipConfig.DisplayName);
		_router.SetLocalPlayerColor(colorConfig.CurrentColor);
		if (!colorConfig.IsStoredValueReadable)
		{
			// The one unreadable-but-present shape there is: a hand-edited value, or one written by a build
			// whose form changed. It reads as automatic from here on, which the player must be able to see.
			_log.LogWarning(
				"Online UI: the stored player colour `{Stored}` is not a colour — expected #RRGGBB or #RRGGBBAA; the marker colour is automatic until a new one is picked.",
				colorConfig.StoredHex);
		}

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
			ChangePlayerColor = color =>
			{
				// The chosen colour (null = automatic) is stored, then handed to the identity adapters so
				// the handshake and every later roster announcement carry it — the same three steps a
				// palette choice used to make.
				colorConfig.SetColor(color);
				_router.SetLocalPlayerColor(color);
				_session.ReportLocalPlayerColor(color);
			},
			Profiles = _profiles,
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

		// S1's read-only probe of the game's own UI: it needs no surface of its own, asks at most once
		// per interval, and stops for good once it has a complete reading (see the Runtime policy).
		_nativeFacts.Update(_time.NowMs);

		_onlineUi.IpDirectActive = _router.IsIpDirectActive;
		if (_ipActions.LastError is not null)
		{
			_lobby.LastError = _ipActions.LastError;
		}

		var ctx = _context = BuildContext();

		// S2a's live surface, and S2b's window family on it: what the player did on the game's own
		// controls first (so a click this frame is already reflected in the caption and the model
		// below), then this frame's state — the window's model is built with the actions that answer it.
		DrainSurfaceIntents();
		PushSurfaceFrame(ctx);
	}

	/// <summary>
	/// The frame's context: the runtime facts and the action delegates the Online UI reads. It is
	/// assembled in one place because both consumers need the same one — the window's model build (here
	/// in Update) and the IMGUI pass (in Draw) — and because the delegates are the same guarded paths the
	/// Steam callbacks use: one lobby-switch policy, two entry points.
	/// </summary>
	private OnlineUiContext BuildContext() => new()
	{
		Steam = _steam,
		Session = _session,
		Entities = _entities,
		Vitals = _remoteVitals,
		Inventory = _remoteInventory,
		PlayerInteraction = _playerInteraction,
		Visibility = _interactionVisibility,
		HostBan = _hostBan,
		HostRules = _hostRules,
		Commands = _commands,
		LocationPings = _locationPings,
		Time = _time,
		Localization = _localization,
		RulesEditor = _rulesEditor,
		Logging = _loggingEditor,
		Language = _languageEditor,
		Profiles = _profiles,
		AnchorQuery = _anchorQuery,
		WorldPresence = _worldPresence,
		WorldLibrary = _worldLibrary,
		LastJoinError = _lobby.LastError,
		State = _onlineUi.Window.State,
		JoinLobby = _onlineUi.JoinLobby,
		CreateLobby = _onlineUi.CreateLobby,
		LeaveLobby = _onlineUi.LeaveLobby,
		CreateIpHost = _onlineUi.CreateIpHost,
		JoinIp = _onlineUi.JoinIp,
		LeaveIp = _onlineUi.LeaveIp,
		IpConfig = _onlineUi.IpConfig,
		ColorConfig = _onlineUi.ColorConfig,
		ChangePlayerColor = _onlineUi.ChangePlayerColor,
		IpDirectActive = _router.IsIpDirectActive,
		TakeItem = _onlineUi.TakeItem,
		OpenRemoteBackpack = _onlineUi.OpenRemoteBackpack,
		OpenRemoteMedical = _onlineUi.OpenRemoteMedical,
		CarryRemote = _onlineUi.CarryRemote,
		PiggybackRemote = _onlineUi.PiggybackRemote,
		CarryOnBackRemote = _onlineUi.CarryOnBackRemote,
		DropCarried = _onlineUi.DropCarried,
		HealRemote = _onlineUi.HealRemote,
		HealWithItem = _onlineUi.HealWithItem,
		PushRemote = _onlineUi.PushRemote,
		RecruitPlayer = _onlineUi.RecruitPlayer,
		KickMember = _onlineUi.KickMember,
		BanMember = _onlineUi.BanMember,
		UnbanMember = _onlineUi.UnbanMember,
		GetLocalHealItems = _onlineUi.GetLocalHealItems,
		HasHealItem = _onlineUi.HasHealItem,
	};

	/// <summary>
	/// Drains what the player did on the native surface and turns it into the same calls the IMGUI
	/// controls made before they moved onto the game's own controls: a launcher click toggles the window,
	/// a hover flips the idle fade's only other input, the window's own close control closes it, and every
	/// page control's interaction lands on the action registered under its id.
	/// </summary>
	private void DrainSurfaceIntents()
	{
		if (_surface is null)
		{
			return;
		}

		while (_surface.TryDequeueIntent(out var intent))
		{
			switch (intent.Kind)
			{
				case OnlineUiIntentKind.LauncherToggled:
					_onlineUi.ToggleWindow(_session.Role);
					break;
				case OnlineUiIntentKind.LauncherHoverEntered:
					_launcherHovered = true;
					break;
				case OnlineUiIntentKind.LauncherHoverLeft:
					_launcherHovered = false;
					break;
				case OnlineUiIntentKind.WindowHoverEntered:
					_onlineUi.SetPointerOverWindow(true);
					break;
				case OnlineUiIntentKind.WindowHoverLeft:
					_onlineUi.SetPointerOverWindow(false);
					break;
				case OnlineUiIntentKind.ControlInvoked:
				case OnlineUiIntentKind.ControlToggled:
				case OnlineUiIntentKind.ControlSelected:
				case OnlineUiIntentKind.ControlChanged:
				case OnlineUiIntentKind.ControlEdited:
					ApplyControlIntent(intent);
					break;
			}
		}
	}

	/// <summary>
	/// Runs the action the intent's control id was registered under. An id the current model does not
	/// carry means the control is gone (the page changed, the member left between the click and this
	/// frame): the intent is dropped and logged rather than applied to whatever took its place.
	/// </summary>
	private void ApplyControlIntent(OnlineUiIntent intent)
	{
		if (!_onlineUi.Window.Apply(intent))
		{
			_log.LogDebug(
				"Online UI: an intent for the control `{Control}` arrived after the window stopped offering it — dropped.",
				intent.ControlId);
		}
	}

	/// <summary>
	/// The surface's frame: the launcher's caption and the opacity the idle rule derives from the pointer
	/// fact it reported, plus the window's model while the window is open. The caption and the model are
	/// rebuilt only when their inputs change — the launcher holds one caption for seconds at a time, and
	/// concatenating it on every update would allocate for nothing (the same discipline the IMGUI
	/// launcher's cached label had).
	/// </summary>
	private void PushSurfaceFrame(OnlineUiContext ctx)
	{
		if (_surface is null)
		{
			return;
		}

		var caption = _localization.T("launcher");
		var open = _onlineUi.IsWindowVisible;
		var label = _launcherLabel;
		if (label is null || _launcherCaption != caption || _launcherLabelOpen != open)
		{
			_launcherCaption = caption;
			_launcherLabelOpen = open;
			label = _launcherLabel = OnlineUiLauncherText.Label(caption, open);
		}

		_surface.Push(new OnlineUiFrame(label, _launcherFade.Evaluate(_time.NowMs, _launcherHovered), _onlineUi.Window.Build(ctx)));
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

		_onlineUi.Draw(_context ??= BuildContext(), _inputBlocker);
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
