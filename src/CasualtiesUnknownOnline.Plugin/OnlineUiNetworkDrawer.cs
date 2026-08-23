using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Network page: the connection diagnostics that are already available from the
/// runtime — Steam/lobby state, role, handshake, per-member RTT and entity sync
/// state. Real traffic/health metrics are recorded in logs; this page is the
/// readable live snapshot.
/// </summary>
internal static class OnlineUiNetworkDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var steam = ctx.Steam;
		var session = ctx.Session;

		GUILayout.Label(ctx.T("network.connection"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.F("network.steam", ctx.T(steam.IsInitialized ? "common.initialized" : "common.not_initialized")), OnlineUiTheme.Label());
		GUILayout.Label(ctx.F("network.lobby", steam.CurrentLobbyId == 0 ? ctx.T("common.none") : steam.CurrentLobbyId.ToString()), OnlineUiTheme.MutedLabel());
		GUILayout.Label(ctx.F("network.role", ctx.RoleName(session.Role)), OnlineUiTheme.MutedLabel());
		GUILayout.Label(ctx.F("network.handshake", ctx.T(session.SessionActive ? "common.active" : "common.idle")), OnlineUiTheme.MutedLabel());
		GUILayout.Label(ctx.F("network.entity_sync", ctx.T(ctx.Entities.EntitySyncActive ? "common.active" : "common.off")), OnlineUiTheme.MutedLabel());
		GUILayout.Label(ctx.F("network.local_player", ctx.T(session.LocalInWorld ? "common.in_world" : "common.menu")), OnlineUiTheme.MutedLabel());
		GUILayout.Label(session.LastRttMs >= 0f ? ctx.F("network.last_rtt", $"{session.LastRttMs:F1} ms") : ctx.T("common.no_ping"), OnlineUiTheme.MutedLabel());

		GUILayout.Space(8f);
		GUILayout.Label(ctx.T("network.peer_rtt"), OnlineUiTheme.Section());
		foreach (var member in session.Members)
		{
			var name = DisplayName(steam, member.SteamId);
			var rtt = member.RttMs >= 0f ? $"{member.RttMs:F0} ms" : ctx.T("common.pending");
			GUILayout.Label($"{name}: {rtt}", OnlineUiTheme.MutedLabel());
		}
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
