namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Which carrier ONE container load's report takes (ticket
/// <c>backlog/todo/container-move-snapshot-only-sync.md</c>, row A1g).
/// <para>
/// The Game Adapter's load hook sees the item AFTER the move, and the game's
/// container-to-container move detaches the item on its way
/// (<c>Container.UnloadItem</c> then <c>Container.LoadItem</c>,
/// <c>PlayerCamera.cs:1589-1590</c>), so the scene at that instant answers "it
/// came from the world" for a child that never left the carried inventory — and
/// the pair reached the peers as a pickup of the child while the TARGET
/// container's contents changed with no event. The fact that answers it is the
/// DEPARTURE the pair opened (<see cref="DropPendingState.Source"/>): where the
/// item stood before the unload. The scene capture from the load's own prefix
/// stays the fallback for a load that no departure opened — an item dragged off
/// the ground, or a container's first fill.
/// </para>
/// <para>
/// The TARGET decides the carrier family: an item that lands in a world
/// container travels as the bound drop report whatever it came from, and only a
/// body-side target splits into "the item left the world" (pickup) and "the item
/// moved inside the carried inventory" (the target root's contents fact). The
/// three kinds are the three carriers <see cref="IItemControl"/> already has —
/// this rule only decides which one a load uses. Pure — no Unity type, no scene
/// read — so its truth table is a unit test.
/// </para>
/// </summary>
internal static class ContainerLoadClassifier
{
	/// <summary>The carrier a container load's report takes.</summary>
	internal enum Kind
	{
		/// <summary>World → body-side container: the item left the world, so the peers drop their world copy and the owner's clone takes it in (<see cref="IItemControl.SendItemPickedUp"/>).</summary>
		Pickup,

		/// <summary>A move INSIDE the carried inventory: the target's carried ROOT is the one message — its full contents fact carries the child, and the peers prune the child from wherever the fact tree held it (<see cref="IItemControl.SendItemCarriedSync"/> on a host, <see cref="IItemControl.SendItemContainerContent"/> on a guest).</summary>
		CarriedContent,

		/// <summary>The item entered a WORLD container: the bound drop report places it inside that container's record and registers the container on first use (<see cref="IItemControl.SendItemDropped"/>).</summary>
		WorldContainerDrop,
	}

	/// <summary>
	/// <paramref name="landsInWorldContainer"/>: the item's new home is a world container — the scene answers it after the load (the item's parent chain ends outside any body).
	/// <paramref name="departure"/>: where the departure that opened this move came from, or null when no departure was registered in this bracket.
	/// <paramref name="wasWorldItem"/>: the pre-load scene capture, the fallback fact for a load no departure opened.
	/// </summary>
	internal static Kind Classify(bool landsInWorldContainer, DropPendingState.Source? departure, bool wasWorldItem)
	{
		if (landsInWorldContainer)
		{
			return Kind.WorldContainerDrop;
		}

		var cameFromTheWorld = departure is { } source ? source == DropPendingState.Source.World : wasWorldItem;
		return cameFromTheWorld ? Kind.Pickup : Kind.CarriedContent;
	}
}
