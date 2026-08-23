using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Admin page: the host-only rule/ban surfaces. The host rules service is
/// read-only for now (config hot-reload is the edit path); the ban list is
/// directly manageable through the existing host-ban service.
/// </summary>
internal static class OnlineUiAdminDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		var session = ctx.Session;
		var isHost = session.Role == SessionRole.Host && session.SessionActive;

		GUILayout.Label("HOST RULES", OnlineUiTheme.Section());
		if (!isHost)
		{
			GUILayout.Label("Host-only page — current role cannot change host rules.", OnlineUiTheme.MutedLabel());
		}

		var rules = ctx.HostRules;
		DrawRule("PvP", rules.PvpEnabled);
		DrawRule("Auto-continue", rules.AutoContinue);
		DrawRule("Allow late join", rules.AllowLateJoin);
		DrawRule("Save inventory", rules.SaveInventory);
		DrawRule("Revive from trader", rules.ReviveFromTrader);
		DrawRule("Revive on next level", rules.ReviveOnNextLevel);
		DrawRule("Permadeath", rules.Permadeath);

		GUILayout.Space(10f);
		GUILayout.Label("BAN LIST", OnlineUiTheme.Section());
		var bans = ctx.HostBan.BannedSteamIds;
		if (bans.Count == 0)
		{
			GUILayout.Label("No banned players.", OnlineUiTheme.MutedLabel());
		}

		foreach (var steamId in bans)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label($"{DisplayName(ctx.Steam, steamId)} [{steamId:X}]", OnlineUiTheme.Label());
			GUILayout.FlexibleSpace();
			if (isHost && GUILayout.Button("Unban", OnlineUiTheme.Button(), GUILayout.Width(70f)))
			{
				ctx.UnbanMember?.Invoke(steamId);
			}

			GUILayout.EndHorizontal();
		}
	}

	private static void DrawRule(string label, bool value)
	{
		var text = value ? "enabled" : "disabled";
		var color = value ? OnlineUiTheme.Positive : OnlineUiTheme.Muted;
		GUILayout.Label($"{label}: <color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>", OnlineUiTheme.Label());
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
