using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Players page: the in-world member roster with vitals, inventory expansion
/// and the direct player-interaction actions (carry/drop/heal/take/recruit).
/// </summary>
internal static class OnlineUiPlayersDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;
		if (steam.CurrentLobbyId == 0)
		{
			GUILayout.Label(ctx.T("players.not_in_lobby"), OnlineUiTheme.MutedLabel());
			return;
		}

		GUILayout.Label(ctx.T("players.section"), OnlineUiTheme.Section());
		GUILayout.Label(session.LocalInWorld ? ctx.T("players.local_in_world") : ctx.T("players.local_menu"), OnlineUiTheme.MutedLabel());

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Draw(ctx, rows);
	}
}
