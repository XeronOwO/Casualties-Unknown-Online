namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// One owner of DEFERRED creation reports on the reporting side. A creation
/// fact can legitimately wait a frame before it is reported (a block break
/// holds its report so its drops' <c>Item.Start</c> folds in, a drop waits for
/// its throw velocity, a trap's building-death drops wait for the event), but
/// an OPERATION on that item may never overtake its creation: the host judges
/// an operation only after it has judged the creation, so the sender has to
/// report the creation first.
///
/// <para>
/// Implemented by the Game Adapter (the only layer that owns the deferred
/// report states) and registered with the item domain at composition. The
/// ordering rule itself lives in <see cref="PendingItemCreations"/>.
/// </para>
/// </summary>
public interface IPendingItemCreationSource
{
	/// <summary>
	/// Report every creation fact this source still holds back, in the order the
	/// reports must reach the host. Called before an operation report goes out,
	/// so a settled creation always precedes it on the same ordered channel.
	/// Re-entrancy is safe: a source clears its pending state before it emits the
	/// report, so a nested settle finds nothing left to do.
	/// </summary>
	void SettlePendingCreations();
}
