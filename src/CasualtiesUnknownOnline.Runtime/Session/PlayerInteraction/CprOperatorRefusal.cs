namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Why an operator's own body refused a CPR press (ticket `remote-medical-cpr.md`, stage 4). The two are separate because
/// the player's fix differs: one is training, the other is rest. The catalogue key for each belongs to the client that
/// shows the line, not to this rule.
/// </summary>
public enum CprOperatorRefusal
{
	/// <summary>Nothing refused it: the press may proceed.</summary>
	None = 0,

	/// <summary>The reference's strength gate failed: this body cannot push hard enough.</summary>
	TooWeak = 1,

	/// <summary>Stamina is at the floor: this body has nothing left to give.</summary>
	Exhausted = 2,
}
