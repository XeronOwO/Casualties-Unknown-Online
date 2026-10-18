namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one scene capability behind the console cheat verb <c>/heal</c>: reset THIS client's own
/// body to the full healthy state <see cref="HealResetState"/> describes, including the structural
/// effects the field table cannot carry — severed limbs put back, the hollow debuff and its overlay
/// removed, splints/tourniquets and the medication components destroyed — then re-report the
/// resulting character snapshot so the host and every peer converge on it.
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
	/// Reset the local player's body now and re-report it. False with
	/// <see cref="LocalBodyResetOutcome.Refusal"/> naming the ground when this client has no local
	/// body to reset (no world, still loading).
	/// </summary>
	LocalBodyResetOutcome Reset();
}
