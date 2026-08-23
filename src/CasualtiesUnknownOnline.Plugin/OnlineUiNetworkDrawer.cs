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

		GUILayout.Label("CONNECTION", OnlineUiTheme.Section());
		GUILayout.Label($"Steam: {(steam.IsInitialized ? "initialized" : "not initialized")}", OnlineUiTheme.Label());
		GUILayout.Label($"Lobby: {(steam.CurrentLobbyId == 0 ? "none" : steam.CurrentLobbyId.ToString())}", OnlineUiTheme.MutedLabel());
		GUILayout.Label($"Role: {session.Role}", OnlineUiTheme.MutedLabel());
		GUILayout.Label($"Handshake: {(session.SessionActive ? "active" : "idle")}", OnlineUiTheme.MutedLabel());
		GUILayout.Label($"Entity sync: {(ctx.Entities.EntitySyncActive ? "active" : "off")}", OnlineUiTheme.MutedLabel());
		GUILayout.Label($"Local player: {(session.LocalInWorld ? "in world" : "menu")}", OnlineUiTheme.MutedLabel());
		GUILayout.Label($"Last RTT: {(session.LastRttMs >= 0f ? $"{session.LastRttMs:F1} ms" : "no ping yet")}", OnlineUiTheme.MutedLabel());

		GUILayout.Space(8f);
		GUILayout.Label("PEER RTT", OnlineUiTheme.Section());
		foreach (var member in session.Members)
		{
			var name = DisplayName(steam, member.SteamId);
			var rtt = member.RttMs >= 0f ? $"{member.RttMs:F0} ms" : "pending";
			GUILayout.Label($"{name}: {rtt}", OnlineUiTheme.MutedLabel());
		}
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
