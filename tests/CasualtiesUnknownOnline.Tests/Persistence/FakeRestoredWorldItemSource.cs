using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the item domain's side of the world-entry gate
/// (<see cref="IRestoredWorldItemSource"/>): a restored cut's world-item set that
/// the generation reconcile has not landed yet.
///
/// The gate drives the seam's choice between "keep the restored tables" and "run
/// the layer-boundary reset", so this half must be able to hold the gate open on
/// its own: the reset's subtree rule drops every world-rooted row, which is
/// exactly the set the reconcile is about to bind.
/// </summary>
internal sealed class FakeRestoredWorldItemSource : IRestoredWorldItemSource
{
	/// <summary>Whether a restore's world items are still waiting for the generation reconcile.</summary>
	internal bool Armed { get; set; }

	public bool RestoredWorldItemsPending => Armed;
}
