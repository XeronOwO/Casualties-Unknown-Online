using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// World-item domain: runtime-generated item entities (drops, creature loot,
/// use-spawned items) as synchronized game objects — instance ids, spawn/drop/
/// pickup/container reports, the settle position authority, the periodic
/// keyframe, the world-entry snapshot reconcile and the pickup rollback.
/// Local compute → report → host relay/arbitration (the Runtime's ItemService
/// owns the authoritative table; this class shuttles between the game objects
/// and it).
/// </summary>
internal sealed class ItemWorldSync(
	SessionService session,
	ItemService items,
	ItemApplication application,
	ILogger<ItemWorldSync> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ItemApplication _application = application;
	private readonly ILogger<ItemWorldSync> _log = log;

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
			if (t.GetComponent<InventorySlot>() != null || t.GetComponent<Body>() != null)
			{
				return false;
			}

			t = t.parent;
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
		if (_application.IsApplyingRemote || HarmonyTraverse.IsGenerating() || !IsWorldItem(item))
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==; remote application attached it first — already synced
		{
			return;
		}

		idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = NextItemId();
		// The glowing floating pickup effect carries over. Drops are executed
		// on the ATTACKER's side (local compute), so the game's 8 ft proximity
		// check (BuildingEntity.cs:74) already ran against the attacker's own
		// distance — the component on the object is the truth.
		var fresh = item.GetComponent<FreshItemDrop>() != null; // Unity object — ==
		_log.LogInformation("[ItemSpawned] local {Type} (id {ItemId}) reported at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), fresh {Fresh}.",
			item.id, idComp.Id, item.transform.position.x, item.transform.position.y,
			item.rb.velocity.x, item.rb.velocity.y, fresh);
		_items.SendItemSpawned(idComp.Id, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(item.transform.position.x, item.transform.position.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			item.transform.eulerAngles.z,
			fresh);
	}

	internal void OnItemDestroyed(Item item)
	{
		if (_application.IsApplyingRemote || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==; remote deletions zero the id (see KillRemoteItem)
		{
			_items.SendItemDestroyed(idComp.Id);
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
		if (_application.IsApplyingRemote)
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			_items.SendItemPickedUp(idComp.Id);
		}
	}

	internal void OnItemDropped(Item item)
	{
		if (_application.IsApplyingRemote)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	internal void OnItemThrown(Item item)
	{
		if (_application.IsApplyingRemote)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			// The throw velocity is set AFTER the drop report (Body.cs:1659-1661)
			// — this second report carries it; the peer's copy (already
			// materialized by the first) gets re-placed with the flight
			// velocity instead of dropping in place.
			_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	internal void OnItemLoadedIntoContainer(Item item, bool wasWorldItem)
	{
		if (_application.IsApplyingRemote)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId == 0)
		{
			return;
		}

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
						new NetVector2(0f, 0f), containerItem.transform.eulerAngles.z, false);
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
			containerId, item.transform.eulerAngles.z, parentPos);
	}

	internal void OnItemUnloadedFromContainer(Item item)
	{
		if (_application.IsApplyingRemote)
		{
			return;
		}

		var itemId = EnsureItemId(item);
		if (itemId != 0)
		{
			_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				0, item.transform.eulerAngles.z);
		}
	}

	internal void OnContainerUnloadedAll(Container container)
	{
		if (_application.IsApplyingRemote)
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
				_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(child, -1),
					new NetVector2(child.transform.position.x, child.transform.position.y),
					new NetVector2(child.rb.velocity.x, child.rb.velocity.y),
					0, child.transform.eulerAngles.z);
			}
		}
	}

}
