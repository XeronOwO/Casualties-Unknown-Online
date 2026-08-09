using CasualtiesUnknownOnline.Runtime.Protocol;
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
/// report → host relay/arbitration (the Runtime's ItemService owns the
/// authoritative table; this class shuttles between the game objects and it).
/// The host position authority and the guest follow live in
/// <see cref="ItemPositionAuthority"/> / <see cref="ItemPositionFollow"/>, the
/// materialization side in <see cref="ItemApplication"/>.
/// </summary>
internal sealed class ItemWorldSync(
	SessionService session,
	ItemService items,
	ItemApplication application,
	DropProtectionGuard guard,
	OperationTrace trace,
	ILogger<ItemWorldSync> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ItemApplication _application = application;
	private readonly DropProtectionGuard _guard = guard;
	private readonly OperationTrace _trace = trace;
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

	/// <summary>Instance-id counter: ids are (counter, account id) — globally unique per session without host allocation.</summary>
	private ulong _nextItemId;

	private ulong NextItemId() => (_nextItemId++ << 32) | (uint)_session.LocalSteamId;

	/// <summary>
	/// Return the item's instance id, allocating one when it does not have it
	/// yet — a generation-time item (world-gen determinism covers it, no id)
	/// that enters the world domain through a runtime act (dropped from an
	/// inventory, unloaded from a container) needs an id so the peers can
	/// materialize it. Returns 0 when the item is not eligible (still
	/// generating).
	/// </summary>
	internal ulong EnsureItemId(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			return idComp.Id;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			return 0; // generation-time instantiation — the world-gen determinism covers it
		}

		idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = NextItemId();
		return idComp.Id;
	}

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
	/// An item that is already inside an inventory/container when Start runs is
	/// NOT a world item: the game's own flow instantiates and picks up in the
	/// same frame (the starting supplies, WorldGeneration.cs:1904-1912; use
	/// transforms like the empty bottle, Item.cs:1442) and MonoBehaviour.Start
	/// only fires on the NEXT frame — after generation finished, so the
	/// IsGenerating guard alone would misclassify them as runtime spawns and
	/// duplicate them for the peers.
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

		idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = NextItemId();
		var itemId = idComp.Id;
		// The glowing floating pickup effect carries over. Drops are executed
		// on the ATTACKER's side (local compute), so the game's 8 ft proximity
		// check (BuildingEntity.cs:74) already ran against the attacker's own
		// distance — the component on the object is the truth.
		var fresh = item.GetComponent<FreshItemDrop>() != null; // Unity object — ==
		_log.LogInformation("[ItemSpawned] local {Type} (id {ItemId}) reported at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), fresh {Fresh}.",
			item.id, itemId, item.transform.position.x, item.transform.position.y,
			item.rb.velocity.x, item.rb.velocity.y, fresh);
		_items.SendItemSpawned(itemId, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(item.transform.position.x, item.transform.position.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			item.transform.eulerAngles.z,
			fresh, item.rb.angularVelocity);
		_trace.End(op, itemId, "OnItemInstantiated", "Reported", "Instantiated");
	}

	internal void OnItemDestroyed(Item item)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// A pending drop of a DESTROYED item is cancelled — without this the
		// pending state lingered until the next drop overwrote it (the flush's
		// Unity == check caught it, but the op trace then showed a permanent
		// begin-without-end and the state could never resolve on its own).
		if (_dropState.TryCancel(item, out var cancelledOp))
		{
			_trace.End(cancelledOp, OperationTrace.IdOf(item), "OnItemDestroyed", "Cancelled", "Destroyed");
		}

		var op = _trace.NextOperationId();
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==; remote deletions zero the id (see KillRemoteItem)
		{
			_items.SendItemDestroyed(idComp.Id);
			_trace.End(op, idComp.Id, "OnItemDestroyed", "Reported", "Destroyed");
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

	internal void OnItemPickupStart(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && _application.PickupOrigins.Count < 256) // Unity object — ==; bounded, oldest overwritten
		{
			_application.PickupOrigins[idComp.Id] = item.transform.position;
		}
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
			// (without this, the entries linger as ghosts — the next keyframe
			// materializes them again as standalone items, "the bag swallowed
			// its contents / the dog food came back as a separate item").
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
			msgs++;
			_trace.End(op, idComp.Id, "OnItemPickedUp", $"Reported({msgs})", "Pickup");
		}
		else
		{
			_trace.End(op, 0, "OnItemPickedUp", "Skipped", "NoId");
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
			SendDropReport(itemId, item, thrown.Pos);
			_trace.End(thrown.Op, itemId, "OnItemThrown", "Reported", "Drop", "Throw");
			return;
		}

		// No matching pending drop — the pump already flushed it on a previous
		// frame (a cross-frame DropItem → ThrowItem, rare) or a hook-order
		// anomaly. Report alone anyway, or the item never enters the domain;
		// the host's re-place covers the rare double report.
		var op = _trace.NextOperationId();
		SendDropReport(itemId, item, (Vector2)item.transform.position);
		_trace.End(op, itemId, "OnItemThrown", "Reported", "Throw");
	}

	/// <summary>Next frame: a drop that was not thrown (a plain drop, velocity ~0)
	/// reports now. The one-frame wait is not a timing hack — the game performs
	/// one drop as a DropItem → ThrowItem sequence within the player's input
	/// frame, and the report must carry the FINAL velocity (a zero-velocity
	/// report made the host materialize a ghost whose wrong trajectory yanked
	/// the dropper's copy back — "dropped — immediately desynced"). An item
	/// that meanwhile left the world (loaded into a container — that path
	/// reported it) does not re-report.</summary>
	internal void FlushPendingDrop()
	{
		if (!_dropState.TryFlush(out var flushed))
		{
			return; // no pending drop, or still waiting — the throw velocity may still land this frame, or the item is destroyed / still attached to the body (drag-to-hand re-pick). A pending drop that stays pending (destroyed, never freed, world left) shows up as a begin-without-end in the item trace — the baseline asserts on that leak.
		}

		SendDropReport(EnsureItemId(flushed.Item), flushed.Item, flushed.Pos);
		_trace.End(flushed.Op, OperationTrace.IdOf(flushed.Item), "FlushPendingDrop", "Reported", "Drop", "Flush");
	}

	private void SendDropReport(ulong itemId, Item item, Vector2 pos)
	{
		// Diagnostic: how many contents rode along (a dropped bag must carry
		// its contents — "the bag is empty after dropping" class of bugs).
		var container = item.GetComponent<Container>();
		_log.LogInformation("[ItemDropped] {Type} (id {ItemId}) at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}) — container contents {Contents}.",
			item.id, itemId, pos.x, pos.y, item.rb.velocity.x, item.rb.velocity.y,
			container != null ? container.transform.childCount : 0); // Unity object — ==
		_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(pos.x, pos.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			0, item.transform.eulerAngles.z, default, item.rb.angularVelocity);
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
				_items.SendItemPickedUp(itemId);
				_trace.End(op, itemId, "OnItemLoadedIntoContainer", "Reported", "Pickup");
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
		_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(item.transform.position.x, item.transform.position.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			containerId, item.transform.eulerAngles.z, parentPos, item.rb.angularVelocity);
		msgs++;
		_trace.End(op, itemId, "OnItemLoadedIntoContainer", $"Reported({msgs})", "ContainerLoad");
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
			var op = _trace.NextOperationId();
			_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z, default, item.rb.angularVelocity);
			_trace.End(op, itemId, "OnItemUnloadedFromContainer", "Reported", "Unload");
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
				var op = _trace.NextOperationId();
				_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(child, -1),
					new NetVector2(child.transform.position.x, child.transform.position.y),
					new NetVector2(child.rb.velocity.x, child.rb.velocity.y),
					0, child.transform.eulerAngles.z, default, child.rb.angularVelocity);
				_trace.End(op, itemId, "OnContainerUnloadedAll", "Reported", "Spill");
			}
			else
			{
				var op = _trace.NextOperationId();
				_trace.End(op, 0, "OnContainerUnloadedAll", "Skipped", "NoId");
			}
		}
	}

}
