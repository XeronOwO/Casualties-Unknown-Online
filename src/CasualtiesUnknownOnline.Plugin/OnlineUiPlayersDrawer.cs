namespace CasualtiesUnknownOnline;

/// <summary>
/// Players page: the in-world member roster. Session/lobby identity and
/// session controls live on the Home page; this page shows the local player
/// state and every member card with vitals, native remote-backpack access and
/// direct player-interaction actions.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) the page builds a display list instead of
/// drawing itself; the member cards themselves are built by <see cref="OnlineUiMemberListDrawer"/>.
/// </para>
/// </summary>
internal static class OnlineUiPlayersDrawer
{
	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;
		if (!ctx.IpDirectActive && steam.CurrentLobbyId == 0)
		{
			page.Muted(ctx.T("players.not_in_session"));
			return;
		}

		page.Section(ctx.T("players.section"));
		page.Muted(session.LocalInWorld ? ctx.T("players.local_in_world") : ctx.T("players.local_menu"));

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Build(ctx, page, rows);
	}
}
