using System;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
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
/// dropdown for a three-way level (the IMGUI toolbar it replaces was the "choice drawn as
/// buttons" shape the overhaul exists to remove).
/// </para>
/// </summary>
internal static class OnlineUiAdminDrawer
{
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
			BuildEditableAccessRule(ctx, page, "admin.rule_remote_backpack_access", rules.RemoteBackpackAccess, editor.SetRemoteBackpackAccess);
			BuildEditableRule(ctx, page, "admin.rule_allow_member_cheat_verbs", rules.AllowMemberCheatVerbs, editor.SetAllowMemberCheatVerbs);
			BuildEditableRule(ctx, page, "admin.rule_widen_run_settings", rules.WidenRunSettings, editor.SetWidenRunSettings);
			BuildEditableNumberRule(ctx, page, "admin.rule_piggyback_weight", HostRuleCensus.Of(HostRuleId.PiggybackWeightMultiplier), rules.PiggybackWeightMultiplier, value => editor.SetPiggybackWeightMultiplier(value));
			BuildEditableParityRule(ctx, page, "admin.rule_native_binding_parity", rules.NativeBindingParity, editor.SetNativeBindingParity);
			BuildEditableRule(ctx, page, "admin.rule_save_inventory", rules.SaveInventory, editor.SetKeepInventory);
			BuildEditableRule(ctx, page, "admin.rule_revive_trader", rules.ReviveFromTrader, editor.SetReviveFromTrader);
			BuildEditableRule(ctx, page, "admin.rule_revive_next_level", rules.ReviveOnNextLevel, editor.SetReviveOnNextLevel);
			BuildEditableRule(ctx, page, "admin.rule_permadeath", rules.Permadeath, editor.SetPermadeath);
			BuildEditableRule(ctx, page, "admin.rule_radiation_line_floor", rules.RadiationLineStopsAboveTheFloor, editor.SetRadiationLineStopsAboveTheFloor);
			BuildEditableNumberRule(ctx, page, "admin.rule_layer_complete_share", HostRuleCensus.Of(HostRuleId.LayerCompleteLivingShare), rules.LayerCompleteLivingShare, value => editor.SetLayerCompleteLivingShare(value));
		}
		else
		{
			// The member's read-only summary walks the CENSUS, not a second list: a rule
			// the census marks host-only is not announced, so a hardcoded list here would
			// quietly render this client's own config value under the host's rule name —
			// the lie this ticket removed. The values come from HostRulesService, which is
			// the host's announced set on a member.
			foreach (var row in HostRuleCensus.MemberRows)
			{
				page.Label($"{ctx.T(row.LabelKey)}: {ReadOnlyValue(ctx, rules.Current, row)}");
			}
		}

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

	/// <summary>The member's read-only rule line: the name in the body colour and the state word in the
	/// state's own colour — the two-tone line the IMGUI page drew.</summary>
	private static string ReadOnlyValue(OnlineUiContext ctx, HostRuleSet set, HostRuleCensus.Row row) => row.Kind switch
	{
		HostRuleKind.Number => set.Number(row.Id).ToString("F2", CultureInfo.InvariantCulture),
		HostRuleKind.Parity => ParityLabels(ctx)[(int)set.Parity(row.Id)],
		HostRuleKind.Access => AccessLabels(ctx)[(int)set.Access(row.Id)],
		_ => TwoTone(ctx, set.Boolean(row.Id)),
	};

	private static string TwoTone(OnlineUiContext ctx, bool value)
	{
		var text = ctx.T(value ? "admin.rule_enabled" : "admin.rule_disabled");
		var color = value ? OnlineUiTheme.Positive : OnlineUiTheme.Muted;
		return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
	}

	/// <summary>The host's rule row: the game's own checkbox row, the rule's name as its label.</summary>
	private static void BuildEditableRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		bool value,
		Action<bool> setter) =>
		page.Toggle(labelKey, ctx.T(labelKey), value, setter);

	/// <summary>The host's numeric rule row (ticket online-ui-paired-controls-and-page-scroll): the game's own
	/// slider with an editable value beside it, both bound to the one rule — the name on its own line above
	/// them. The bounds are the census's declared range, the same one the console's <c>/gamerule</c> refusal
	/// names, so the slider and the verb cannot disagree about what the rule accepts. The label key stays the
	/// literal the editor passes (the localisation census counts a literal handed to a label parameter as the
	/// key's reader); the number is DISPLAYED to two places, while the rule is stored as a float and the
	/// console accepts its full form.</summary>
	private static void BuildEditableNumberRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		HostRuleCensus.Row rule,
		float value,
		Action<float> setter)
	{
		var range = rule.Range ?? throw new InvalidOperationException($"host rule '{rule.Name}' is numeric but declares no value range");
		page.NumberRule(labelKey, ctx.T(labelKey), value, range.Minimum, range.Maximum, setter, format: value => $"{value:F2}");
	}

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

	/// <summary>The remote-backpack access rule as the game's own dropdown row, in the enum's own order.</summary>
	private static void BuildEditableAccessRule(
		OnlineUiContext ctx,
		OnlineUiPageBuilder page,
		string labelKey,
		RemoteBackpackAccess value,
		Action<RemoteBackpackAccess> setter) =>
		page.Dropdown(
			labelKey,
			ctx.T(labelKey),
			AccessLabels(ctx),
			(int)value,
			index => setter((RemoteBackpackAccess)index));

	private static string[] AccessLabels(OnlineUiContext ctx) =>
		[ctx.T("admin.access_allow"), ctx.T("admin.access_unconscious_or_dead"), ctx.T("admin.access_deny")];
}
