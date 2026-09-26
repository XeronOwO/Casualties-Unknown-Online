using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The CUO Online UI window: its shell state and the page it shows, built into the display list the
/// game's own controls render (ticket online-ui-art-and-controls-overhaul, S2b).
///
/// <para>
/// This class no longer draws anything. It knows which page is open, builds the tab row and the page's
/// rows through <see cref="OnlineUiPageBuilder"/>, and dispatches the intents that come back from the
/// surface to the action each control id was registered under — the same pairing the IMGUI controls
/// used to make inline. The shell's own chrome (its frame, its close control, its scroll) belongs to
/// the surface, so nothing here knows about pixels.
/// </para>
///
/// <para>
/// The tab row and the page dispatch stay in this file on purpose: the console page's removal is pinned
/// as exactly one tab per surviving page, in page order, and exactly one case per page here, and that
/// contract has to hold in the uGUI world exactly as it did in the IMGUI one.
/// </para>
/// </summary>
internal sealed class OnlineUiWindow
{
	private readonly OnlineUiWindowState _state = new();
	private readonly Dictionary<string, Action<OnlineUiIntent>> _actions = [];

	internal OnlineUiWindowState State => _state;

	/// <summary>
	/// The window's model for this frame, or null when the window is not shown. The rebuild happens every
	/// frame the window is open (the pages show live session facts, as their IMGUI rows did) and the
	/// action table is rebuilt with it, so an intent always meets the registration that produced its
	/// control.
	/// </summary>
	internal OnlineUiWindowModel? Build(OnlineUiContext ctx)
	{
		_actions.Clear();
		if (!_state.Visible)
		{
			return null;
		}

		// The shell's own close control: the surface owns the button (it is chrome), this is what its
		// click means.
		_actions[OnlineUiControlIds.WindowClose] = _ => _state.Visible = false;

		var page = new OnlineUiPageBuilder(ctx, _actions);
		BuildTabs(page);
		switch (_state.Page)
		{
			case OnlineUiPage.Home:
				OnlineUiHomeDrawer.Build(ctx, page);
				break;
			case OnlineUiPage.Players:
				OnlineUiPlayersDrawer.Build(ctx, page);
				break;
			case OnlineUiPage.Network:
				OnlineUiNetworkDrawer.Build(ctx, page);
				break;
			case OnlineUiPage.Admin:
				OnlineUiAdminDrawer.Build(ctx, page);
				break;
			case OnlineUiPage.Worlds:
				OnlineUiWorldsDrawer.Build(ctx, page);
				break;
			case OnlineUiPage.Preferences:
				OnlineUiPreferencesDrawer.Build(ctx, page);
				break;
		}

		return new OnlineUiWindowModel(ctx.T("window.title"), page.Tabs, page.Rows);
	}

	/// <summary>
	/// Runs the action registered under the intent's control id. False means the id is not in this
	/// frame's model — the control the player acted on is gone (the page changed, the member left), and
	/// a click on a control that no longer exists is dropped rather than guessed at.
	/// </summary>
	internal bool Apply(OnlineUiIntent intent)
	{
		if (!_actions.TryGetValue(intent.ControlId, out var action))
		{
			return false;
		}

		action(intent);
		return true;
	}

	private void BuildTabs(OnlineUiPageBuilder page)
	{
		BuildTab(page, OnlineUiPage.Home, "tab.home", page.T("tab.home"));
		BuildTab(page, OnlineUiPage.Players, "tab.players", page.T("tab.players"));
		BuildTab(page, OnlineUiPage.Network, "tab.network", page.T("tab.network"));
		BuildTab(page, OnlineUiPage.Admin, "tab.admin", page.T("tab.admin"));
		BuildTab(page, OnlineUiPage.Worlds, "tab.worlds", page.T("tab.worlds"));
		BuildTab(page, OnlineUiPage.Preferences, "tab.preferences", page.T("tab.preferences"));
	}

	private void BuildTab(OnlineUiPageBuilder page, OnlineUiPage target, string id, string label) =>
		page.Tab(id, label, _state.Page == target, () => _state.Page = target);
}
