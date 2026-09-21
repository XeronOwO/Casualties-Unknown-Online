namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// How a host treats a member whose declared native binding differs from the
/// host's own for a mod BOTH sides list (`docs/api/mod-api.md` §5). Parity
/// compares declarations only: an undeclared binding stays invisible, so an equal
/// declaration is visibility, never proof of identical behaviour.
/// </summary>
public enum NativeBindingParity
{
	/// <summary>No parity check — a difference is not even recorded.</summary>
	Allow,

	/// <summary>The member is admitted and the mismatch is recorded in the host's log. The default: the declaration is new, so a host that refused by default would lock out every session whose host updated first.</summary>
	Warn,

	/// <summary>The member is refused, naming the mod and both declarations.</summary>
	Require,
}
