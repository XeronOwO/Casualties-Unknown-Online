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

		GUILayout.Label(ctx.T("admin.host_rules"), OnlineUiTheme.Section());
		if (!isHost)
		{
			GUILayout.Label(ctx.T("admin.host_only"), OnlineUiTheme.MutedLabel());
		}

		var rules = ctx.HostRules;
		DrawRule(ctx.T("admin.rule_pvp"), rules.PvpEnabled, ctx);
		DrawRule(ctx.T("admin.rule_auto_continue"), rules.AutoContinue, ctx);
		DrawRule(ctx.T("admin.rule_allow_late_join"), rules.AllowLateJoin, ctx);
		DrawRule(ctx.T("admin.rule_save_inventory"), rules.SaveInventory, ctx);
		DrawRule(ctx.T("admin.rule_revive_trader"), rules.ReviveFromTrader, ctx);
		DrawRule(ctx.T("admin.rule_revive_next_level"), rules.ReviveOnNextLevel, ctx);
		DrawRule(ctx.T("admin.rule_permadeath"), rules.Permadeath, ctx);

		GUILayout.Space(10f);
		GUILayout.Label(ctx.T("admin.ban_list"), OnlineUiTheme.Section());
		var bans = ctx.HostBan.BannedSteamIds;
		if (bans.Count == 0)
		{
			GUILayout.Label(ctx.T("admin.no_bans"), OnlineUiTheme.MutedLabel());
		}

		foreach (var steamId in bans)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label($"{DisplayName(ctx.Steam, steamId)} [{steamId:X}]", OnlineUiTheme.Label());
			GUILayout.FlexibleSpace();
			if (isHost && GUILayout.Button(ctx.T("admin.unban"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
			{
				ctx.UnbanMember?.Invoke(steamId);
			}

			GUILayout.EndHorizontal();
		}
	}

	private static void DrawRule(string label, bool value, OnlineUiContext ctx)
	{
		var text = ctx.T(value ? "admin.rule_enabled" : "admin.rule_disabled");
		var color = value ? OnlineUiTheme.Positive : OnlineUiTheme.Muted;
		GUILayout.Label($"{label}: <color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>", OnlineUiTheme.Label());
	}

	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
