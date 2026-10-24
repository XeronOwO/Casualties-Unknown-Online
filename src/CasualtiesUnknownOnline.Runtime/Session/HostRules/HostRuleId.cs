namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// One host rule, named exactly as <see cref="IHostRules"/> names it. It is the key
/// the census, the announcement and every local check share: a rule is addressed by
/// this id rather than by one of its readers, so "which rules does a member's client
/// act on" is answered once
/// (<see cref="HostRuleCensus"/>) instead of once per call site.
///
/// <para>
/// The enum's names are the rule's own identity. The console's spelling (lowercase snake case) is
/// declared per rule in <see cref="HostRuleCensus"/>, and a refusal names that spelling, so the
/// operator learns the word the verb actually accepts.
/// </para>
/// </summary>
public enum HostRuleId
{
	PvpEnabled,
	AutoContinue,
	AllowLateJoin,
	RemoteBackpackAccess,
	AllowMemberCheatVerbs,
	WidenRunSettings,
	PiggybackWeightMultiplier,
	NativeBindingParity,
	SaveInventory,
	ReviveFromTrader,
	ReviveOnNextLevel,
	Permadeath,

	/// <summary>The radiation line stops a margin above the layer floor instead of descending past it (the host rule the user asked to align with).</summary>
	RadiationLineStopsAboveTheFloor,

	/// <summary>
	/// The end-of-layer panel's two choices appear on the HOST's screen only when this share of the LIVING players
	/// has finished the layer (0..1). A member's Admin page renders it and an op'd member may set it through
	/// `/gamerule`, so the value is announced like every other rule.
	/// </summary>
	LayerCompleteLivingShare,
}
