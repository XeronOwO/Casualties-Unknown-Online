using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Home page for the Online UI: Steam status, create-lobby entry and join-by-ID
/// entry. This is the first page a player sees from the main menu.
/// </summary>
internal static class OnlineUiHomeDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;

		GUILayout.Label("STEAM STATUS", OnlineUiTheme.Section());
		GUILayout.Label(steam.IsInitialized ? "Steam: <color=#70D28F>initialized</color>" : "Steam: <color=#E6615A>not initialized</color>", OnlineUiTheme.Label());
		if (steam.IsInitialized)
		{
			GUILayout.Label($"Persona: {DisplayName(steam, steam.LocalSteamId)}", OnlineUiTheme.MutedLabel());
			GUILayout.Label($"SteamID: {steam.LocalSteamId}", OnlineUiTheme.MutedLabel());
		}

		GUILayout.Space(8f);
		GUILayout.Label("SESSION", OnlineUiTheme.Section());
		GUILayout.Label($"Role: {session.Role}  Handshake: {(session.SessionActive ? "active" : "idle")}", OnlineUiTheme.Label());
		GUILayout.Label($"Lobby: {(steam.CurrentLobbyId == 0 ? "none" : steam.CurrentLobbyId.ToString())}", OnlineUiTheme.MutedLabel());
		if (session.LastRttMs >= 0f)
		{
			GUILayout.Label($"Last RTT: {session.LastRttMs:F1} ms", OnlineUiTheme.MutedLabel());
		}

		GUILayout.Space(10f);
		if (steam.CurrentLobbyId != 0)
		{
			GUILayout.Label("You are already in a lobby.", OnlineUiTheme.Status(OnlineUiTheme.Positive));
			if (GUILayout.Button("Open Lobby Page", OnlineUiTheme.Button(), GUILayout.Height(30f)))
			{
				ctx.State.Page = OnlineUiPage.Lobby;
			}

			return;
		}

		GUILayout.Label("HOST A GAME", OnlineUiTheme.Section());
		GUILayout.Label("Create a public Steam lobby and wait for friends.", OnlineUiTheme.MutedLabel());
		if (GUILayout.Button("Create Lobby", OnlineUiTheme.Button(), GUILayout.Height(34f)))
		{
			ctx.State.Error = null;
			ctx.CreateLobby?.Invoke();
		}

		GUILayout.Space(10f);
		GUILayout.Label("JOIN A GAME", OnlineUiTheme.Section());
		GUILayout.Label("Enter the lobby ID shown by the host.", OnlineUiTheme.MutedLabel());
		GUILayout.BeginHorizontal();
		ctx.State.LobbyIdInput = GUILayout.TextField(ctx.State.LobbyIdInput, 20, GUILayout.Width(240f));
		if (GUILayout.Button("Join", OnlineUiTheme.Button(), GUILayout.Width(90f)))
		{
			var trimmed = ctx.State.LobbyIdInput.Trim();
			if (ulong.TryParse(trimmed, out _))
			{
				ctx.State.Error = null;
				ctx.JoinLobby?.Invoke(trimmed);
			}
			else
			{
				ctx.State.Error = "Lobby ID must be a number.";
			}
		}

		GUILayout.EndHorizontal();

		DrawError(ctx);

		GUILayout.Space(10f);
		GUILayout.Label("Hotkeys: F8 create / F9 join from config / F7 ping peer", OnlineUiTheme.MutedLabel());
	}

	private static void DrawError(OnlineUiContext ctx)
	{
		var error = ctx.State.Error ?? ctx.LastJoinError;
		if (!string.IsNullOrEmpty(error))
		{
			GUILayout.Label(error!, OnlineUiTheme.Status(OnlineUiTheme.Error));
		}
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
