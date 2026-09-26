using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
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

	/// <summary>The local player colour config editor (the stored colour, or automatic).</summary>
	internal PlayerColorConfigEditor? ColorConfig;

	/// <summary>Invoked when the user picks a local player colour; null returns the marker to automatic.</summary>
	internal Action<PlayerColorValue?>? ChangePlayerColor;

	/// <summary>Full CUO config template store (saved named BepInEx profiles).</summary>
	internal ConfigurationProfileStore? Profiles;

	/// <summary>Set by the plugin each frame; true while the router is on the IP-direct path.</summary>
	internal bool IpDirectActive;

	/// <summary>Invoked when the user clicks Take on one of a remote player's inventory lines.</summary>
	internal Func<ulong, ulong, bool>? TakeItem;

	/// <summary>Invoked when the user wants to open a remote player's inventory in the game's native radial backpack UI.</summary>
	internal Func<ulong, string, bool>? OpenRemoteBackpack;

	/// <summary>Invoked when the user wants to open the game's native medical UI for a remote player.</summary>
	internal Func<ulong, string, bool>? OpenRemoteMedical;

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

	/// <summary>
	/// Which CUO surface the pointer is over, and which world input that forbids (S4). The two facts
	/// about the migrated controls are the native surface's own polls — the plugin receives them as hover
	/// flips — and the rectangles of the panels CUO still draws itself are this class's; what turns them
	/// into an answer is the Runtime's rule, asked by both world input paths.
	/// </summary>
	private readonly OnlineUiPointerCensus _pointerCensus = new();

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

	/// <summary>The window's shell state and model builder, driven by <see cref="OnlineUiHost"/>.</summary>
	internal OnlineUiWindow Window => _window;

	/// <summary>Records the pointer-over-window fact the native surface reports as a hover flip.</summary>
	internal void SetPointerOverWindow(bool over) => _pointerCensus.OverWindow = over;

	/// <summary>
	/// Records the pointer-over-launcher fact the native surface reports as a hover flip. It is the same
	/// polled fact the idle fade reads, and it is what keeps a middle-click over the launcher from
	/// pinging the world and a right-click there from opening the in-world menu (S2a's recorded limit).
	/// </summary>
	internal void SetPointerOverLauncher(bool over) => _pointerCensus.OverLauncher = over;

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

	/// <summary>Programmatic close (ESC, X button, or a remote-open path); the modal guard sees it on the next frame's adapter call.</summary>
	internal void CloseWindow() => _window.State.Visible = false;

	/// <summary>
	/// Toggles the modal window from the native launcher (the game's own button, ticket
	/// online-ui-art-and-controls-overhaul S2a). Opening it while a session is already running lands on
	/// the Players page: the Home page is the host/join form, and the launcher's click says the player
	/// wants to see who is in the session — the same rule the IMGUI launcher applied.
	/// </summary>
	internal void ToggleWindow(SessionRole role)
	{
		// The command console is a modal surface of its own and owns the input while it is open. Now
		// that CUO's own guard no longer covers CUO's own canvas (S4), nothing else stops a click on the
		// launcher from opening the window behind the console — so the launcher's rule says it here,
		// where the surface's own opening rule already lives. Information, not Debug: a swallowed click
		// is the player's, and the plugin's own default log level is Information, so a line nobody can
		// see would not make the refusal observable.
		if (_commandOverlay.IsOpen)
		{
			Plugin.Logger.LogInfo("Online UI launcher click ignored: the command console owns the input.");
			return;
		}

		_window.State.Visible = !_window.State.Visible;
		if (_window.State.Visible && _window.State.Page == OnlineUiPage.Home && role != SessionRole.None)
		{
			_window.State.Page = OnlineUiPage.Players;
		}
	}

	/// <summary>Closes the standalone player-interaction quick panel.</summary>
	internal void CloseQuickPanel() => _quickPanel.Close();

	/// <summary>Toggles the standalone player-interaction quick panel (configurable session hotkey).</summary>
	internal void ToggleQuickPanel() => _quickPanel.Toggle();

	/// <summary>
	/// True when a world middle-click should not become a location ping: a modal CUO surface is open
	/// (the command console or the window), or the pointer is inside any CUO surface — the launcher and
	/// the window through the surface's own polls, the quick panel and the player context menu through
	/// their rectangles.
	/// </summary>
	internal bool IsPointerOverUi(Vector2 mousePosition)
	{
		RefreshPointerCensus();
		var gui = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
		return _pointerCensus.BlocksWorldPing(gui.x, gui.y);
	}

	/// <summary>
	/// One path's question to the census. The IMGUI panels are read live: their rectangles are this
	/// class's state, and a panel toggled this frame must be recognised on this frame's click.
	/// </summary>
	private void RefreshPointerCensus()
	{
		_pointerCensus.ModalSurfaceOpen = IsCommandConsoleOpen || IsWindowVisible;
		_pointerCensus.OverlayRects = CollectOverlayRects();
	}

	private bool BlocksWorldMenu(Vector2 guiPoint)
	{
		RefreshPointerCensus();
		return _pointerCensus.BlocksWorldMenu(guiPoint.x, guiPoint.y);
	}

	/// <summary>
	/// The IMGUI pass: the surfaces CUO still draws itself — the nameplates and off-screen arrows, the
	/// network HUD, the location pings, the player context menu, the quick panel and the command console.
	/// The modal window is NOT drawn here any more: since S2b it is the game's own controls on the native
	/// surface, built from the model <see cref="OnlineUiHost"/> pushes.
	/// </summary>
	internal void Draw(OnlineUiContext ctx, INativeInputBlocker? inputBlocker)
	{
		// ESC closes the modal Online UI. The native PlayerCamera.HandleInput
		// pause/menu handling is short-circuited by the adapter while the modal
		// is open, and the one-frame `CuoEscCloseSuppression` keeps the modal
		// guard active on this closing frame regardless of whether OnGUI is
		// observed before or after Update, so the same key cannot reach the
		// game's pause path. The standalone command overlay handles its own
		// Escape below.
		var esc = Event.current;
		if (_commandOverlay.IsOpen == false
			&& _window.State.Visible
			&& esc != null
			&& esc.type == EventType.KeyDown
			&& esc.keyCode == KeyCode.Escape)
		{
			Plugin.Logger.LogInfo("Online UI window ESC consumed; closing window.");
			CloseWindow();
			esc.Use();
		}

		if (!_commandOverlay.IsOpen)
		{
			UpdateDelayedStatus(ctx);
			DrawNetworkHud(ctx);
			DrawNameplatesAndArrows(ctx, ctx.Entities);
			LocationPingOverlay.Draw(ctx);
			DrawPlayerContextMenu(ctx);
			_quickPanel.Draw(ctx);
		}

		_commandOverlay.Draw(ctx);

		// Non-modal CUO surfaces (quick panel, right-click context menu) are
		// IMGUI and invisible to UGUI; scoped blockers keep their pixels from
		// leaking to the menu/world without blocking the rest of the screen.
		// The command console is handled by the full modal guard, not scoped
		// blocks.
		inputBlocker?.SetOnlineUiScopedBlocks(_commandOverlay.IsOpen ? [] : CollectOverlayRects());
	}

	/// <summary>
	/// The GUI-space rectangles of the surfaces CUO still draws itself — the quick panel and the player
	/// context menu — in ONE place: the adapter's scoped raycast blockers and the plugin's pointer
	/// census read the same list, so a click that is blocked on one path cannot leak on the other. An
	/// open panel that is not drawn this pass is not included, because its rectangle is only current
	/// while it is drawn.
	/// </summary>
	private IReadOnlyList<OnlineUiBlockRect> CollectOverlayRects()
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
			|| ctx.Session.Role != SessionRole.None;
		if (hadSession == _lastHadSession)
		{
			return;
		}

		_lastHadSession = hadSession;
		if (hadSession)
		{
			var message = ctx.IpDirectActive
				? ctx.T(ctx.Session.Role == SessionRole.Host ? "hud.ip_host_started" : "hud.ip_guest_joined")
				: ctx.T(ctx.Session.Role == SessionRole.Host ? "hud.steam_host_started" : "hud.steam_guest_joined");
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
		if (!ctx.IpDirectActive && ctx.Steam.CurrentLobbyId == 0 && ctx.Session.Role == SessionRole.None)
		{
			return;
		}

		// Minimal top-left readout: no background panel (the game shows the
		// hand-held item there), only the live RTT plus the latest delayed
		// session event. Full details are in the Online UI window.
		//
		// This uses explicit GUI.Label rects, not GUILayout: the status line is
		// time-gated, and a GUILayout control that appears/disappears between
		// IMGUI Layout and Repaint passes throws "Getting control 1's position in
		// a group with only 1 controls". A fixed non-layout overlay cannot drift
		// between passes.
		const float rowHeight = 20f;
		var rect = new Rect(8f, 8f, 220f, 48f);
		var rtt = ctx.Session.LastRttMs >= 0f ? $"{ctx.Session.LastRttMs:F0} ms" : ctx.T("common.pending");
		GUI.Label(new Rect(rect.x, rect.y, rect.width, rowHeight),
			$"{ctx.T("hud.rtt")}: {rtt}", OnlineUiTheme.MutedLabel());

		var elapsed = Time.realtimeSinceStartup - _statusSetTime;
		if (_statusMessage is not null && elapsed >= StatusDelaySeconds && elapsed <= StatusDelaySeconds + StatusHoldSeconds)
		{
			GUI.Label(new Rect(rect.x, rect.y + rowHeight, rect.width, rowHeight),
				_statusMessage, OnlineUiTheme.Status(OnlineUiTheme.Positive));
		}
		else if (_statusMessage is not null && elapsed > StatusDelaySeconds + StatusHoldSeconds)
		{
			_statusMessage = null;
		}
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
			// Right-clicks inside a CUO surface belong to the UI, not the world: never open,
			// re-target or close the in-world menu from one. The launcher's and the window's
			// rectangles are the native surface's own polls (the migrated controls are uGUI, so the
			// plugin keeps no rectangle for them); the two IMGUI panels and the modal flag are the
			// same rule's other facts.
			if (BlocksWorldMenu(mouse))
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
		var remotePlayers = ctx.Entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
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
		var remotePlayers = entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			if (remote.IsLocal || !ctx.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			// Prefer the live render clone's head limb so markers stay on top of
			// the head while standing/crouching/lying; fall back to the body's
			// authoritative position before the clone exists.
			var worldPoint = new Vector3(remote.Position.X, remote.Position.Y, 0f);
			if (ctx.AnchorQuery?.TryGetRemoteHeadPosition(remote.SteamId, out var headX, out var headY) == true)
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
