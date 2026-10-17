namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The HOST's own rulings on what a member's submission may reach — the second
/// question the admission seam asks, beside the per-kind authority declaration
/// (<see cref="WireCommandAuthorityPolicy"/>).
///
/// <para>
/// The two are deliberately different things. The authority declaration is a
/// property of the WIRE KIND: it says whether a member may author this shape of
/// report at all, and it is the same answer in every session of this build. A
/// ruling here is a property of the SESSION: the host's configuration and its
/// roster decide whether an otherwise member-authorable verb is open, so the same
/// report is refused on one host and admitted on another, and the host may change
/// its mind between two reports (every member is read per submission, never cached).
/// </para>
///
/// <para>
/// The cheat family has TWO rulings, and a member's verb needs both — the user's
/// 2026-10-18 ruling. <see cref="CheatVerbsOpenToMembers"/> is the host's switch: is
/// the family open to members at all (<c>HostRulesOptions.AllowMemberCheatVerbs</c>,
/// off by default). <see cref="AllowsCheatVerb"/> is the host's roster: WHO may ask
/// (<c>CheatPermissions</c>, edited by <c>/op</c> and <c>/deop</c>). The switch alone
/// admits nobody and a grant alone admits nothing; each refusal names its own
/// ground, so the operator learns whether to ask for the switch or for the grant.
/// The host's own verbs need neither — its standing is the online switch.
/// </para>
///
/// <para>
/// The port exists because both rulings live in the Runtime (the host's configuration
/// and its session state) and the seam lives in the Application layer, which may not
/// reference them: the Application declares the questions it needs answered and the
/// Runtime answers them. A build that registers no implementation fails at
/// construction rather than admitting everything — the family's default is OFF, and
/// an unanswered ruling must not read as consent.
/// </para>
/// </summary>
public interface IMemberCommandRules
{
	/// <summary>
	/// Whether the host's cheat SWITCH is open to members. False — the default —
	/// refuses every member's cheat verb by name at the seam, which is the broader of
	/// the two grounds: the family is closed regardless of who has a grant.
	/// </summary>
	bool CheatVerbsOpenToMembers { get; }

	/// <summary>
	/// Whether the host's cheat ROSTER names this member. Read per submission, so a
	/// revoke refuses the next request and leaves the committed ones alone. It is the
	/// WHO half of the pair: with the switch open and no grant, the verb is still
	/// refused, by this ground.
	/// </summary>
	bool AllowsCheatVerb(ulong steamId);
}
