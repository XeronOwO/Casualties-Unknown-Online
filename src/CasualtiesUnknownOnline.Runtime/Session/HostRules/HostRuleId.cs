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

	/// <summary>A spectating player may free-fly the camera instead of only following a teammate. On by default, aligned with the reference mod: a free camera can scout the layer ahead, so it is a group decision rather than one client's rendering choice.</summary>
	SpectatorFreeFly,

	/// <summary>An UNCONSCIOUS player may spectate. Off by default: in this project's model an unconscious body is still its owner's to act on, so this changes a live player's client rather than a dead one's.</summary>
	SpectateWhileUnconscious,

	/// <summary>A client that has not entered the world yet may spectate. Off by default: that is the late-join case, and it is a separate promise about what a not-yet-in-world client may see.</summary>
	SpectateWhileWaitingToJoin,
}
