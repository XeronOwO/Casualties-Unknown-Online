using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host-rule census: one row per rule, answering the ONE question that decides
/// whether a rule travels — does a member's client read it?
///
/// <para>
/// A rule only the host executes needs no replication; a rule whose value a member's
/// screen, console or local gesture reads does. The question is answered here, per
/// rule, and kept answered: <see cref="Of"/> throws for a rule this table does not
/// name, and the census gate reflects <see cref="IHostRules"/> and fails when a rule
/// is missing, so a rule added later cannot skip the question by never being asked
/// (the shape <c>WireCommandAuthorityPolicy</c> uses for wire kinds).
/// </para>
///
/// <para>
/// The answer today is <see cref="Audience.Members"/> for all thirteen, and the reason
/// is the member's own Admin page: its read-only branch renders every rule
/// (<c>OnlineUiAdminDrawer</c>), so a rule's value IS read on a member's screen. Three
/// of them are read by behaviour as well — <see cref="HostRuleId.AllowRemoteInventoryTake"/>
/// decides the member card's take affordance, <see cref="HostRuleId.AllowMemberItemDataWrite"/>
/// decides the member's own console write, and <see cref="HostRuleId.PiggybackWeightMultiplier"/>
/// is read by the member whose body is carrying someone
/// (<c>CarriagePatchBridge.GetCarriedEncumbrance</c> returns for the LOCAL body only).
/// A future rule that neither a member's screen nor a member's gesture reads is
/// declared <see cref="Audience.HostOnly"/> and stays off the wire — and off the
/// member's screen, which the read-only branch walks <see cref="MemberRows"/> for.
/// </para>
/// </summary>
public static class HostRuleCensus
{
	/// <summary>Whether a rule's value reaches a member's client at all.</summary>
	public enum Audience
	{
		/// <summary>A member's screen, console or local gesture reads it: the value is announced.</summary>
		Members,

		/// <summary>Only the host reads it: the rule stays off the wire and off the member's screen.</summary>
		HostOnly,
	}

	/// <summary>One census row: the rule, its audience, its value shape, and the localization key its label uses (so the census is also what a rule list renders from).</summary>
	public sealed record Row(HostRuleId Id, Audience Audience, HostRuleKind Kind, string LabelKey);

	/// <summary>The census, in the order the Admin page renders it.</summary>
	public static IReadOnlyList<Row> Rows { get; } =
	[
		new(HostRuleId.PvpEnabled, Audience.Members, HostRuleKind.Boolean, "admin.rule_pvp"),
		new(HostRuleId.AutoContinue, Audience.Members, HostRuleKind.Boolean, "admin.rule_auto_continue"),
		new(HostRuleId.AllowLateJoin, Audience.Members, HostRuleKind.Boolean, "admin.rule_allow_late_join"),
		new(HostRuleId.AllowRemoteInventoryTake, Audience.Members, HostRuleKind.Boolean, "admin.rule_allow_remote_inventory_take"),
		new(HostRuleId.AllowMemberItemDataWrite, Audience.Members, HostRuleKind.Boolean, "admin.rule_allow_member_item_data_write"),
		new(HostRuleId.WidenRunSettings, Audience.Members, HostRuleKind.Boolean, "admin.rule_widen_run_settings"),
		new(HostRuleId.PiggybackWeightMultiplier, Audience.Members, HostRuleKind.Number, "admin.rule_piggyback_weight"),
		new(HostRuleId.NativeBindingParity, Audience.Members, HostRuleKind.Parity, "admin.rule_native_binding_parity"),
		new(HostRuleId.SaveInventory, Audience.Members, HostRuleKind.Boolean, "admin.rule_save_inventory"),
		new(HostRuleId.ReviveFromTrader, Audience.Members, HostRuleKind.Boolean, "admin.rule_revive_trader"),
		new(HostRuleId.ReviveOnNextLevel, Audience.Members, HostRuleKind.Boolean, "admin.rule_revive_next_level"),
		new(HostRuleId.Permadeath, Audience.Members, HostRuleKind.Boolean, "admin.rule_permadeath"),
		new(HostRuleId.RadiationLineStopsAboveTheFloor, Audience.Members, HostRuleKind.Boolean, "admin.rule_radiation_line_floor"),
	];

	/// <summary>The rows a MEMBER's client renders and acts on — the set the announcement carries.</summary>
	public static IReadOnlyList<Row> MemberRows { get; } = [.. Rows.Where(row => row.Audience == Audience.Members)];

	private static readonly IReadOnlyDictionary<HostRuleId, Row> ById = Rows.ToDictionary(row => row.Id);

	/// <summary>
	/// The one row for <paramref name="rule"/>. Throws for a rule the census does not
	/// classify: an unclassified rule must fail loudly rather than inherit whichever
	/// audience a reader assumed.
	/// </summary>
	public static Row Of(HostRuleId rule) => ById.TryGetValue(rule, out var row)
		? row
		: throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule has no census row");
}
