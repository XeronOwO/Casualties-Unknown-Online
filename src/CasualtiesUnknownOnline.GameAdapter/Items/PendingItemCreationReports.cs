using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The Game Adapter's deferred creation reports, exposed as ONE
/// <see cref="IPendingItemCreationSource"/>. Three report owners legitimately
/// hold a creation back for a frame so its payload is complete — a drop waits
/// for its throw velocity, a destructive trap's building-death drops fold into
/// the same event, a block break's drops fold into the break + drops message —
/// and the item domain settles all three before it reports an operation on any
/// item. That order IS the invariant: the host judges the creation first, so it
/// never has to hold, wait for, or guess about the operation.
///
/// <para>
/// Settling is safe to call at any moment: each owner flushes only when its own
/// frame rule allows it, and clears its pending state before it emits the
/// report, so a settle that runs inside another settle finds nothing left.
/// </para>
/// </summary>
internal sealed class PendingItemCreationReports(
	ItemWorldSync drops,
	EntityEventSync entityEvents,
	BlockBreakSync breaks) : IPendingItemCreationSource
{
	public void SettlePendingCreations()
	{
		drops.FlushPendingDrop();
		entityEvents.FlushPendingDrops();
		breaks.FlushPendingBlockBreak();
	}
}
