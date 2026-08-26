using System;
using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Admin page: the host-only rule/ban surfaces. Hosts can toggle the host-rule
/// and respawn flags directly; guests see the read-only summary. The ban list
/// is directly manageable through the existing host-ban service.
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
		if (isHost && ctx.RulesEditor is { } editor)
		{
			DrawEditableRule(ctx, "admin.rule_pvp", rules.PvpEnabled, editor.SetPvpEnabled);
			DrawEditableRule(ctx, "admin.rule_auto_continue", rules.AutoContinue, editor.SetAutoContinue);
			DrawEditableRule(ctx, "admin.rule_allow_late_join", rules.AllowLateJoin, editor.SetAllowLateJoin);
			DrawEditableRule(ctx, "admin.rule_allow_remote_inventory_take", rules.AllowRemoteInventoryTake, editor.SetAllowRemoteInventoryTake);
			DrawEditableRule(ctx, "admin.rule_widen_run_settings", rules.WidenRunSettings, editor.SetWidenRunSettings);
			DrawEditableNumberRule(ctx, "admin.rule_piggyback_weight", rules.PiggybackWeightMultiplier, v => editor.SetPiggybackWeightMultiplier(v), 0f, 3f);
			DrawEditableRule(ctx, "admin.rule_save_inventory", rules.SaveInventory, editor.SetKeepInventory);
			DrawEditableRule(ctx, "admin.rule_revive_trader", rules.ReviveFromTrader, editor.SetReviveFromTrader);
			DrawEditableRule(ctx, "admin.rule_revive_next_level", rules.ReviveOnNextLevel, editor.SetReviveOnNextLevel);
			DrawEditableRule(ctx, "admin.rule_permadeath", rules.Permadeath, editor.SetPermadeath);
		}
		else
		{
			DrawRule(ctx.T("admin.rule_pvp"), rules.PvpEnabled, ctx);
			DrawRule(ctx.T("admin.rule_auto_continue"), rules.AutoContinue, ctx);
			DrawRule(ctx.T("admin.rule_allow_late_join"), rules.AllowLateJoin, ctx);
			DrawRule(ctx.T("admin.rule_allow_remote_inventory_take"), rules.AllowRemoteInventoryTake, ctx);
			DrawRule(ctx.T("admin.rule_widen_run_settings"), rules.WidenRunSettings, ctx);
			DrawNumberRule(ctx, "admin.rule_piggyback_weight", rules.PiggybackWeightMultiplier);
			DrawRule(ctx.T("admin.rule_save_inventory"), rules.SaveInventory, ctx);
			DrawRule(ctx.T("admin.rule_revive_trader"), rules.ReviveFromTrader, ctx);
			DrawRule(ctx.T("admin.rule_revive_next_level"), rules.ReviveOnNextLevel, ctx);
			DrawRule(ctx.T("admin.rule_permadeath"), rules.Permadeath, ctx);
		}

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
			GUILayout.Label($"{ctx.DisplayName(steamId)} [{steamId:X}]", OnlineUiTheme.Label());
			GUILayout.FlexibleSpace();
			if (isHost && GUILayout.Button(ctx.T("admin.unban"), OnlineUiTheme.Button(), GUILayout.Width(70f)))
			{
				ctx.UnbanMember?.Invoke(steamId);
			}

			GUILayout.EndHorizontal();
		}
	}

	private static void DrawNumberRule(OnlineUiContext ctx, string labelKey, float value) =>
		GUILayout.Label($"{ctx.T(labelKey)}: {value:F2}", OnlineUiTheme.Label());

	private static void DrawEditableNumberRule(
		OnlineUiContext ctx,
		string labelKey,
		float value,
		Action<float> setter,
		float min,
		float max)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T(labelKey), OnlineUiTheme.Label());
		GUILayout.FlexibleSpace();
		var next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(120f));
		GUILayout.Label($"{next:F2}", OnlineUiTheme.MutedLabel(), GUILayout.Width(36f));
		if (Math.Abs(next - value) > 0.001f)
		{
			setter(next);
		}

		GUILayout.EndHorizontal();
	}

	private static void DrawRule(string label, bool value, OnlineUiContext ctx)
	{
		var text = ctx.T(value ? "admin.rule_enabled" : "admin.rule_disabled");
		var color = value ? OnlineUiTheme.Positive : OnlineUiTheme.Muted;
		GUILayout.Label($"{label}: <color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>", OnlineUiTheme.Label());
	}

	private static void DrawEditableRule(OnlineUiContext ctx, string labelKey, bool value, Action<bool> setter)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(ctx.T(labelKey), OnlineUiTheme.Label());
		GUILayout.FlexibleSpace();
		var next = GUILayout.Toggle(value, ctx.T(value ? "admin.rule_enabled" : "admin.rule_disabled"), OnlineUiTheme.Button());
		if (next != value)
		{
			setter(next);
		}

		GUILayout.EndHorizontal();
	}
}
