using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Home page for the Online UI: connection status, Steam create/join-by-ID
/// entry, and the IP-direct (non-Steam) host/join entry. This is the first page
/// a player sees from the main menu.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) the page builds a display list instead of
/// drawing itself: the transport switch, both host/join forms and every field are rows on the game's own
/// controls, and each control's click or edit comes back as an intent carrying the id registered here.
/// </para>
/// </summary>
internal static class OnlineUiHomeDrawer
{
	private const float CopyButtonWidth = 130f;
	private const float LeaveButtonWidth = 150f;
	private const float LobbyIdWidth = 240f;
	private const float JoinButtonWidth = 90f;
	private const float PortFieldWidth = 80f;
	private const float NameFieldWidth = 200f;
	private const float AddressFieldWidth = 180f;
	private const float JoinPortFieldWidth = 60f;

	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;

		page.Section(ctx.T("home.steam_status"));
		var steamStatus = steam.IsInitialized
			? $"<color=#70D28F>{ctx.T("home.steam_initialized")}</color>"
			: $"<color=#E6615A>{ctx.T("home.steam_not_initialized")}</color>";
		page.Label(steamStatus);
		if (steam.IsInitialized)
		{
			page.Muted(ctx.F("home.persona", ColoredName(ctx, steam.LocalSteamId)));
			page.Muted(ctx.F("home.steam_id", steam.LocalSteamId));
		}

		page.Space();
		page.Section(ctx.T("home.session"));
		var sessionText = ctx.IpDirectActive ? ctx.T("home.ip_direct") : ctx.T("home.steam_network");
		page.Label($"{ctx.F("home.role", ctx.RoleName(session.Role))}  {ctx.T(session.SessionActive ? "home.handshake_active" : "home.handshake_idle")} — {sessionText}");
		if (ctx.IpDirectActive)
		{
			page.Muted(ctx.F("lobby.role_owner", ctx.RoleName(session.Role), ColoredName(ctx, session.HostSteamId)));
		}
		else
		{
			page.Muted(steam.CurrentLobbyId == 0
				? ctx.T("home.lobby_none")
				: ctx.F("home.lobby", steam.CurrentLobbyId));
			if (steam.CurrentLobbyId != 0)
			{
				page.Muted(ctx.F("lobby.role_owner", ctx.RoleName(session.Role), ColoredName(ctx, steam.GetLobbyOwner())));
				page.Muted(ctx.F("lobby.members", steam.GetLobbyMembers().Length));

				page.Button(
					"home.copy_lobby_id",
					ctx.T("lobby.copy_id"),
					() =>
					{
						GUIUtility.systemCopyBuffer = steam.CurrentLobbyId.ToString();
						ctx.State.Error = ctx.T("lobby.id_copied");
					},
					width: CopyButtonWidth);

				if (!string.IsNullOrEmpty(ctx.State.Error))
				{
					page.Status(ctx.State.Error!, OnlineUiTheme.Positive);
				}
			}
		}

		if (session.LastRttMs >= 0f)
		{
			page.Muted(ctx.F("home.last_rtt", $"{session.LastRttMs:F1}"));
		}

		if (ctx.IpDirectActive || steam.CurrentLobbyId != 0)
		{
			var leaveLabel = session.Role == SessionRole.Host ? ctx.T("lobby.close_room") : ctx.T("lobby.leave_lobby");
			page.Button(
				"home.leave",
				ctx.IpDirectActive ? ctx.T("ip.leave") : leaveLabel,
				() =>
				{
					if (ctx.IpDirectActive)
					{
						ctx.LeaveIp?.Invoke();
					}
					else
					{
						ctx.LeaveLobby?.Invoke();
					}
				},
				width: LeaveButtonWidth);

			page.Status(ctx.T("home.already_in_session"), OnlineUiTheme.Positive);
			page.Button("home.open_players", ctx.T("home.open_players_page"), () => ctx.State.Page = OnlineUiPage.Players);
			return;
		}

		BuildTransportSelector(ctx, page);

		if (ctx.State.TransportMode == OnlineUiTransportMode.Steam)
		{
			BuildSteamHostJoin(ctx, page);
		}
		else
		{
			BuildIpDirect(ctx, page);
		}

		page.Space();
		page.Muted(ctx.T("home.hotkeys"));
	}

	/// <summary>The Steam / IP-direct switch: a two-way choice, marked the way the tab row marks the
	/// current page.</summary>
	private static void BuildTransportSelector(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var steam = ctx.State.TransportMode == OnlineUiTransportMode.Steam;
		page.Space();
		page.Row(
			page.ButtonElement(
				"home.transport.steam",
				ctx.T("home.steam_network"),
				() => ctx.State.TransportMode = OnlineUiTransportMode.Steam,
				selected: steam),
			page.ButtonElement(
				"home.transport.ip",
				ctx.T("home.ip_direct"),
				() => ctx.State.TransportMode = OnlineUiTransportMode.IpDirect,
				selected: !steam));
		page.Space();
	}

	private static void BuildSteamHostJoin(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		page.Section(ctx.T("home.host_a_game"));
		page.Muted(ctx.T("home.host_hint"));
		page.Button(
			"home.create_lobby",
			ctx.T("home.create_lobby"),
			() =>
			{
				ctx.State.Error = null;
				ctx.CreateLobby?.Invoke();
			});

		page.Space();
		page.Section(ctx.T("home.join_a_game"));
		page.Muted(ctx.T("home.join_hint"));
		page.Row(
			page.TextFieldElement(
				"home.lobby_id",
				"",
				ctx.State.LobbyIdInput,
				maxLength: 20,
				edited: text => ctx.State.LobbyIdInput = text,
				width: LobbyIdWidth),
			page.ButtonElement("home.join", ctx.T("home.join"), () => JoinLobby(ctx), width: JoinButtonWidth));

		BuildError(ctx, page);
	}

	private static void JoinLobby(OnlineUiContext ctx)
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

	private static void BuildIpDirect(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		if (ctx.IpConfig is not { } config)
		{
			return;
		}

		page.Section(ctx.T("ip.section"));
		page.Muted(ctx.T("ip.hint"));

		page.Row(
			page.LabelElement(ctx.T("ip.listen_port"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				"ip.listen_port",
				"",
				config.ListenPort.ToString(),
				maxLength: 6,
				edited: text =>
				{
					if (int.TryParse(text, out var listenPort) && listenPort is >= 1 and <= 65535 && listenPort != config.ListenPort)
					{
						config.SetListenPort(listenPort);
					}
				},
				width: PortFieldWidth));

		page.Row(
			page.LabelElement(ctx.T("ip.display_name"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				"ip.display_name",
				"",
				config.DisplayName,
				maxLength: 24,
				edited: text =>
				{
					if (text != config.DisplayName)
					{
						config.SetDisplayName(text);
					}
				},
				width: NameFieldWidth));

		page.Button(
			"ip.create_host",
			ctx.T("ip.create_host"),
			() =>
			{
				ctx.State.Error = null;
				ctx.CreateIpHost?.Invoke();
			});

		page.Space();
		page.Section(ctx.T("ip.join_section"));
		page.Row(
			page.LabelElement(ctx.T("ip.address"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				"ip.address",
				"",
				config.JoinAddress,
				maxLength: 64,
				edited: text =>
				{
					if (text != config.JoinAddress)
					{
						config.SetJoinAddress(text);
					}
				},
				width: AddressFieldWidth),
			page.LabelElement(ctx.T("ip.port"), color: OnlineUiTheme.Muted),
			page.TextFieldElement(
				"ip.port",
				"",
				config.JoinPort.ToString(),
				maxLength: 6,
				edited: text =>
				{
					if (int.TryParse(text, out var joinPort) && joinPort is >= 1 and <= 65535 && joinPort != config.JoinPort)
					{
						config.SetJoinPort(joinPort);
					}
				},
				width: JoinPortFieldWidth));

		page.Button(
			"ip.join",
			ctx.T("ip.join"),
			() =>
			{
				ctx.State.Error = null;
				ctx.JoinIp?.Invoke(config.JoinAddress.Trim(), config.JoinPort);
			});

		BuildError(ctx, page);
	}

	private static void BuildError(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var error = ctx.State.Error ?? ctx.LastJoinError;
		if (!string.IsNullOrEmpty(error))
		{
			page.Status(error!, OnlineUiTheme.Error);
		}
	}

	private static string ColoredName(OnlineUiContext ctx, ulong steamId)
	{
		var color = ctx.PlayerColor(steamId);
		var hex = ColorUtility.ToHtmlStringRGB(new Color(color.R, color.G, color.B, color.A));
		return $"<color=#{hex}>{ctx.DisplayName(steamId)}</color>";
	}
}
