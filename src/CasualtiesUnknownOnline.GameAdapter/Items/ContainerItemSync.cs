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
/// phantom drop on the peer).
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

	/// <summary>True while a remote message is being applied — local reports must stay silent (call identity lives in CallContext).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	internal void OnLoadedIntoContainer(Item item, bool wasWorldItem)
	{
		if (IsRemoteApply)
		{
			return;
		}

		// The item entered a container — a pending drop of it is cancelled (it
		// was re-placed, not dropped; the container path reports its own move).
		if (_dropState.TryCancel(item, out var loadedOp))
		{
			_trace.End(loadedOp, OperationTrace.IdOf(item), "OnItemLoadedIntoContainer", "Cancelled", "LoadedIntoContainer");
		}

		var itemId = _ids.EnsureId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		if (!ItemWorldSync.IsWorldItem(item))
		{
			// The item left the world into a BODY-side container (a backpack or
			// held container — dragging a ground item into the bag in your
			// inventory goes through LoadItem, NOT PickUpItem, so the world-item
			// copy would stay on the peer: "still on the ground"). World →
			// inventory is pickup semantics — report it.
			if (wasWorldItem)
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
			else
			{
				// A move INSIDE the carried inventory (a backpack's contents
				// shifted between container/slot/hand): the parent container's
				// FULL fact is one operation = one message. The owner's body is
				// the local fact source — a guest reports the parent, the host
				// records and broadcasts it as the carried-fact event; a host
				// move IS the authority and broadcasts directly. The peers'
				// clone fact table replaces the parent entry wholesale, so the
				// new nested contents re-render immediately (the 1 Hz character
				// snapshot stays only as the reliable-event fallback).
				var parent = item.transform.parent != null ? item.transform.parent.GetComponent<Item>() : null;
				if (parent == null) // Unity object — ==
				{
					_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternalNoParent");
					return;
				}

				var parentId = _ids.EnsureId(parent);
				if (parentId == 0)
				{
					_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternalNoId");
					return;
				}

				var capture = ItemStateCodec.CaptureItem(parent, ItemStateCodec.SlotOf(parent));
				if (_session.Role == SessionRole.Host && _session.SessionActive)
				{
					_items.SendItemCarriedSync(_session.LocalSteamId, capture);
				}
				else
				{
					_items.SendItemContainerContent(parentId, capture);
				}

				_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Committed", "ContainerContent");
				_log.LogInformation("[ContainerLoad] {Type} (id {ItemId}) moved inside body container {ContainerType} (id {ContainerId}) — nested content event.", item.id, itemId, parent.id, parentId);
			}

			return;
		}

		// A WORLD container (a trash bag on the ground, generation-time — no
		// instance id) becomes an item-domain object on first use: it gets an
		// id here, and the item's drop message carries the container's position
		// so the peers can bind their local (also generation-time, id-less)
		// container by position and place the item inside it. A container that
		// just entered the domain is REGISTERED (spawn report): the peers bind
		// their local copy instead of materializing, and the table entry keeps
		// the snapshot reconcile from killing the bound local container.
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

	internal void OnUnloadedFromContainer(Item item)
	{
		if (IsRemoteApply)
		{
			return;
		}

		if (_dropState.TryCancel(item, out var unloadedOp)) // the unload report below IS this item's report — a later flush must not send it again
		{
			_trace.End(unloadedOp, OperationTrace.IdOf(item), "OnItemUnloadedFromContainer", "Cancelled", "UnloadedReported");
		}

		var itemId = _ids.EnsureId(item);
		if (itemId != 0)
		{
			// Landed check: an unload that ends with the item STILL inside an
			// inventory/container (the unload was intercepted — the container
			// path reports its own moves) never left the world; reporting it
			// would materialize a phantom drop on the peer.
			var status = ItemWorldSync.IsWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
			var op = _trace.NextOperationId();
			_reports.CommitReport(itemId, op, "OnItemUnloadedFromContainer", status,
				() =>
				{
					_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
						new NetVector2(item.transform.position.x, item.transform.position.y),
						new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
						0, item.transform.eulerAngles.z, default, item.rb.angularVelocity);
					return 1;
				},
				"Unload");
		}
	}

	internal void OnUnloadedAll(Container container)
	{
		if (IsRemoteApply)
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
}
