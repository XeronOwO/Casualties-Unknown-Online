namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host/solo: the world-item set a restored cut put into the kernel and the live
/// world has not taken yet.
///
/// Three readers need this one set, and they must never disagree:
/// <list type="bullet">
/// <item>the generation reconcile asks whether it must reconcile its objects
/// against the restored set instead of publishing them under fresh ids
/// (<see cref="RestoredWorldItemsPending"/> holds the full rule),</item>
/// <item>the world-entry seam's gate asks whether ANY restored half is still
/// owed — a restore whose ONLY pending half is this one must keep the restored
/// tables through the generation, because the layer-boundary reset would drop the
/// world-rooted rows the reconcile is about to bind (and the loss would be silent:
/// the reset's subtree rule removes exactly those rows, so the reconcile would
/// then have nothing left to materialize), and</item>
/// <item>the save layer drops the expectation when the restore will never be
/// reconciled (<see cref="CancelRestoredWorldItems"/>). Those two facts are the
/// whole port: it is what keeps the save layer off the handler surface behind it,
/// and what lets a suite hand it a recording stand-in.</item>
/// </list>
/// </summary>
public interface IRestoredWorldItemSource
{
	/// <summary>
	/// Host/solo: a restore put the archive's world items into the kernel and the
	/// layer is about to be regenerated. While this is true the generation must
	/// RECONCILE its objects against the restored set (bind the regenerated object
	/// to the restored id, materialize what generation did not create, drop the
	/// generation's leftovers the cut never described) instead of publishing them
	/// under fresh ids — publishing beside the restored set is what left two item
	/// families at one physical spot and resurrected a ground copy next to the
	/// restored one. The world-entry seam reads the same flag to keep the restored
	/// tables through the generation while this half is still owed.
	/// </summary>
	bool RestoredWorldItemsPending { get; }

	/// <summary>
	/// Host/solo: the restored item set will never be reconciled — a layer-end cut
	/// (its world rows are dropped by the layer reset, not restored), a new run, a
	/// supersession, or the session ending. Drops the expectation; an ARMED set
	/// reports the loss to the restore's account instead of disappearing silently,
	/// and the arm's own restore sequence stamps that contribution, so a release of a
	/// half the open account does not owe is not counted.
	/// </summary>
	void CancelRestoredWorldItems(string reason);
}
