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
/// The answer today is <see cref="Audience.Members"/> for all sixteen, and the reason
/// is the member's own Admin page: its read-only branch renders every rule
/// (<c>OnlineUiAdminDrawer</c>), so a rule's value IS read on a member's screen. Three
/// of them are read by behaviour as well — <see cref="HostRuleId.RemoteBackpackAccess"/>
/// decides the member card's backpack and take affordances (and whether the remote view
/// opens operable, view-only or not at all), <see cref="HostRuleId.AllowMemberCheatVerbs"/>
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

	/// <summary>
	/// The accepted value range of a numeric rule, as one closed interval. It is declared here
	/// because the value shape is the rule's own fact: the Admin page's slider and the console
	/// verb's refusal both read it, so neither can drift from the other.
	/// </summary>
	public readonly record struct ValueRange(float Minimum, float Maximum);

	/// <summary>
	/// One census row: the rule, its audience, its value shape (with a numeric rule's range), the
	/// console name a player types, and the localization key its label uses. The member's read-only
	/// Admin summary, the numeric row's slider bounds and the console's <c>/gamerule</c> verb all
	/// read it; the host's Admin rows stay explicit because each one binds its own setter.
	/// </summary>
	public sealed record Row(HostRuleId Id, Audience Audience, HostRuleKind Kind, string Name, string LabelKey, ValueRange? Range = null);

	/// <summary>The census, in the order the Admin page renders it.</summary>
	public static IReadOnlyList<Row> Rows { get; } =
	[
		new(HostRuleId.PvpEnabled, Audience.Members, HostRuleKind.Boolean, "pvp_enabled", "admin.rule_pvp"),
		new(HostRuleId.AutoContinue, Audience.Members, HostRuleKind.Boolean, "auto_continue", "admin.rule_auto_continue"),
		new(HostRuleId.AllowLateJoin, Audience.Members, HostRuleKind.Boolean, "allow_late_join", "admin.rule_allow_late_join"),
		new(HostRuleId.RemoteBackpackAccess, Audience.Members, HostRuleKind.Access, "remote_backpack_access", "admin.rule_remote_backpack_access"),
		new(HostRuleId.AllowMemberCheatVerbs, Audience.Members, HostRuleKind.Boolean, "allow_member_cheat_verbs", "admin.rule_allow_member_cheat_verbs"),
		new(HostRuleId.WidenRunSettings, Audience.Members, HostRuleKind.Boolean, "widen_run_settings", "admin.rule_widen_run_settings"),
		new(HostRuleId.PiggybackWeightMultiplier, Audience.Members, HostRuleKind.Number, "piggyback_weight_multiplier", "admin.rule_piggyback_weight", new ValueRange(0f, 3f)),
		new(HostRuleId.NativeBindingParity, Audience.Members, HostRuleKind.Parity, "native_binding_parity", "admin.rule_native_binding_parity"),
		new(HostRuleId.SaveInventory, Audience.Members, HostRuleKind.Boolean, "save_inventory", "admin.rule_save_inventory"),
		new(HostRuleId.ReviveFromTrader, Audience.Members, HostRuleKind.Boolean, "revive_from_trader", "admin.rule_revive_trader"),
		new(HostRuleId.ReviveOnNextLevel, Audience.Members, HostRuleKind.Boolean, "revive_on_next_level", "admin.rule_revive_next_level"),
		new(HostRuleId.Permadeath, Audience.Members, HostRuleKind.Boolean, "permadeath", "admin.rule_permadeath"),
		new(HostRuleId.RadiationLineStopsAboveTheFloor, Audience.Members, HostRuleKind.Boolean, "radiation_line_stops_above_the_floor", "admin.rule_radiation_line_floor"),
		new(HostRuleId.SpectatorFreeFly, Audience.Members, HostRuleKind.Boolean, "spectator_free_fly", "admin.rule_spectator_free_fly"),
		new(HostRuleId.SpectateWhileUnconscious, Audience.Members, HostRuleKind.Boolean, "spectate_while_unconscious", "admin.rule_spectate_while_unconscious"),
		new(HostRuleId.SpectateWhileWaitingToJoin, Audience.Members, HostRuleKind.Boolean, "spectate_while_waiting_to_join", "admin.rule_spectate_while_waiting_to_join"),
	];

	/// <summary>The rows a MEMBER's client renders and acts on — the set the announcement carries.</summary>
	public static IReadOnlyList<Row> MemberRows { get; } = [.. Rows.Where(row => row.Audience == Audience.Members)];

	private static readonly IReadOnlyDictionary<HostRuleId, Row> ById = Rows.ToDictionary(row => row.Id);

	private static readonly IReadOnlyDictionary<string, Row> ByName = Rows.ToDictionary(row => row.Name, StringComparer.Ordinal);

	/// <summary>The console names, sorted for the one answer that lists them (an unknown rule).</summary>
	public static IReadOnlyList<string> Names { get; } = [.. Rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal)];

	/// <summary>
	/// The row a player's <c>/gamerule</c> argument names, by exact ordinal match: Minecraft's arguments
	/// are case-sensitive and these names are the rule's own spelling, so a config-style camel-case name
	/// is a typo the verb reports rather than a second accepted spelling.
	/// </summary>
	public static Row? Find(string name) => ByName.TryGetValue(name, out var row) ? row : null;

	/// <summary>
	/// The one row for <paramref name="rule"/>. Throws for a rule the census does not
	/// classify: an unclassified rule must fail loudly rather than inherit whichever
	/// audience a reader assumed.
	/// </summary>
	public static Row Of(HostRuleId rule) => ById.TryGetValue(rule, out var row)
		? row
		: throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule has no census row");
}
