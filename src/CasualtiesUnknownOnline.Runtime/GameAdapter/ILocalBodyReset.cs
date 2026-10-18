namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one scene capability behind the console cheat verb <c>/heal</c>: reset THIS client's own
/// body to the full healthy state <see cref="HealResetState"/> describes, including the structural
/// effects the field table cannot carry — severed limbs put back, the hollow debuff and its overlay
/// removed, splints/tourniquets and the medication components destroyed — then report the resulting
/// character snapshot so the host and every peer converge on it.
///
/// <para>
/// After a succeeded host instruction the report has two halves. The ordinary character snapshot
/// still goes out, but it MAY NOT state the three continuous values the host owns (blood volume,
/// total bleeding speed and per-limb bleed), so the reset's result also travels as the authority
/// event <c>HealResetApplied</c>, bound to the instruction id the host issued: the host stores it
/// through the character data's authority-merge seam (<c>ApplyHealResetEvent</c>), not through the
/// filtered report path. A host reset is its own authority — its snapshot is written authoritatively
/// already — and passes no id.
/// </para>
///
/// <para>
/// It is one call with one answer because the target's own client is the only place the body
/// exists: the host instructs, the target applies. A build with no game scene registers
/// <see cref="UnavailableLocalBodyReset"/> and the verb answers that it cannot reset a body instead
/// of silently doing nothing.
/// </para>
/// </summary>
public interface ILocalBodyReset
{
	/// <summary>
	/// Reset the local player's body now and report it. <paramref name="instructionId"/> is the
	/// host's ticket when a host instruction triggered this call (the target echoes it to the host
	/// with the reset result), or null when this client's own console triggered the reset (a host
	/// self-reset needs no event: its own snapshot is the authority write). False with
	/// <see cref="LocalBodyResetOutcome.Refusal"/> naming the ground when this client has no local
	/// body to reset (no world, still loading).
	/// </summary>
	LocalBodyResetOutcome Reset(ulong? instructionId);
}
