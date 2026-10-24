using System;
using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host's rules as ONE absolute value set: one field per
/// <see cref="IHostRules"/> member, same names, same types, so the whole rule
/// surface travels, compares and is read by id as a single fact.
///
/// <para>
/// It exists because a rule is not one process's private configuration any more: a
/// member's client reads the values the HOST is enforcing (the member list's take
/// affordance, the console's write verb, the carrier's own encumbrance, the Admin
/// page's read-only summary), and the only shape that can be announced, re-announced
/// and applied idempotently is the complete set rather than a delta. The same shape
/// is what <see cref="HostRuleCensus"/> classifies per rule and what
/// <see cref="AnnouncedHostRules"/> stores on the receiving side.
/// </para>
///
/// <para>
/// Its field set is pinned to <see cref="IHostRules"/> by the census gate: a rule
/// added to the interface without a field here fails that gate, so no rule can
/// quietly skip the "does a member read this?" question.
/// </para>
/// </summary>
public readonly record struct HostRuleSet(
	bool PvpEnabled,
	bool AutoContinue,
	bool AllowLateJoin,
	RemoteBackpackAccess RemoteBackpackAccess,
	bool AllowMemberCheatVerbs,
	bool WidenRunSettings,
	float PiggybackWeightMultiplier,
	NativeBindingParity NativeBindingParity,
	bool SaveInventory,
	bool ReviveFromTrader,
	bool ReviveOnNextLevel,
	bool Permadeath,
	bool RadiationLineStopsAboveTheFloor)
{
	/// <summary>Reads one process's own rule surface as a set (the host's own values, and a member's before anything is announced).</summary>
	public static HostRuleSet From(IHostRules rules) => new(
		rules.PvpEnabled,
		rules.AutoContinue,
		rules.AllowLateJoin,
		rules.RemoteBackpackAccess,
		rules.AllowMemberCheatVerbs,
		rules.WidenRunSettings,
		rules.PiggybackWeightMultiplier,
		rules.NativeBindingParity,
		rules.SaveInventory,
		rules.ReviveFromTrader,
		rules.ReviveOnNextLevel,
		rules.Permadeath,
		rules.RadiationLineStopsAboveTheFloor);

	/// <summary>The set the two config sections compose into — the mapping <see cref="IHostRules"/> itself declares (the respawn rules come from <see cref="RespawnOptions"/>, under the names the rule surface uses).</summary>
	public static HostRuleSet FromOptions(HostRulesOptions hostRules, RespawnOptions respawn) => new(
		hostRules.PvpEnabled,
		hostRules.AutoContinue,
		hostRules.AllowLateJoin,
		hostRules.RemoteBackpackAccess,
		hostRules.AllowMemberCheatVerbs,
		hostRules.WidenRunSettings,
		hostRules.PiggybackWeightMultiplier,
		hostRules.NativeBindingParity,
		respawn.RespawnKeepInventory,
		respawn.ReviveFromTrader,
		respawn.ReviveOnNextLevel,
		respawn.Permadeath,
		hostRules.RadiationLineStopsAboveTheFloor);

	/// <summary>
	/// The value of one flag rule, by id. Throws for a rule that is not a flag: a
	/// reader that asks the wrong shape must fail loudly rather than read a
	/// neighbouring field's meaning (the same rule the census gate enforces).
	/// </summary>
	public bool Boolean(HostRuleId rule) => rule switch
	{
		HostRuleId.PvpEnabled => PvpEnabled,
		HostRuleId.AutoContinue => AutoContinue,
		HostRuleId.AllowLateJoin => AllowLateJoin,
		HostRuleId.AllowMemberCheatVerbs => AllowMemberCheatVerbs,
		HostRuleId.WidenRunSettings => WidenRunSettings,
		HostRuleId.SaveInventory => SaveInventory,
		HostRuleId.ReviveFromTrader => ReviveFromTrader,
		HostRuleId.ReviveOnNextLevel => ReviveOnNextLevel,
		HostRuleId.Permadeath => Permadeath,
		HostRuleId.RadiationLineStopsAboveTheFloor => RadiationLineStopsAboveTheFloor,
		_ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule is not a flag"),
	};

	/// <summary>The value of the one scalar rule, by id.</summary>
	public float Number(HostRuleId rule) => rule switch
	{
		HostRuleId.PiggybackWeightMultiplier => PiggybackWeightMultiplier,
		_ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule is not a scalar"),
	};

	/// <summary>The value of the one three-way rule, by id.</summary>
	public NativeBindingParity Parity(HostRuleId rule) => rule switch
	{
		HostRuleId.NativeBindingParity => NativeBindingParity,
		_ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule is not the parity level"),
	};

	/// <summary>The value of the remote-backpack access rule, by id.</summary>
	public RemoteBackpackAccess Access(HostRuleId rule) => rule switch
	{
		HostRuleId.RemoteBackpackAccess => RemoteBackpackAccess,
		_ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "host rule is not the backpack access level"),
	};
}
