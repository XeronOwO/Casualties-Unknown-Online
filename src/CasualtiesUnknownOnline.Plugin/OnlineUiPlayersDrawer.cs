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
			GUILayout.Label("You are not in a lobby.", OnlineUiTheme.MutedLabel());
			return;
		}

		GUILayout.Label("PLAYERS", OnlineUiTheme.Section());
		GUILayout.Label($"Local player: {(session.LocalInWorld ? "in world" : "menu")}", OnlineUiTheme.MutedLabel());

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Draw(ctx, rows);
	}
}
