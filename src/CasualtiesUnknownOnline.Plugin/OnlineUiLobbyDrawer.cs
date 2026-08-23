using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Lobby page: the current Steam lobby identity, member roster and host admin
/// actions. World interaction actions are rendered here too when they apply,
/// but the full world/inventory page is Players.
/// </summary>
internal static class OnlineUiLobbyDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;
		if (steam.CurrentLobbyId == 0)
		{
			GUILayout.Label(ctx.T("lobby.not_in_lobby"), OnlineUiTheme.MutedLabel());
			return;
		}

		GUILayout.Label(ctx.F("lobby.title", steam.CurrentLobbyId), OnlineUiTheme.Section());
		GUILayout.Label(ctx.F("lobby.role_owner", ctx.RoleName(session.Role), DisplayName(steam, steam.GetLobbyOwner())), OnlineUiTheme.Label());
		GUILayout.Label(ctx.F("lobby.members", steam.GetLobbyMembers().Length), OnlineUiTheme.MutedLabel());

		if (GUILayout.Button(ctx.T("lobby.copy_id"), OnlineUiTheme.Button(), GUILayout.Width(130f)))
		{
			GUIUtility.systemCopyBuffer = steam.CurrentLobbyId.ToString();
			ctx.State.Error = ctx.T("lobby.id_copied");
		}

		if (!string.IsNullOrEmpty(ctx.State.Error))
		{
			GUILayout.Label(ctx.State.Error!, OnlineUiTheme.Status(OnlineUiTheme.Positive));
		}

		GUILayout.Space(6f);
		var leaveLabel = session.Role == SessionRole.Host ? ctx.T("lobby.close_room") : ctx.T("lobby.leave_lobby");
		if (GUILayout.Button(leaveLabel, OnlineUiTheme.Button(), GUILayout.Width(150f)))
		{
			ctx.LeaveLobby?.Invoke();
		}

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("lobby.members_section"), OnlineUiTheme.Section());

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Draw(ctx, rows);
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
