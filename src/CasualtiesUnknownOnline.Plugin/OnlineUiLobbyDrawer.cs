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
			GUILayout.Label("You are not in a lobby. Create or join one from Home.", OnlineUiTheme.MutedLabel());
			return;
		}

		GUILayout.Label($"LOBBY {steam.CurrentLobbyId}", OnlineUiTheme.Section());
		GUILayout.Label($"Role: {session.Role}  Owner: {DisplayName(steam, steam.GetLobbyOwner())}", OnlineUiTheme.Label());
		GUILayout.Label($"Members: {steam.GetLobbyMembers().Length}", OnlineUiTheme.MutedLabel());

		if (GUILayout.Button("Copy Lobby ID", OnlineUiTheme.Button(), GUILayout.Width(130f)))
		{
			GUIUtility.systemCopyBuffer = steam.CurrentLobbyId.ToString();
			ctx.State.Error = "Lobby ID copied to clipboard.";
		}

		if (!string.IsNullOrEmpty(ctx.State.Error))
		{
			GUILayout.Label(ctx.State.Error!, OnlineUiTheme.Status(OnlineUiTheme.Positive));
		}

		GUILayout.Space(8f);
		GUILayout.Label("MEMBERS", OnlineUiTheme.Section());

		var rows = OnlineUiMemberListDrawer.BuildRows(ctx);
		OnlineUiMemberListDrawer.Draw(ctx, rows);
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
