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

		GUILayout.Label(ctx.T("home.steam_status"), OnlineUiTheme.Section());
		var steamStatus = steam.IsInitialized
			? $"<color=#70D28F>{ctx.T("home.steam_initialized")}</color>"
			: $"<color=#E6615A>{ctx.T("home.steam_not_initialized")}</color>";
		GUILayout.Label(steamStatus, OnlineUiTheme.Label());
		if (steam.IsInitialized)
		{
			GUILayout.Label(ctx.F("home.persona", DisplayName(steam, steam.LocalSteamId)), OnlineUiTheme.MutedLabel());
			GUILayout.Label(ctx.F("home.steam_id", steam.LocalSteamId), OnlineUiTheme.MutedLabel());
		}

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("home.session"), OnlineUiTheme.Section());
		GUILayout.Label($"{ctx.F("home.role", ctx.RoleName(session.Role))}  {ctx.T(session.SessionActive ? "home.handshake_active" : "home.handshake_idle")}", OnlineUiTheme.Label());
		GUILayout.Label(steam.CurrentLobbyId == 0
			? ctx.T("home.lobby_none")
			: ctx.F("home.lobby", steam.CurrentLobbyId), OnlineUiTheme.MutedLabel());
		if (session.LastRttMs >= 0f)
		{
			GUILayout.Label(ctx.F("home.last_rtt", $"{session.LastRttMs:F1}"), OnlineUiTheme.MutedLabel());
		}

		GUILayout.Space(10f);
		if (steam.CurrentLobbyId != 0)
		{
			GUILayout.Label(ctx.T("home.already_in_lobby"), OnlineUiTheme.Status(OnlineUiTheme.Positive));
			if (GUILayout.Button(ctx.T("home.open_lobby_page"), OnlineUiTheme.Button(), GUILayout.Height(30f)))
			{
				ctx.State.Page = OnlineUiPage.Lobby;
			}

			return;
		}

		GUILayout.Label(ctx.T("home.host_a_game"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("home.host_hint"), OnlineUiTheme.MutedLabel());
		if (GUILayout.Button(ctx.T("home.create_lobby"), OnlineUiTheme.Button(), GUILayout.Height(34f)))
		{
			ctx.State.Error = null;
			ctx.CreateLobby?.Invoke();
		}

		GUILayout.Space(10f);
		GUILayout.Label(ctx.T("home.join_a_game"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("home.join_hint"), OnlineUiTheme.MutedLabel());
		GUILayout.BeginHorizontal();
		ctx.State.LobbyIdInput = GUILayout.TextField(ctx.State.LobbyIdInput, 20, GUILayout.Width(240f));
		if (GUILayout.Button(ctx.T("home.join"), OnlineUiTheme.Button(), GUILayout.Width(90f)))
		{
			var trimmed = ctx.State.LobbyIdInput.Trim();
			if (ulong.TryParse(trimmed, out _))
			{
				ctx.State.Error = null;
				ctx.JoinLobby?.Invoke(trimmed);
			}
			else
			{
				ctx.State.Error = ctx.T("home.lobby_id_must_be_number");
			}
		}

		GUILayout.EndHorizontal();

		DrawError(ctx);

		GUILayout.Space(10f);
		GUILayout.Label(ctx.T("home.hotkeys"), OnlineUiTheme.MutedLabel());
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
