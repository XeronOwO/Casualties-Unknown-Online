using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The CUO Online UI window shell: the launcher button, the draggable IMGUI
/// window, the page tabs and the scrollable page host. Page content lives in
/// the <c>OnlineUi*Drawer</c> classes; this class owns only the shell and the
/// local presentation state.
/// </summary>
internal sealed class OnlineUiWindow
{
	private const int WindowId = 9001;
	private const float Width = 780f;
	private const float Height = 540f;

	private readonly OnlineUiWindowState _state = new();

	private Rect _windowRect;

	internal OnlineUiWindowState State => _state;

	internal void Draw(OnlineUiContext ctx)
	{
		ctx.State = _state;
		DrawLauncherButton(ctx);

		if (!_state.Visible)
		{
			return;
		}

		if (_windowRect.width < 1f)
		{
			_windowRect = new Rect((Screen.width - Width) * 0.5f, (Screen.height - Height) * 0.5f, Width, Height);
		}

		_windowRect = GUI.Window(WindowId, _windowRect, id => DrawWindowContents(ctx), "", OnlineUiTheme.Window());
	}

	private void DrawLauncherButton(OnlineUiContext ctx)
	{
		var rect = new Rect(Screen.width - 170f, 12f, 158f, 34f);
		OnlineUiTheme.DrawBackground(rect);
		if (GUI.Button(rect, _state.Visible ? "CUO ONLINE ▲" : "CUO ONLINE ▼", OnlineUiTheme.Launcher()))
		{
			_state.Visible = !_state.Visible;
			if (_state.Visible && _state.Page == OnlineUiPage.Home && ctx.Session.Role != Runtime.Session.SessionRole.None)
			{
				_state.Page = OnlineUiPage.Lobby;
			}
		}
	}

	private void DrawWindowContents(OnlineUiContext ctx)
	{
		OnlineUiTheme.DrawBackground(new Rect(0f, 0f, _windowRect.width, _windowRect.height));

		var area = new Rect(8f, 8f, _windowRect.width - 16f, _windowRect.height - 16f);
		GUILayout.BeginArea(area);

		GUILayout.BeginHorizontal();
		GUILayout.Label("CASUALTIES UNKNOWN: ONLINE", OnlineUiTheme.Title());
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("✕", OnlineUiTheme.Button(), GUILayout.Width(26f), GUILayout.Height(22f)))
		{
			_state.Visible = false;
		}

		GUILayout.EndHorizontal();

		GUILayout.Space(4f);
		DrawTabs();
		GUILayout.Space(6f);

		_state.Scroll = GUILayout.BeginScrollView(_state.Scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
		switch (_state.Page)
		{
			case OnlineUiPage.Home:
				OnlineUiHomeDrawer.Draw(ctx);
				break;
			case OnlineUiPage.Lobby:
				OnlineUiLobbyDrawer.Draw(ctx);
				break;
			case OnlineUiPage.Players:
				OnlineUiPlayersDrawer.Draw(ctx);
				break;
			case OnlineUiPage.Network:
				OnlineUiNetworkDrawer.Draw(ctx);
				break;
			case OnlineUiPage.Admin:
				OnlineUiAdminDrawer.Draw(ctx);
				break;
		}

		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private void DrawTabs()
	{
		GUILayout.BeginHorizontal();
		DrawTab("Home", OnlineUiPage.Home);
		DrawTab("Lobby", OnlineUiPage.Lobby);
		DrawTab("Players", OnlineUiPage.Players);
		DrawTab("Network", OnlineUiPage.Network);
		DrawTab("Admin", OnlineUiPage.Admin);
		GUILayout.EndHorizontal();
	}

	private void DrawTab(string label, OnlineUiPage page)
	{
		if (GUILayout.Button(label, OnlineUiTheme.Tab(_state.Page == page), GUILayout.Height(28f)))
		{
			_state.Page = page;
		}
	}
}
