using CasualtiesUnknownOnline.Runtime.Protocol;
using CommitStatus = CasualtiesUnknownOnline.GameAdapter.Items.ItemReportCommitter.CommitStatus;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// World-item report side: every way an item enters or leaves the world domain
/// (drops, throws, container loads/unloads, pickups, destruction) funnels into
/// one report here — instance-id allocation, the merged one-report-per-drop
/// initial vectors and the snapshot-race protection marking. Local compute →
/// report → host relay/arbitration; the drop state machine and report commit
/// live in <see cref="ItemDropState"/> / <see cref="ItemReportCommitter"/>, the
/// materialization side in <see cref="ItemApplication"/>.
/// </summary>
internal sealed class ItemWorldSync(
	SessionService session,
	ItemService items,
	ItemApplication application,
	DropProtectionGuard guard,
	OperationTrace trace,
	ItemReportCommitter reports,
	ItemIdAllocator ids,
	ILogger<ItemWorldSync> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ItemApplication _application = application;
	private readonly DropProtectionGuard _guard = guard;
	private readonly OperationTrace _trace = trace;
	private readonly ItemReportCommitter _reports = reports;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<ItemWorldSync> _log = log;

	/// <summary>True while a remote message is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not a bool).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	internal void BindToSession()
	{
		_items.ItemSpawned += OnRemoteItemBecameWorld;
		_items.ItemDropped += OnRemoteItemBecameWorld;
	}

	internal void Unbind()
	{
		_items.ItemSpawned -= OnRemoteItemBecameWorld;
		_items.ItemDropped -= OnRemoteItemBecameWorld;
	}

	/// <summary>An item was materialized from a remote message (spawn/drop broadcast) — same snapshot-race protection as a local drop.</summary>
	private void OnRemoteItemBecameWorld(WorldItem item) => _guard.Mark(item.ItemId);

	private void OnRemoteItemBecameWorld(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos) => _guard.Mark(itemId);

	/// <summary>Instance-id allocation (ids are (counter, account id) — see ItemIdAllocator).</summary>
	internal ulong EnsureItemId(Item item) => _ids.EnsureId(item);

	/// <summary>True when the item's parent chain ends outside any inventory/body — it is part of the world.</summary>
	internal static bool IsWorldItem(Item item)
	{
		var t = item.transform;
		while (t != null)
		{
			// == null on Unity objects (a scene-reload-destroyed parent is not managed-null)
			// Limb: worn items are parented to the limb (WearWearable, Body.cs:1508)
			// — they are character state, not world items.
			if (t.GetComponent<InventorySlot>() != null || t.GetComponent<Body>() != null || t.GetComponent<Limb>() != null)
			{
				return false;
			}

			t = t.parent;
		}

		return true;
	}

	/// <summary>
	/// True for a world item that is NOT inside another container — the
	/// standalone-item collectors (spawn reports, position stream, keyframe)
	/// must never treat container contents as independent items: a world bag's
	/// contents travel WITH the bag (Contents travel inside the bag's carried
	/// state), and a standalone duplicate would materialize on the peer and sit
	/// stuck in the same spot ("a bag with dog food dropped — two items stuck
	/// together"). The Container check rides the PARENT chain, starting above
	/// the item itself — a bag IS a container, its own Container component must
	/// not exclude it from the position stream, or the host never streams it
	/// and the peer's copy free-simulates on its own physics ("dropping a bag
	/// from the mouth — immediately desynced").
	/// </summary>
	internal static bool IsStandaloneWorldItem(Item item)
	{
		if (!IsWorldItem(item))
		{
			return false;
		}

		for (var t = item.transform.parent; t != null; t = t.parent)
		{
			if (t.GetComponent<Container>() != null) // Unity object — ==
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Called from the Item.Start patch after a runtime-generated item appeared
	/// (drops, creature loot, use-spawned items — every instantiation lands
	/// here). Generation-time items are skipped (world-gen determinism covers
	/// them); everything else gets an instance id and is reported. Solo play
	/// records too (no broadcast) — a solo-turned-lobby host hands its
	/// accumulated items to a joining guest via the snapshot.
	/// An item already inside an inventory/container when Start runs is NOT a
	/// world item: the game instantiates and picks up in the same frame (the
	/// starting supplies, WorldGeneration.cs:1904-1912) and MonoBehaviour.Start
	/// fires on the NEXT frame — after generation, so the IsGenerating guard
	/// alone would misclassify them as runtime spawns and duplicate them.
	/// </summary>
	internal void OnItemInstantiated(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating() || !IsStandaloneWorldItem(item))
		{
			return;
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==; remote application attached it first — already synced
		{
			_trace.End(op, OperationTrace.IdOf(item), "OnItemInstantiated", "Skipped", "AlreadySynced");
			return;
		}

		var itemId = _ids.Allocate(item);
		// The glowing floating pickup effect carries over (the proximity check
		// already ran on the attacker's side — the component is the truth).
		var fresh = item.GetComponent<FreshItemDrop>() != null; // Unity object — ==
		_log.LogInformation("[ItemSpawned] local {Type} (id {ItemId}) reported at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), fresh {Fresh}.",
			item.id, itemId, item.transform.position.x, item.transform.position.y,
			item.rb.velocity.x, item.rb.velocity.y, fresh);
		_reports.CommitReport(itemId, op, "OnItemInstantiated", CommitStatus.Committed,
			() =>
			{
				_items.SendItemSpawned(itemId, ItemStateCodec.CaptureItem(item, -1),
					new NetVector2(item.transform.position.x, item.transform.position.y),
					new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
					item.transform.eulerAngles.z,
					fresh, item.rb.angularVelocity);
				return 1;
			},
			"Instantiated");
	}

	internal void OnItemDestroyed(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// A pending drop of a DESTROYED item is cancelled — it used to linger
		// until the next drop overwrote it (a permanent begin-without-end).
		if (_dropState.TryCancel(item, out var cancelledOp))
		{
			_trace.End(cancelledOp, OperationTrace.IdOf(item), "OnItemDestroyed", "Cancelled", "Destroyed");
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==; remote deletions zero the id (see KillRemoteItem)
		{
			_reports.CommitReport(idComp.Id, op, "OnItemDestroyed", CommitStatus.Committed,
				() =>
				{
					_items.SendItemDestroyed(idComp.Id);
					return 1;
				},
				"Destroyed");
		}
		else
		{
			_trace.End(op, OperationTrace.IdOf(item), "OnItemDestroyed", "Skipped", "NoId");
		}
	}

	/// <summary>The world was left (scene switch / session end) — a pending drop cannot resolve anymore; cancel it so the operation trace stays balanced.</summary>
	internal void ResetPending()
	{
		if (_dropState.TryReset(out var op))
		{
			_trace.End(op, 0, "ResetPending", "Cancelled", "WorldLeft");
		}
	}

	/// <summary>The pickup-start position of the last PickUpItem call — explicit state passed between the pickup-start and picked-up hooks (CLAUDE.md #9): still on the ground HERE, the picked-up hook runs after the re-parent. Id-less generation-time items have no PickupOrigins key — this covers them.</summary>
	private (Item Item, Vector2 Pos)? _lastPickupStart;

	internal void OnItemPickupStart(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && _application.PickupOrigins.Count < 256) // Unity object — ==; bounded, oldest overwritten
		{
			_application.PickupOrigins[idComp.Id] = item.transform.position;
		}

		_lastPickupStart = (item, item.transform.position); // the ground position, before the pickup re-parents the item
	}

	internal void OnItemPickedUp(Item item)
	{
		if (IsRemoteApply)
		{
			return;
		}

		// The item re-entered an inventory — a pending drop of it is cancelled
		// and NOT reported: the same-frame drag sequence (PlayerCamera.cs:1623
		// DropItem + 1629 PickUpItem — a body-internal reorder) dropped the
		// item from a slot and re-picked it in one player input. The drop got
		// an instance id (EnsureItemId) but the item never entered the world
		// table, so reporting the pickup made the host refuse the unknown id
		// and roll the item back out of the inventory ("dragged from a slot to
		// the hand — immediately dropped, forever unpickable").
		if (_dropState.TryCancel(item, out var pickedOp))
		{
			_trace.End(pickedOp, OperationTrace.IdOf(item), "OnItemPickedUp", "Cancelled", "RePick");
			return;
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			// A picked-up CONTAINER carries its contents out of the world: each
			// content item that had an instance id leaves the world table too
			// (without this, the entries linger as ghosts — "the bag swallowed
			// its contents / the dog food came back as a separate item"). The
			// landed check is Indeterminate (report still goes out), not
			// Rejected: a pickup ending with the item STILL a world item is
			// suspicious, but PickUpItem's landing variants are not fully
			// enumerated — observe first, tighten later.
			var status = IsWorldItem(item) ? CommitStatus.Indeterminate : CommitStatus.Committed;
			_reports.CommitReport(idComp.Id, op, "OnItemPickedUp", status,
				() =>
				{
					var msgs = 0;
					var container = item.GetComponent<Container>();
					if (container != null)
					{
						for (var i = 0; i < container.transform.childCount; i++)
						{
							var child = container.transform.GetChild(i).GetComponent<Item>();
							var childId = child != null ? child.GetComponent<ItemInstanceId>() : null; // Unity objects — ==
							if (childId != null && childId.Id != 0)
							{
								_items.SendItemPickedUp(childId.Id);
								msgs++;
							}
						}
					}

					_items.SendItemPickedUp(idComp.Id);
					return msgs + 1;
				},
				"Pickup");
		}
		else
		{
			// A GENERATION-TIME item (no id — world-gen determinism covers it)
			// left the world through a pickup/wear: the peer's scene holds the
			// same generation-time object, and without an id it can neither bind
			// nor delete it — "the worn item still lies on the ground on the
			// peer's side". Allocate the id and report spawn-then-pickup: the
			// spawn binds the peer's same-spot object (SpawnWorldItem →
			// FindExistingAt), the pickup removes it (OnRemoteItemPickedUp).
			var id = EnsureItemId(item);
			if (id != 0)
			{
				var startPos = _lastPickupStart is { } start && start.Item == item // Unity object — ==
					? start.Pos
					: (Vector2)item.transform.position;
				_reports.CommitReport(id, op, "OnItemPickedUp", CommitStatus.Committed,
					() =>
					{
						_items.SendItemSpawned(id, ItemStateCodec.CaptureItem(item, -1),
							new NetVector2(startPos.x, startPos.y), new NetVector2(0f, 0f),
							item.transform.eulerAngles.z, false, 0f);
						_items.SendItemPickedUp(id);
						return 2;
					},
					"Pickup", "GenerationItem");
			}
			else
			{
				_trace.End(op, 0, "OnItemPickedUp", "Skipped", "NoId");
			}
		}
	}

	/// <summary>
	/// A drop is buffered into ONE report carrying the COMPLETE initial
	/// vectors. The game performs one drop as two calls — DropItem (the item
	/// leaves the slot, velocity still 0) and, for throws, ThrowItem (sets the
	/// flight velocity, Body.cs:1659-1661) — and DropWearable fires its own
	/// hook too. Reporting each call separately made the host materialize a
	/// zero-velocity ghost whose wrong trajectory entered the position stream
	/// and yanked the dropper's own copy back ("dropped — immediately
	/// desynced", "bounces back"). So the report waits: ThrowItem consumes it
	/// immediately with the final velocity, otherwise the pump flushes it at
	/// end of frame (a plain drop, velocity 0). One drop operation = one
	/// report = one materialization from complete initial conditions.
	/// </summary>
	/// <summary>The drop-operation state machine — all pending read/write points live in this one owner (see ItemDropState).</summary>
	private readonly ItemDropState _dropState = new();

	internal void OnItemDropped(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId); // the roll-out is local physics until the host's stream takes over — the reconcile must not kill the fresh copy
		}

		if (_dropState.Current == ItemDropState.Phase.Dropped && !_dropState.IsPendingFor(item)) // two drops in one frame (rare) — flush the first first
		{
			FlushPendingDrop();
		}

		_trace.Begin(op, itemId, "OnItemDropped", "Drop");
		_dropState.EnterDrop(item, (Vector2)item.transform.position, op); // the throw velocity lands a moment later (ThrowItem) — merge into one report
	}

	internal void OnItemThrown(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		if (_session.Role == SessionRole.Guest)
		{
			_guard.Mark(itemId);
		}

		if (_dropState.TryConsumeByThrow(item, out var thrown))
		{
			// Landed check: a throw with the item NOT a standalone world item
			// (re-attached mid-flight — an inventory-internal sequence) never
			// left the world; reporting it would materialize a phantom drop.
			var status = IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
			_reports.CommitReport(itemId, thrown.Op, "OnItemThrown", status,
				() =>
				{
					_reports.SendDropReport(itemId, item, thrown.Pos);
					return 1;
				},
				"Drop", "Throw");
			return;
		}

		// No matching pending drop — the pump already flushed it on a previous
		// frame (a cross-frame DropItem → ThrowItem, rare) or a hook-order
		// anomaly. Report alone anyway, or the item never enters the domain;
		// the host's re-place covers the rare double report.
		var op = _trace.NextOperationId();
		var throwStatus = IsStandaloneWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
		_reports.CommitReport(itemId, op, "OnItemThrown", throwStatus,
			() =>
			{
				_reports.SendDropReport(itemId, item, (Vector2)item.transform.position);
				return 1;
			},
			"Throw");
	}

	/// <summary>Next frame: a drop that was not thrown (a plain drop, velocity ~0)
	/// reports now — the one-frame wait lets the game's DropItem → ThrowItem
	/// sequence set the FINAL velocity (a zero-velocity report materialized a
	/// ghost on the host, "dropped — immediately desynced"). An item that
	/// meanwhile left the world does not re-report.</summary>
	internal void FlushPendingDrop()
	{
		if (!_dropState.TryFlush(out var flushed))
		{
			return; // no pending drop, or still waiting — the throw velocity may still land this frame, or the item is destroyed / still attached to the body (drag-to-hand re-pick). A pending drop that stays pending (destroyed, never freed, world left) shows up as a begin-without-end in the item trace — the baseline asserts on that leak.
		}

		// TryFlush already verified the item is a standalone world item — the
		// commit is Committed by construction.
		var itemId = EnsureItemId(flushed.Item);
		_reports.CommitReport(itemId, flushed.Op, "FlushPendingDrop", CommitStatus.Committed,
			() =>
			{
				_reports.SendDropReport(itemId, flushed.Item, flushed.Pos);
				return 1;
			},
			"Drop", "Flush");
	}

	internal void OnItemLoadedIntoContainer(Item item, bool wasWorldItem)
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

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

		var op = _trace.NextOperationId();

		if (!IsWorldItem(item))
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
						_items.SendItemPickedUp(itemId);
						return 1;
					},
					"Pickup");
			}
			else
			{
				_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Skipped", "BodyInternal");
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
			if (IsWorldItem(containerItem))
			{
				var containerIdComp = containerItem.GetComponent<ItemInstanceId>();
				if (containerIdComp == null) // Unity object — ==; first use of a generation-time container
				{
					containerId = EnsureItemId(containerItem);
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

	internal void OnItemUnloadedFromContainer(Item item)
	{
		if (IsRemoteApply)
		{
			return;
		}

		if (_dropState.TryCancel(item, out var unloadedOp)) // the unload report below IS this item's report — a later flush must not send it again
		{
			_trace.End(unloadedOp, OperationTrace.IdOf(item), "OnItemUnloadedFromContainer", "Cancelled", "UnloadedReported");
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			// Landed check: an unload that ends with the item STILL inside an
			// inventory/container (the unload was intercepted — the container
			// path reports its own moves) never left the world; reporting it
			// would materialize a phantom drop on the peer.
			var status = IsWorldItem(item) ? CommitStatus.Committed : CommitStatus.Rejected;
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

	internal void OnContainerUnloadedAll(Container container)
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

			var itemId = EnsureItemId(child);
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
