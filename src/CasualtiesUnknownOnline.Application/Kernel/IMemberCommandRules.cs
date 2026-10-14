namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The HOST's own rules for what a member's submission may reach — the second
/// question the admission seam asks, beside the per-kind authority declaration
/// (<see cref="WireCommandAuthorityPolicy"/>).
///
/// <para>
/// The two are deliberately different things. The authority declaration is a
/// property of the WIRE KIND: it says whether a member may author this shape of
/// report at all, and it is the same answer in every session of this build. A
/// rule here is a property of the SESSION: the host's configuration decides
/// whether an otherwise member-authorable verb is open, so the same report is
/// refused on one host and admitted on another, and the host may change its mind
/// between two reports (the entry is read per submission, never cached).
/// </para>
///
/// <para>
/// The port exists because the rule lives in the Runtime's host configuration
/// (<c>HostRulesOptions</c>) and the seam lives in the Application layer, which
/// may not reference it: the Application declares the question it needs answered
/// and the Runtime answers it from the host's own options. A build that registers
/// no implementation fails at construction rather than admitting everything —
/// the family's default is OFF, and an unanswered rule must not read as consent.
/// </para>
/// </summary>
public interface IMemberCommandRules
{
	/// <summary>
	/// Whether the host's rule opens the item-data write verbs (<c>/data set</c> and
	/// the verbs that follow it) to members. False — the default — refuses a
	/// member's console write by name at the seam, which is the only place that
	/// refusal can be made: the write reaches the host as a wire command and the
	/// member's own console cannot be the gate.
	/// </summary>
	bool AllowsItemDataWrite { get; }
}
