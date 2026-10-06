using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The container chain's owner: an item entering or leaving a container (the
/// drag-UI LoadItem/UnloadItem and the container-broke UnloadAllItems spill,
/// Container.cs:46-66). One container move = one report; a pending drop of the
/// moved item is cancelled (the container path reports its own move), a world
/// container entering the domain on first use is registered so the peers bind
/// their local generation-time copy, and every report lands through the
/// verified commit (a move that did not actually happen is Rejected — no
/// phantom drop on the peer). A REPLAY of a peer's fact stays silent
/// (<see cref="CallContext.IsReplayedRemoteFact"/>); a peer's intent this client
/// executes on its own items does not — that mutation is this client's own fact,
/// so a remote-driven container move carries the same event a local drag does.
/// <para>
/// UnloadItem is ALSO the first half of a container-to-container move
/// (<c>PlayerCamera.cs:1589-1590</c>: <c>source.UnloadItem(child, null)</c> then
/// <c>target.LoadItem(child)</c>, one bracket), so the two hooks are read as ONE
/// operation: the unload registers the departure (where the item came from) and
/// sends nothing itself, and the load consumes it and lets
/// <see cref="ContainerLoadClassifier"/> pick the carrier — the target's. The
/// scene cannot classify the pair on its own: the unload has already detached
/// the child, which reads as "it came from the world" (batch `20261006-b`, row
/// A1g: the expansion's child was reported as a pickup of itself while the
/// TARGET container's contents changed with no event).
/// </para>
/// </summary>
internal sealed class ContainerItemSync(
	IItemControl items,
	ItemDropState dropState,
	ItemIdAllocator ids,
	OperationTrace trace,
	ItemReportCommitter reports,
	ISessionControl session,
	ILogger<ContainerItemSync> log)
{
	private readonly IItemControl _items = items;
	private readonly ItemDropState _dropState = dropState;
	private readonly ItemIdAllocator _ids = ids;
	private readonly OperationTrace _trace = trace;
	private readonly ItemReportCommitter _reports = reports;
	private readonly ISessionControl _session = session;
	private readonly ILogger<ContainerItemSync> _log = log;

	internal void OnLoadedIntoContainer(Item item, bool wasWorldItem)
	{
		if (CallContext.IsReplayedRemoteFact)
		{
			return;
		}

		// Remote clone inventory renders are display proxies: their container
		// adds/removes are presentation-only and must never enter the item
		// domain (the native remote-backpack view materialises those children
		// on the clone).
		if (item.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
		{
			return;
		}

		// The move this load completes. A departure registered in this bracket — a
		// body drop, or the container unload that opened a container-to-container
		// pair — is the fact that says where the item came from; it was re-placed,
		// not dropped, so its own report is cancelled here and this load reports the
		// move instead. `wasWorldItem` (the pre-load scene capture) answers only the
		// loads no departure opened.
		DropPendingState.Source? departure = null;
		if (_dropState.TryConsumeByContainerLoad(item, out var source, out var departureOp))
		{
			_trace.End(departureOp, OperationTrace.IdOf(item), "OnItemLoadedIntoContainer", "Cancelled", "LoadedIntoContainer");
			departure = source;
		}

		var itemId = _ids.EnsureId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		switch (ContainerLoadClassifier.Classify(ItemWorldSync.IsWorldItem(item), departure, wasWorldItem))
		{
			case ContainerLoadClassifier.Kind.Pickup:
				ReportWorldItemIntoBodyContainer(item, itemId, op);
				break;
			case ContainerLoadClassifier.Kind.CarriedContent:
				ReportCarriedContent(item, itemId, op);
				break;
			case ContainerLoadClassifier.Kind.WorldContainerDrop:
				ReportIntoWorldContainer(item, itemId, op);
				break;
		}
	}

	/// <summary>
	/// The item left the WORLD into a BODY-side container (a backpack or held
	/// container — dragging a ground item into the bag in your inventory goes
	/// through LoadItem, NOT PickUpItem, so the world-item copy would stay on the
	/// peer: "still on the ground"). World → inventory is pickup semantics — report
	/// it.
	/// </summary>
	private void ReportWorldItemIntoBodyContainer(Item item, ulong itemId, long op)
	{
		_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) left the world into a body container — pickup report.", item.id, itemId);
		_reports.CommitReport(itemId, op, "OnItemLoadedIntoContainer", CommitStatus.Committed,
			() =>
			{
				_items.SendItemPickedUp(itemId, ItemStateCodec.CaptureDigest(item));
				return 1;
			},
			"Pickup");
	}

	/// <summary>
	/// A move INSIDE the carried inventory (a backpack's contents shifted between
	/// container/slot/hand): the parent container's FULL fact is one operation = one
	/// message. The owner's body is the local fact source — a guest reports the
	/// parent, the host records and broadcasts it as the carried-fact event; a host
	/// move IS the authority and broadcasts directly. The peers' clone fact table
	/// replaces the parent entry wholesale, so the new nested contents re-render
	/// immediately (the 1 Hz character snapshot stays only as the reliable-event
	/// fallback).
	/// </summary>
	private void ReportCarriedContent(Item item, ulong itemId, long op)
	{
		var parent = item.transform.parent != null ? item.transform.parent.GetComponent<Item>() : null;
		if (parent == null) // Unity object — ==
		{
			_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternalNoParent");
			return;
		}

		// A nested container (trash bag inside a backpack) must not be
		// reported as a standalone carried root: kernel container sync
		// spawns a missing parent as Carried, which lifts the nested node
		// out of its real ancestor in the clone fact tree (visible →
		// invisible on the next snapshot). Report the top-level carried
		// root so the whole subtree keeps its contained ancestry.
		var root = FindCarriedRootItem(parent);
		if (root == null) // Unity object — ==
		{
			_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternalNoRoot");
			return;
		}

		var rootId = _ids.EnsureId(root);
		if (rootId == 0)
		{
			_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternalNoId");
			return;
		}

		var capture = ItemStateCodec.CaptureItem(root, ItemStateCodec.SlotOf(root));
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_items.SendItemCarriedSync(_session.LocalSteamId, capture);
		}
		else
		{
			_items.SendItemContainerContent(rootId, capture);
		}

		_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Committed", "ContainerContent");
		_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) moved inside body container {ContainerType} — root content event up to {RootType} (id {RootId}).",
			item.id, itemId, parent.id, root.id, rootId);
	}

	/// <summary>
	/// Into a WORLD container (a trash bag on the ground, generation-time — no
	/// instance id). The item becomes an item-domain object on first use: it gets
	/// an id here, and the item's drop message carries the container's position so
	/// the peers can bind their local (also generation-time, id-less) container by
	/// position and place the item inside it. A container that just entered the
	/// domain is REGISTERED (spawn report): the peers bind their local copy instead
	/// of materializing, and the table entry keeps the snapshot reconcile from
	/// killing the bound local container.
	/// </summary>
	private void ReportIntoWorldContainer(Item item, ulong itemId, long op)
	{
		var containerItem = item.transform.parent != null ? item.transform.parent.GetComponent<Item>() : null;
		ulong containerId = 0;
		var parentPos = new NetVector2(0f, 0f);
		var msgs = 0;
		if (containerItem != null) // Unity object — ==; the container position always travels (the receiver binds a local generation-time container by position)
		{
			parentPos = new NetVector2(containerItem.transform.position.x, containerItem.transform.position.y);
			if (ItemWorldSync.IsWorldItem(containerItem))
			{
				var containerIdComp = containerItem.GetComponent<ItemInstanceId>();
				if (containerIdComp == null) // Unity object — ==; first use of a generation-time container
				{
					containerId = _ids.EnsureId(containerItem);
					var containerPos = new NetVector2(containerItem.transform.position.x, containerItem.transform.position.y);
					_items.SendItemSpawned(containerId, ItemStateCodec.CaptureItem(containerItem, -1), containerPos,
						new NetVector2(0f, 0f), containerItem.transform.eulerAngles.z, false, 0f);
					msgs++;
				}
				else
				{
					containerId = containerIdComp.Id;
				}
			}
		}

		_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) into container {ContainerId} ({ContainerType}) at ({X:F1},{Y:F1}), parentPos ({PX:F1},{PY:F1}).",
			item.id, itemId, containerId, containerItem?.id ?? "none",
			item.transform.position.x, item.transform.position.y, parentPos.X, parentPos.Y);
		_reports.CommitReport(itemId, op, "OnItemLoadedIntoContainer", CommitStatus.Committed,
			() =>
			{
				_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
					new NetVector2(item.transform.position.x, item.transform.position.y),
					new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
					containerId, item.transform.eulerAngles.z, parentPos, item.rb.angularVelocity);
				return msgs + 1; // msgs = the container spawn above (0 or 1), +1 for the drop itself
			},
			"ContainerLoad");
	}

	internal void OnUnloadedFromContainer(Item item, bool wasWorldItem)
	{
		if (CallContext.IsReplayedRemoteFact)
		{
			return;
		}

		if (item.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
		{
			return;
		}

		if (_dropState.TryCancel(item, out var unloadedOp)) // this departure replaces that one — the item cannot leave twice, and the report below IS this item's
		{
			_trace.End(unloadedOp, OperationTrace.IdOf(item), "OnItemUnloadedFromContainer", "Cancelled", "UnloadedReported");
		}

		var itemId = _ids.EnsureId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		// Landed check: an unload that ends with the item STILL inside an
		// inventory/container (the unload was intercepted — the container path
		// reports its own moves) never left the world; reporting it would
		// materialize a phantom drop on the peer.
		if (!ItemWorldSync.IsWorldItem(item))
		{
			_trace.End(op, itemId, "OnItemUnloadedFromContainer", "Rejected", "Unload");
			return;
		}

		// The item is momentarily in the world, and the rest of its bracket decides
		// whether it STAYS there: the container-to-container move
		// (PlayerCamera.cs:1589-1590, source.UnloadItem then target.LoadItem)
		// re-homes it, and that move's carrier is the TARGET's fact. So the
		// departure waits for the frame-end flush; a load that follows consumes it
		// and reports the move instead. `wasWorldItem` is the pre-unload fact the
		// patch captured — where the item came from, which is what classifies the
		// move that consumes this departure.
		var source = wasWorldItem ? DropPendingState.Source.World : DropPendingState.Source.CarriedInventory;
		_trace.Begin(op, itemId, "OnItemUnloadedFromContainer", "Unload");
		_dropState.EnterDrop(itemId, item, item.transform.position, op, source);
		_log.LogInformation("[ContainerUnload] {Type} (id {ItemId}) left its container into the world from the {Source} side — the drop report waits one frame (a container load in this bracket re-homes it and reports the target's fact).",
			item.id, itemId, source);
	}

	internal void OnUnloadedAll(Container container)
	{
		if (CallContext.IsReplayedRemoteFact)
		{
			return;
		}

		if (container.GetComponentInParent<RemoteCloneRender>() != null) // Unity object — ==
		{
			return;
		}

		for (var i = 0; i < container.transform.childCount; i++)
		{
			var child = container.transform.GetChild(i).GetComponent<Item>();
			if (child == null) // Unity object — ==
			{
				continue;
			}

			var itemId = _ids.EnsureId(child);
			if (itemId != 0)
			{
				// Landed check per child: an unload-all that ends with the child
				// STILL parented to the container (re-parented mid-loop — the
				// container path reports its own moves) never spilled; reporting
				// it would materialize a phantom drop on the peer ("the spilled
				// item stayed in the container on the other side").
				var status = child.transform.parent != container.transform ? CommitStatus.Committed : CommitStatus.Rejected;
				var op = _trace.NextOperationId();
				_reports.CommitReport(itemId, op, "OnContainerUnloadedAll", status,
					() =>
					{
						_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(child, -1),
							new NetVector2(child.transform.position.x, child.transform.position.y),
							new NetVector2(child.rb.velocity.x, child.rb.velocity.y),
							0, child.transform.eulerAngles.z, default, child.rb.angularVelocity);
						return 1;
					},
					"Spill");
			}
			else
			{
				var op = _trace.NextOperationId();
				_trace.End(op, 0, "OnContainerUnloadedAll", "Skipped", "NoId");
			}
		}
	}

	/// <summary>
	/// Walks up the Unity hierarchy from a carried container to the outermost
	/// item whose parent is no longer an item (the body slot/limb root). This is
	/// the subtree identified by <c>SyncContainerItemsCommand</c> as Carried; a
	/// nested container must never be reported as that root on its own.
	/// </summary>
	private static Item? FindCarriedRootItem(Item item)
	{
		var current = item;
		while (current != null) // Unity object — ==
		{
			var parentTransform = current.transform.parent;
			if (parentTransform == null)
			{
				break;
			}

			var parentItem = parentTransform.GetComponent<Item>();
			if (parentItem == null)
			{
				break;
			}

			current = parentItem;
		}

		return current;
	}
}
