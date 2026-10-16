namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// One host rule, named exactly as <see cref="IHostRules"/> names it. It is the key
/// the census, the announcement and every local check share: a rule is addressed by
/// this id rather than by one of its readers, so "which rules does a member's client
/// act on" is answered once
/// (<see cref="HostRuleCensus"/>) instead of once per call site.
///
/// <para>
/// The names are the config keys the player sees (<c>HostRulesOptions</c> /
/// <c>RespawnOptions</c>), so a refusal can name the rule it acted on in the
/// operator's own vocabulary.
/// </para>
/// </summary>
public enum HostRuleId
{
	PvpEnabled,
	AutoContinue,
	AllowLateJoin,
	AllowRemoteInventoryTake,
	AllowMemberItemDataWrite,
	WidenRunSettings,
	PiggybackWeightMultiplier,
	NativeBindingParity,
	SaveInventory,
	ReviveFromTrader,
	ReviveOnNextLevel,
	Permadeath,

	/// <summary>The radiation line stops a margin above the layer floor instead of descending past it (the host rule the user asked to align with).</summary>
	RadiationLineStopsAboveTheFloor,
}
