using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Admin page: the host-only rule/ban surfaces. Hosts edit the host-rule and respawn flags directly;
/// guests see the read-only summary. The ban list is directly manageable through the existing host-ban
/// service.
///
/// <para>
/// Since ticket online-ui-art-and-controls-overhaul (S2b) the page builds a display list instead of
/// drawing itself, and each rule is the game's own settings row: a label with the rule's name and the
/// game's control beside it — a checkbox for a flag, a slider for the weight multiplier and the game's
/// dropdown for the three-way parity level (the IMGUI toolbar it replaces was the "choice drawn as
/// buttons" shape the overhaul exists to remove).
/// </para>
/// </summary>
internal static class OnlineUiAdminDrawer
{
	private const float NumberRuleWidth = 120f;

	internal static void Build(OnlineUiContext ctx, OnlineUiPageBuilder page)
	{
		var session = ctx.Session;
		var isHost = session.Role == SessionRole.Host && session.SessionActive;

		page.Section(ctx.T("admin.host_rules"));
		if (!isHost)
		{
			page.Muted(ctx.T("admin.host_only"));
		}

		var rules = ctx.HostRules;
		if (isHost && ctx.RulesEditor is { } editor)
		{
			BuildEditableRule(ctx, page, "admin.rule_pvp", rules.PvpEnabled, editor.SetPvpEnabled);
			BuildEditableRule(ctx, page, "admin.rule_auto_continue", rules.AutoContinue, editor.SetAutoContinue);
			BuildEditableRule(ctx, page, "admin.rule_allow_late_join", rules.AllowLateJoin, editor.SetAllowLateJoin);
			BuildEditableRule(ctx, page, "admin.rule_allow_remote_inventory_take", rules.AllowRemoteInventoryTake, editor.SetAllowRemoteInventoryTake);
			BuildEditableRule(ctx, page, "admin.rule_widen_run_settings", rules.WidenRunSettings, editor.SetWidenRunSettings);
			BuildEditableNumberRule(ctx, page, "admin.rule_piggyback_weight", rules.PiggybackWeightMultiplier, value => editor.SetPiggybackWeightMultiplier(value), 0f, 3f);
			BuildEditableParityRule(ctx, page, "admin.rule_native_binding_parity", rules.NativeBindingParity, editor.SetNativeBindingParity);
			BuildEditableRule(ctx, page, "admin.rule_save_inventory", rules.SaveInventory, editor.SetKeepInventory);
			BuildEditableRule(ctx, page, "admin.rule_revive_trader", rules.ReviveFromTrader, editor.SetReviveFromTrader);
			BuildEditableRule(ctx, page, "admin.rule_revive_next_level", rules.ReviveOnNextLevel, editor.SetReviveOnNextLevel);
			BuildEditableRule(ctx, page, "admin.rule_permadeath", rules.Permadeath, editor.SetPermadeath);
		}
		else
		{
			BuildRule(ctx, page, "admin.rule_pvp", rules.PvpEnabled);
			BuildRule(ctx, page, "admin.rule_auto_continue", rules.AutoContinue);
			BuildRule(ctx, page, "admin.rule_allow_late_join", rules.AllowLateJoin);
			BuildRule(ctx, page, "admin.rule_allow_remote_inventory_take", rules.AllowRemoteInventoryTake);
			BuildRule(ctx, page, "admin.rule_widen_run_settings", rules.WidenRunSettings);
			BuildNumberRule(ctx, page, "admin.rule_piggyback_weight", rules.PiggybackWeightMultiplier);
			BuildParityRule(ctx, page, "admin.rule_native_binding_parity", rules.NativeBindingParity);
			BuildRule(ctx, page, "admin.rule_save_inventory", rules.SaveInventory);
			BuildRule(ctx, page, "admin.rule_revive_trader", rules.ReviveFromTrader);
			BuildRule(ctx, page, "admin.rule_revive_next_level", rules.ReviveOnNextLevel);
			BuildRule(ctx, page, "admin.rule_permadeath", rules.Permadeath);
		}

		page.Space();
		page.Section(ctx.T("admin.ban_list"));
		var bans = ctx.HostBan.BannedSteamIds;
		if (bans.Count == 0)
		{
			page.Muted(ctx.T("admin.no_bans"));
		}

		foreach (var steamId in bans)
		{
			var entry = $"{ctx.DisplayName(steamId)} [{steamId:X}]";
			if (!isHost)
			{
				page.Label(entry);
				continue;
			}

			page.Row(
				page.LabelElement(entry),
				page.ButtonElement($"admin.unban.{steamId:X}", ctx.T("admin.unban"), () => ctx.UnbanMember?.Invoke(steamId), width: 70f));
		}
	}

	/// <summary>The guest's read-only rule line: the name in the body colour and the state word in the
	/// state's own colour — the two-tone line the IMGUI page drew.</summary>
	private static void BuildRule(OnlineUiContext ctx, OnlineUiPageBuilder page, string labelKey, bool value)
	{
		var text = ctx.T(value ? "admin.rule_enabled" : "admin.rule_disabled");
		var color = value ? OnlineUiTheme.Positive : OnlineUiTheme.Muted;
		page.Label($"{ctx.T(labelKey)}: <color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>");
	}

	private static void BuildNumberRule(OnlineUiContext ctx, OnlineUiPageBuilder page, string labelKey, float value) =>
		page.Label($"{ctx.T(labelKey)}: {value:F2}");

	private static void BuildParityRule(OnlineUiContext ctx, OnlineUiPageBuilder page, string labelKey, NativeBindingParity value) =>
		page.Label($"{ctx.T(labelKey)}: {ParityLabels(ctx)[(int)value]}");

	/// <summary>The host's rule row: the game's own checkbox row, the rule's name as its label.</summary>
	private static void BuildEditableRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		bool value,
		Action<bool> setter) =>
		page.Toggle(labelKey, ctx.T(labelKey), value, setter);

	/// <summary>The host's numeric rule row: the game's own slider row with the value beside it.</summary>
	private static void BuildEditableNumberRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		float value,
		Action<float> setter,
		float min,
		float max) =>
		page.Slider(labelKey, ctx.T(labelKey), value, min, max, $"{value:F2}", setter, width: NumberRuleWidth);

	/// <summary>The three-way native-binding parity rule as the game's own dropdown row: a small closed
	/// set the player opens, tracks and picks from — the control the IMGUI toolbar only pretended to be.</summary>
	private static void BuildEditableParityRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		NativeBindingParity value,
		Action<NativeBindingParity> setter) =>
		page.Dropdown(
			labelKey,
			ctx.T(labelKey),
			ParityLabels(ctx),
			(int)value,
			index => setter((NativeBindingParity)index));

	private static string[] ParityLabels(OnlineUiContext ctx) =>
		[ctx.T("admin.parity_allow"), ctx.T("admin.parity_warn"), ctx.T("admin.parity_require")];
}
