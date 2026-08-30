using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Home page for the Online UI: connection status, Steam create/join-by-ID
/// entry, and the IP-direct (non-Steam) host/join entry. This is the first page
/// a player sees from the main menu.
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
			GUILayout.Label(ctx.F("home.persona", ColoredName(ctx, steam.LocalSteamId)), OnlineUiTheme.MutedLabel());
			GUILayout.Label(ctx.F("home.steam_id", steam.LocalSteamId), OnlineUiTheme.MutedLabel());
		}

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("home.session"), OnlineUiTheme.Section());
		var sessionText = ctx.IpDirectActive ? ctx.T("home.ip_direct") : ctx.T("home.steam_network");
		GUILayout.Label($"{ctx.F("home.role", ctx.RoleName(session.Role))}  {ctx.T(session.SessionActive ? "home.handshake_active" : "home.handshake_idle")} — {sessionText}", OnlineUiTheme.Label());
		if (ctx.IpDirectActive)
		{
			GUILayout.Label(ctx.F("lobby.role_owner", ctx.RoleName(session.Role), ColoredName(ctx, session.HostSteamId)), OnlineUiTheme.MutedLabel());
		}
		else
		{
			GUILayout.Label(steam.CurrentLobbyId == 0
				? ctx.T("home.lobby_none")
				: ctx.F("home.lobby", steam.CurrentLobbyId), OnlineUiTheme.MutedLabel());
			if (steam.CurrentLobbyId != 0)
			{
				GUILayout.Label(ctx.F("lobby.role_owner", ctx.RoleName(session.Role), ColoredName(ctx, steam.GetLobbyOwner())), OnlineUiTheme.MutedLabel());
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
			}
		}

		if (session.LastRttMs >= 0f)
		{
			GUILayout.Label(ctx.F("home.last_rtt", $"{session.LastRttMs:F1}"), OnlineUiTheme.MutedLabel());
		}

		if (ctx.IpDirectActive || steam.CurrentLobbyId != 0)
		{
			var leaveLabel = session.Role == Runtime.Session.SessionRole.Host ? ctx.T("lobby.close_room") : ctx.T("lobby.leave_lobby");
			if (GUILayout.Button(ctx.IpDirectActive ? ctx.T("ip.leave") : leaveLabel, OnlineUiTheme.Button(), GUILayout.Width(150f)))
			{
				if (ctx.IpDirectActive)
				{
					ctx.LeaveIp?.Invoke();
				}
				else
				{
					ctx.LeaveLobby?.Invoke();
				}
			}

			GUILayout.Label(ctx.T("home.already_in_session"), OnlineUiTheme.Status(OnlineUiTheme.Positive));
			if (GUILayout.Button(ctx.T("home.open_players_page"), OnlineUiTheme.Button(), GUILayout.Height(30f)))
			{
				ctx.State.Page = OnlineUiPage.Players;
			}

			return;
		}

		DrawTransportSelector(ctx);

		if (ctx.State.TransportMode == OnlineUiTransportMode.Steam)
		{
			DrawSteamHostJoin(ctx);
		}
		else
		{
			DrawIpDirect(ctx);
		}

		GUILayout.Space(10f);
		GUILayout.Label(ctx.T("home.hotkeys"), OnlineUiTheme.MutedLabel());
	}

	private static void DrawTransportSelector(OnlineUiContext ctx)
	{
		GUILayout.Space(6f);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button(ctx.T("home.steam_network"),
			OnlineUiTheme.Tab(ctx.State.TransportMode == OnlineUiTransportMode.Steam),
			GUILayout.Height(28f)))
		{
			ctx.State.TransportMode = OnlineUiTransportMode.Steam;
		}

		if (GUILayout.Button(ctx.T("home.ip_direct"),
			OnlineUiTheme.Tab(ctx.State.TransportMode == OnlineUiTransportMode.IpDirect),
			GUILayout.Height(28f)))
		{
			ctx.State.TransportMode = OnlineUiTransportMode.IpDirect;
		}

		GUILayout.EndHorizontal();
		GUILayout.Space(8f);
	}

	private static void DrawSteamHostJoin(OnlineUiContext ctx)
	{
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
	}

	private static void DrawIpDirect(OnlineUiContext ctx)
	{
		if (ctx.IpConfig is not { } config)
		{
			return;
		}

		GUILayout.Label(ctx.T("ip.section"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("ip.hint"), OnlineUiTheme.MutedLabel());

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("ip.listen_port"), OnlineUiTheme.MutedLabel());
		var listenText = GUILayout.TextField(config.ListenPort.ToString(), 6, GUILayout.Width(80f));
		if (int.TryParse(listenText, out var listenPort) && listenPort is >= 1 and <= 65535 && listenPort != config.ListenPort)
		{
			config.SetListenPort(listenPort);
		}

		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("ip.display_name"), OnlineUiTheme.MutedLabel());
		var name = GUILayout.TextField(config.DisplayName, 24, GUILayout.Width(200f));
		if (name != config.DisplayName)
		{
			config.SetDisplayName(name);
		}

		GUILayout.EndHorizontal();

		if (GUILayout.Button(ctx.T("ip.create_host"), OnlineUiTheme.Button(), GUILayout.Height(34f)))
		{
			ctx.State.Error = null;
			ctx.CreateIpHost?.Invoke();
		}

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("ip.join_section"), OnlineUiTheme.Section());
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T("ip.address"), OnlineUiTheme.MutedLabel());
		var address = GUILayout.TextField(config.JoinAddress, 64, GUILayout.Width(180f));
		if (address != config.JoinAddress)
		{
			config.SetJoinAddress(address);
		}

		GUILayout.Label(ctx.T("ip.port"), OnlineUiTheme.MutedLabel());
		var portText = GUILayout.TextField(config.JoinPort.ToString(), 6, GUILayout.Width(60f));
		if (int.TryParse(portText, out var joinPort) && joinPort is >= 1 and <= 65535 && joinPort != config.JoinPort)
		{
			config.SetJoinPort(joinPort);
		}

		GUILayout.EndHorizontal();

		if (GUILayout.Button(ctx.T("ip.join"), OnlineUiTheme.Button(), GUILayout.Height(30f)))
		{
			ctx.State.Error = null;
			ctx.JoinIp?.Invoke(config.JoinAddress.Trim(), config.JoinPort);
		}

		DrawError(ctx);
	}

	private static void DrawError(OnlineUiContext ctx)
	{
		var error = ctx.State.Error ?? ctx.LastJoinError;
		if (!string.IsNullOrEmpty(error))
		{
			GUILayout.Label(error!, OnlineUiTheme.Status(OnlineUiTheme.Error));
		}
	}

	private static string ColoredName(OnlineUiContext ctx, ulong steamId)
	{
		var color = ctx.PlayerColor(steamId);
		var hex = ColorUtility.ToHtmlStringRGB(new Color(color.R, color.G, color.B, color.A));
		return $"<color=#{hex}>{ctx.DisplayName(steamId)}</color>";
	}
}
