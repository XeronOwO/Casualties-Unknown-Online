using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The guest's half of the creation-before-operation invariant: an operation
/// report naming an item may only leave after every creation report that item
/// depends on has left, so the host never has to wait on, or guess about, an
/// operation on an item whose creation it has not judged yet. The rule is
/// enforced where the reports are emitted (the item domain's operation sends)
/// and the deferred creation owners are supplied by the Game Adapter
/// (<see cref="IPendingItemCreationSource"/>), because only the adapter knows
/// what it is still holding back.
///
/// <para>
/// This is ORDERING, never a latency window: nothing here waits, expires or
/// measures time. A settle either reports the creation right now or leaves it
/// to the frame-end flush that was already due — and a creation still deferred
/// at that point cannot be named by the operation, because the item has no
/// instance id on this side until the very frame whose end carries its report.
/// </para>
///
/// <para>
/// Host side: the host never reports an operation to itself over the wire, so
/// the rule is guest-only. A guest that skips it is a protocol violation the
/// host answers by refusing the operation at once (see
/// <c>KernelProtocolCommandHandler</c>) instead of holding it.
/// </para>
/// </summary>
internal sealed class PendingItemCreations(ISessionControl session)
{
	private readonly ISessionControl _session = session;
	private readonly List<IPendingItemCreationSource> _sources = [];

	/// <summary>Register one owner of deferred creation reports (composition-time; the Game Adapter registers itself).</summary>
	internal void Register(IPendingItemCreationSource source) => _sources.Add(source);

	/// <summary>
	/// Settle every deferred creation report before an operation report goes
	/// out. No-op outside a live guest session: only a guest reports its local
	/// compute for a judging host, and the host's own creations are its own
	/// authority and never travel as reports.
	/// </summary>
	internal void SettleBeforeOperation()
	{
		if (_sources.Count == 0 || _session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		for (var i = 0; i < _sources.Count; i++)
		{
			_sources[i].SettlePendingCreations();
		}
	}
}
