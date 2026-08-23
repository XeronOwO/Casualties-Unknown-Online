using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Players page: the in-world member roster. Session/lobby identity and
/// session controls live on the Home page; this page shows the local player
/// state and every member card with vitals, inventory expansion and direct
/// player-interaction actions.
/// </summary>
internal static class OnlineUiPlayersDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;
		if (!ctx.IpDirectActive && steam.CurrentLobbyId == 0)
		{
			GUILayout.Label(ctx.T("players.not_in_session"), OnlineUiTheme.MutedLabel());
			return;
		}

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("players.section"), OnlineUiTheme.Section());
		GUILayout.Label(session.LocalInWorld ? ctx.T("players.local_in_world") : ctx.T("players.local_menu"), OnlineUiTheme.MutedLabel());

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Draw(ctx, rows);
	}
}
