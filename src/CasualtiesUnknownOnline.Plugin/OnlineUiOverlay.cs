using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI overlay. It owns the IMGUI composition (the new CUO Online
/// window and world nameplates/off-screen arrows).
/// The old top-left status/lobby/member dump is gone: the same runtime facts
/// are now presented through the tabbed <see cref="OnlineUiWindow"/>.
/// </summary>
internal sealed class OnlineUiOverlay
{
	/// <summary>Invoked when the user clicks Join with a numeric lobby id.</summary>
	internal Func<string, bool>? JoinLobby;

	/// <summary>Invoked when the user clicks Create Lobby.</summary>
	internal Func<bool>? CreateLobby;

	/// <summary>Invoked when the user clicks Leave Lobby / Close Room.</summary>
	internal Func<bool>? LeaveLobby;

	/// <summary>Invoked when the user clicks Create IP Host.</summary>
	internal Func<bool>? CreateIpHost;

	/// <summary>Invoked when the user clicks Join IP with an address/port.</summary>
	internal Func<string, int, bool>? JoinIp;

	/// <summary>Invoked when the user clicks Leave IP Direct.</summary>
	internal Func<bool>? LeaveIp;

	/// <summary>The IP-direct config editor (address/port/display name fields).</summary>
	internal IpDirectConfigEditor? IpConfig;

	/// <summary>The local player color config editor (palette selection).</summary>
	internal PlayerColorConfigEditor? ColorConfig;

	/// <summary>Invoked when the user changes the local player color palette index.</summary>
	internal Action<int>? ChangePlayerColor;

	/// <summary>Full CUO config template store (saved named BepInEx profiles).</summary>
	internal ConfigurationProfileStore? Profiles;

	/// <summary>Set by the plugin each frame; true while the router is on the IP-direct path.</summary>
	internal bool IpDirectActive;

	/// <summary>Invoked when the user clicks Take on one of a remote player's inventory lines.</summary>
	internal Func<ulong, ulong, bool>? TakeItem;

	/// <summary>Invoked when the user wants to open a remote player's inventory in the game's native radial backpack UI.</summary>
	internal Func<ulong, string, bool>? OpenRemoteBackpack;

	/// <summary>Invoked when the user clicks Carry on an unconscious/dead remote player.</summary>
	internal Func<ulong, bool>? CarryRemote;

	/// <summary>Invoked when the user clicks Piggyback on a conscious/alive remote player (local climbs onto them).</summary>
	internal Func<ulong, bool>? PiggybackRemote;

	/// <summary>Invoked when the user clicks Carry on back on a conscious/alive remote player (local is the carrier).</summary>
	internal Func<ulong, bool>? CarryOnBackRemote;

	/// <summary>Invoked when the user clicks Drop on the currently carried remote player.</summary>
	internal Func<ulong, bool>? DropCarried;

	/// <summary>Invoked when the user clicks Heal on an in-world remote player.</summary>
	internal Func<ulong, bool>? HealRemote;

	/// <summary>Read-only UI check: does the local body currently carry a heal-profile medical item?</summary>
	internal Func<bool>? HasHealItem;

	/// <summary>Invoked when the user clicks one of the explicit local heal items (instance id must be non-zero).</summary>
	internal Func<ulong, ulong, bool>? HealWithItem;

	/// <summary>Explicit local heal items for the Online UI selector (slot items with wire ids only).</summary>
	internal Func<IReadOnlyList<LocalHealItem>>? GetLocalHealItems;

	/// <summary>Invoked when the user clicks Push on an in-world remote player.</summary>
	internal Func<ulong, bool>? PushRemote;

	/// <summary>Invoked when the user clicks Recruit on a dead in-world teammate (trader-recruit co-op revive).</summary>
	internal Func<ulong, bool>? RecruitPlayer;

	/// <summary>Invoked when the host clicks Kick on a non-local lobby member.</summary>
	internal Func<ulong, bool>? KickMember;

	/// <summary>Invoked when the host clicks Ban on a non-local lobby member.</summary>
	internal Func<ulong, bool>? BanMember;

	/// <summary>Invoked when the host clicks Unban on a banned SteamID.</summary>
	internal Func<ulong, bool>? UnbanMember;

	private readonly OnlineUiWindow _window = new();

	private readonly OnlineUiPlayerContextMenu _contextMenu = new();

	private readonly OnlineUiQuickPanel _quickPanel = new();

	private readonly CommandConsoleOverlay _commandOverlay;

	internal OnlineUiOverlay(ConsoleInputSession consoleInput)
	{
		_commandOverlay = new CommandConsoleOverlay(consoleInput);
	}

	private const float StatusDelaySeconds = 1.5f;
	private const float StatusHoldSeconds = 15f;

	// Nameplate/off-screen marker style. The edge margin is deliberately larger
	// than the arrow box so markers keep an inner padding from the screen edge
	// (game UI can occupy the very edge of the screen).
	private const float ScreenEdgeMargin = 52f;
	private const int NameplateFontSize = 15;
	private const int OffScreenArrowFontSize = 22;
	private const int OffScreenNameFontSize = 13;

	private string? _statusMessage;
	private float _statusSetTime = float.NegativeInfinity;
	private bool _lastHadSession;

	internal bool IsWindowVisible => _window.State.Visible;

	internal bool IsQuickPanelVisible => _quickPanel.IsVisible;

	internal bool IsCommandConsoleOpen => _commandOverlay.IsOpen;

	/// <summary>Opens the standalone command console and closes every other CUO surface so the input state is modal.</summary>
	internal void OpenCommandConsole()
	{
		if (_commandOverlay.IsOpen)
		{
			return;
		}

		_window.State.Visible = false;
		_quickPanel.Close();
		_commandOverlay.Open();
	}

	/// <summary>Closes the standalone command console.</summary>
	internal void CloseCommandConsole() => _commandOverlay.Close();

	/// <summary>Programmatic close (ESC hotkey); the modal guard sees it on the next frame's adapter call.</summary>
	internal void CloseWindow() => _window.State.Visible = false;

	/// <summary>Closes the standalone player-interaction quick panel.</summary>
	internal void CloseQuickPanel() => _quickPanel.Close();

	/// <summary>Toggles the standalone player-interaction quick panel (configurable session hotkey).</summary>
	internal void ToggleQuickPanel() => _quickPanel.Toggle();

	/// <summary>Opens the standalone quick panel pinned to a right-clicked remote, expands that member's inventory immediately, and closes the full Online window so the independent panel is the active surface.</summary>
	internal void OpenQuickPanelFor(ulong steamId)
	{
		_window.State.Visible = false;
		_quickPanel.Open(steamId);
		_window.State.ExpandedMember = steamId;
	}

	internal void Draw(
		SteamService steam,
		SessionService session,
		EntitySyncService entities,
		RemoteVitalsService vitals,
		RemoteInventoryService inventory,
		IPlayerInteractionControl playerInteraction,
		IPlayerInteractionVisibility? interactionVisibility,
		IHostBanService hostBan,
		IHostRules hostRules,
		ICommandControl commands,
		IGameAdapter? adapter,
		ILocalizationService localization,
		HostRulesConfigEditor? rulesEditor,
		LoggingConfigEditor? logging,
		LocalizationConfigEditor? language,
		string? lastJoinError)
	{
		var ctx = new OnlineUiContext
		{
			Steam = steam,
			Session = session,
			Entities = entities,
			Vitals = vitals,
			Inventory = inventory,
			PlayerInteraction = playerInteraction,
			Visibility = interactionVisibility,
			HostBan = hostBan,
			HostRules = hostRules,
			Commands = commands,
			Localization = localization,
			RulesEditor = rulesEditor,
			Logging = logging,
			Language = language,
			Profiles = Profiles,
			Adapter = adapter,
			LastJoinError = lastJoinError,
			State = _window.State,
			JoinLobby = JoinLobby,
			CreateLobby = CreateLobby,
			LeaveLobby = LeaveLobby,
			CreateIpHost = CreateIpHost,
			JoinIp = JoinIp,
			LeaveIp = LeaveIp,
			IpConfig = IpConfig,
			ColorConfig = ColorConfig,
			ChangePlayerColor = ChangePlayerColor,
			IpDirectActive = IpDirectActive,
			TakeItem = TakeItem,
			OpenRemoteBackpack = OpenRemoteBackpack,
			CarryRemote = CarryRemote,
			PiggybackRemote = PiggybackRemote,
			CarryOnBackRemote = CarryOnBackRemote,
			DropCarried = DropCarried,
			HealRemote = HealRemote,
			HealWithItem = HealWithItem,
			PushRemote = PushRemote,
			RecruitPlayer = RecruitPlayer,
			KickMember = KickMember,
			BanMember = BanMember,
			UnbanMember = UnbanMember,
			GetLocalHealItems = GetLocalHealItems,
			HasHealItem = HasHealItem,
			OpenQuickPanel = OpenQuickPanelFor,
		};

		// ESC closes the modal Online UI. The native PlayerCamera.HandleInput
		// pause/menu handling is already short-circuited by the adapter when
		// the modal is open, and this OnGUI frame runs after Update, so the
		// same key cannot reach the game's pause path. The standalone command
		// overlay handles its own Escape below.
		var esc = Event.current;
		if (_commandOverlay.IsOpen == false
			&& _window.State.Visible
			&& esc != null
			&& esc.type == EventType.KeyDown
			&& esc.keyCode == KeyCode.Escape)
		{
			CloseWindow();
			esc.Use();
		}

		if (!_commandOverlay.IsOpen)
		{
			_window.Draw(ctx);
			UpdateDelayedStatus(ctx);
			DrawNetworkHud(ctx);
			DrawNameplatesAndArrows(ctx, entities);
			DrawPlayerContextMenu(ctx);
			_quickPanel.Draw(ctx);
		}

		_commandOverlay.Draw(ctx);

		// Non-modal CUO surfaces (quick panel, right-click context menu) are
		// IMGUI and invisible to UGUI; scoped blockers keep their pixels from
		// leaking to the menu/world without blocking the rest of the screen.
		// The command console is handled by the full modal guard, not scoped blocks.
		adapter?.SetOnlineUiScopedBlocks(_commandOverlay.IsOpen ? [] : CollectScopedBlocks());
	}

	private IReadOnlyList<OnlineUiBlockRect> CollectScopedBlocks()
	{
		var blocks = new List<OnlineUiBlockRect>(2);
		if (_contextMenu.IsOpen)
		{
			blocks.Add(FromRect(_contextMenu.Bounds));
		}

		if (_quickPanel.IsVisible)
		{
			blocks.Add(FromRect(_quickPanel.Bounds));
		}

		return blocks;
	}

	private static OnlineUiBlockRect FromRect(Rect rect) =>
		new(rect.x, rect.y, rect.width, rect.height);

	private void UpdateDelayedStatus(OnlineUiContext ctx)
	{
		var hadSession = ctx.IpDirectActive
			|| ctx.Session.SessionActive
			|| ctx.Session.Role != Runtime.Session.SessionRole.None;
		if (hadSession == _lastHadSession)
		{
			return;
		}

		_lastHadSession = hadSession;
		if (hadSession)
		{
			var message = ctx.IpDirectActive
				? ctx.T(ctx.Session.Role == Runtime.Session.SessionRole.Host ? "hud.ip_host_started" : "hud.ip_guest_joined")
				: ctx.T(ctx.Session.Role == Runtime.Session.SessionRole.Host ? "hud.steam_host_started" : "hud.steam_guest_joined");
			Notify(message);
		}
		else
		{
			Notify(ctx.T("hud.session_ended"));
		}
	}

	internal void Notify(string message)
	{
		_statusMessage = message;
		_statusSetTime = Time.realtimeSinceStartup;
	}

	private void DrawNetworkHud(OnlineUiContext ctx)
	{
		if (!ctx.IpDirectActive && ctx.Steam.CurrentLobbyId == 0 && ctx.Session.Role == Runtime.Session.SessionRole.None)
		{
			return;
		}

		// Minimal top-left readout: no background panel (the game shows the
		// hand-held item there), only the live RTT plus the latest delayed
		// session event. Full details are in the Online UI window.
		var rect = new Rect(8f, 8f, 220f, 48f);
		GUILayout.BeginArea(rect);
		var rtt = ctx.Session.LastRttMs >= 0f ? $"{ctx.Session.LastRttMs:F0} ms" : ctx.T("common.pending");
		GUILayout.Label($"{ctx.T("hud.rtt")}: {rtt}", OnlineUiTheme.MutedLabel());

		var elapsed = Time.realtimeSinceStartup - _statusSetTime;
		if (_statusMessage is not null && elapsed >= StatusDelaySeconds && elapsed <= StatusDelaySeconds + StatusHoldSeconds)
		{
			GUILayout.Label(_statusMessage, OnlineUiTheme.Status(OnlineUiTheme.Positive));
		}
		else if (_statusMessage is not null && elapsed > StatusDelaySeconds + StatusHoldSeconds)
		{
			_statusMessage = null;
		}

		GUILayout.EndArea();
	}

	private void DrawPlayerContextMenu(OnlineUiContext ctx)
	{
		HandleContextMenuInput(ctx);
		_contextMenu.Draw(ctx);
	}

	private void HandleContextMenuInput(OnlineUiContext ctx)
	{
		var evt = Event.current;
		if (evt == null || evt.type != EventType.MouseDown)
		{
			return;
		}

		var mouse = evt.mousePosition;
		if (evt.button == 1)
		{
			// Right-clicks inside the Online window belong to the UI, not the
			// world; never open/re-target/close the in-world menu from there.
			if (_window.ContainsPoint(mouse))
			{
				return;
			}

			// Right-clicks inside the standalone quick panel belong to that UI,
			// not the world; never open/re-target the in-world menu from there.
			if (_quickPanel.IsVisible && _quickPanel.Contains(mouse))
			{
				return;
			}

			// A right-click inside an already-open menu is left for the menu
			// buttons (or a future switch); it must not re-target/re-close.
			if (_contextMenu.IsOpen && _contextMenu.Contains(mouse))
			{
				return;
			}

			if (TryFindRemoteCandidatesAt(mouse, ctx, out var candidates))
			{
				_contextMenu.Open(candidates[0], candidates, mouse);
				evt.Use();
			}
			else if (_contextMenu.IsOpen)
			{
				_contextMenu.Close();
				evt.Use();
			}

			return;
		}

		if (evt.button == 0 && _contextMenu.IsOpen && !_contextMenu.Contains(mouse))
		{
			_contextMenu.Close();
		}
	}

	private static bool TryFindRemoteCandidatesAt(Vector2 guiMouse, OnlineUiContext ctx, out IReadOnlyList<ulong> steamIds)
	{
		var camera = Camera.main;
		if (camera == null)
		{
			steamIds = [];
			return false;
		}

		const float radius = 48f;
		var screenTargets = new List<RemoteScreenTarget>();
		foreach (var remote in ctx.Entities.RemotePlayers)
		{
			if (remote.IsLocal || !ctx.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			var world = new Vector3(remote.Position.X, remote.Position.Y, 0f);
			var screen = camera.WorldToScreenPoint(world);
			if (screen.z < 0f)
			{
				continue;
			}

			var gui = new Vector2(screen.x, Screen.height - screen.y);
			screenTargets.Add(new RemoteScreenTarget(remote.SteamId, gui.x, gui.y));
		}

		var matches = RemoteTargetPicker.Find(screenTargets, guiMouse.x, guiMouse.y, radius);
		var result = new List<ulong>(matches.Count);
		foreach (var match in matches)
		{
			result.Add(match.SteamId);
		}

		steamIds = result;
		return result.Count > 0;
	}

	private static void DrawNameplatesAndArrows(OnlineUiContext ctx, EntitySyncService entities)
	{
		var camera = Camera.main;
		if (camera == null)
		{
			return;
		}

		var local = entities.LocalPlayer.Position;
		foreach (var remote in entities.RemotePlayers)
		{
			if (remote.IsLocal || !ctx.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			// Prefer the live render clone's head limb so markers stay on top of
			// the head while standing/crouching/lying; fall back to the body's
			// authoritative position before the clone exists.
			var worldPoint = new Vector3(remote.Position.X, remote.Position.Y, 0f);
			if (ctx.Adapter?.TryGetRemoteHeadPosition(remote.SteamId, out var headX, out var headY) == true)
			{
				worldPoint = new Vector3(headX, headY, 0f);
			}

			var projected = camera.WorldToScreenPoint(worldPoint);
			// GUI y grows DOWN; WorldToScreenPoint y grows UP.
			var gui = new Vector2(projected.x, Screen.height - projected.y);
			var placement = OffScreenArrowGeometry.Place(gui.x, gui.y, Screen.width, Screen.height, ScreenEdgeMargin);

			var dx = remote.Position.X - local.X;
			var dy = remote.Position.Y - local.Y;
			var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
			var color = ToColor(ctx.PlayerColor(remote.SteamId));
			var name = ctx.DisplayName(remote.SteamId);
			if (placement.Direction == OffScreenArrowDirection.None)
			{
				DrawNameplate(placement.X, placement.Y, name, color);
			}
			else
			{
				DrawOffScreenArrow(placement, name, ctx.F("hud.distance", Mathf.RoundToInt(distance)), color);
			}
		}
	}

	private static void DrawNameplate(float x, float y, string name, Color color)
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = NameplateFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		style.normal.textColor = color;
		var rect = NameplateLayout.AboveHead(x, y);
		GUI.Label(new Rect(rect.X, rect.Y, rect.Width, rect.Height), name, style);
	}

	private static void DrawOffScreenArrow(OffScreenArrowPlacement placement, string name, string distanceText, Color color)
	{
		var arrowStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = OffScreenArrowFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		arrowStyle.normal.textColor = color;

		const float arrowSize = 32f;
		var arrow = placement.Direction switch
		{
			OffScreenArrowDirection.Up => "\u25B2",   // ▲
			OffScreenArrowDirection.Down => "\u25BC", // ▼
			OffScreenArrowDirection.Left => "\u25C0", // ◄
			OffScreenArrowDirection.Right => "\u25B6", // ►
			_ => "\u2022",                            // •
		};
		GUI.Label(new Rect(placement.X - (arrowSize * 0.5f), placement.Y - (arrowSize * 0.5f), arrowSize, arrowSize), arrow, arrowStyle);

		var nameStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = OffScreenNameFontSize,
			alignment = TextAnchor.MiddleCenter,
		};
		nameStyle.normal.textColor = color;
		GUI.Label(new Rect(placement.X - 80f, placement.Y + (arrowSize * 0.5f) + 4f, 160f, 20f), name + "  " + distanceText, nameStyle);
	}

	private static Color ToColor(PlayerColorValue value) => new(value.R, value.G, value.B, value.A);

}
