namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The value shape of one host rule — which of <see cref="HostRuleSet"/>'s typed
/// accessors answers it, and therefore how a reader that walks the census renders it.
/// </summary>
public enum HostRuleKind
{
	/// <summary>A flag: the rule is on or off (<see cref="HostRuleSet.Boolean"/>).</summary>
	Boolean,

	/// <summary>A scalar the rule is stored at (<see cref="HostRuleSet.Number"/>).</summary>
	Number,

	/// <summary>The three-way native-binding parity level (<see cref="HostRuleSet.Parity"/>).</summary>
	Parity,
}
