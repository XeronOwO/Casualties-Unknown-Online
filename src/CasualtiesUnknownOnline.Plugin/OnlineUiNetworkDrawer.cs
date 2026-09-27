namespace CasualtiesUnknownOnline;

/// <summary>
/// Network page: the connection diagnostics that are already available from the
/// runtime — transport/lobby state, role, handshake, per-member RTT and entity
/// sync state. Real traffic/health metrics are recorded in logs; this page is
/// the readable live snapshot.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) the page builds a display list instead of
/// drawing itself: the rows go to the game's own controls on the native surface, and the ping button's
/// click comes back as an intent.
/// </para>
/// </summary>
internal static class OnlineUiNetworkDrawer
{
	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;

		page.Section(ctx.T("network.connection"));
		if (ctx.IpDirectActive)
		{
			page.Label(ctx.F("network.mode", ctx.T("ip.mode_label")));
			page.Muted(ctx.F("network.address", ctx.IpConfig?.ListenPort.ToString() ?? ""));
		}
		else
		{
			page.Label(ctx.F("network.steam", ctx.T(steam.IsInitialized ? "common.initialized" : "common.not_initialized")));
			page.Muted(ctx.F("network.lobby", steam.CurrentLobbyId == 0 ? ctx.T("common.none") : steam.CurrentLobbyId.ToString()));
		}

		page.Muted(ctx.F("network.role", ctx.RoleName(session.Role)));
		page.Muted(ctx.F("network.handshake", ctx.T(session.SessionActive ? "common.active" : "common.idle")));
		page.Muted(ctx.F("network.entity_sync", ctx.T(ctx.Entities.EntitySyncActive ? "common.active" : "common.off")));
		page.Muted(ctx.F("network.local_player", ctx.T(session.LocalInWorld ? "common.in_world" : "common.menu")));
		page.Muted(session.LastRttMs >= 0f ? ctx.F("network.last_rtt", $"{session.LastRttMs:F1} ms") : ctx.T("common.no_ping"));
		page.Button("network.ping", ctx.T("network.ping"), session.RequestPing, width: 90f);

		page.Section(ctx.T("network.peer_rtt"));
		foreach (var member in session.Members)
		{
			var name = ctx.DisplayName(member.SteamId);
			var rtt = member.RttMs >= 0f ? $"{member.RttMs:F0} ms" : ctx.T("common.pending");
			page.Muted($"{name}: {rtt}");
		}
	}
}
