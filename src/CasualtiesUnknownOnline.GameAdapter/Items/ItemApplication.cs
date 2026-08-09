using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Remote world-item application: everything a received message does to the
/// local scene — materialize (or bind a generation-time object), re-place,
/// bind containers, kill, roll back and the snapshot reconcile. Owns the
/// "applying remote" guard: the local-report hooks (ItemWorldSync) read it so
/// a remote application never echoes back as a local report.
/// </summary>
internal sealed class ItemApplication(
	ItemService items,
	DropProtectionGuard guard,
	ILogger<ItemApplication> log)
{
	private readonly ItemService _items = items;
	private readonly DropProtectionGuard _guard = guard;
	private readonly ILogger<ItemApplication> _log = log;

	/// <summary>Pickup origin cache (id → world position) — the rollback target for a refused pickup (the pickup-start hook fills it).</summary>
	internal readonly Dictionary<ulong, Vector2> PickupOrigins = [];

	internal void BindToSession()
	{
		_items.ItemSpawned += OnRemoteItemSpawned;
		_items.ItemPickedUp += OnRemoteItemPickedUp;
		_items.ItemDropped += OnRemoteItemDropped;
		_items.ItemDestroyed += OnRemoteItemDestroyed;
		_items.ItemRejected += OnItemRejected;
		_items.ItemSnapshotReceived += OnRemoteItemSnapshot;
		_items.ItemCorrectionReceived += OnItemCorrection;
	}

	internal void Unbind()
	{
		_items.ItemSpawned -= OnRemoteItemSpawned;
		_items.ItemPickedUp -= OnRemoteItemPickedUp;
		_items.ItemDropped -= OnRemoteItemDropped;
		_items.ItemDestroyed -= OnRemoteItemDestroyed;
		_items.ItemRejected -= OnItemRejected;
		_items.ItemSnapshotReceived -= OnRemoteItemSnapshot;
		_items.ItemCorrectionReceived -= OnItemCorrection;
	}

	/// <summary>A world item now exists on a remote side — materialize it locally (full state: condition + components + contents).</summary>
	private void OnRemoteItemSpawned(WorldItem worldItem)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			SpawnWorldItem(worldItem);
		}
	}

	/// <summary>
	/// A world item left the world into someone's inventory. We never receive
	/// the broadcast of our own successful pickup (the source is excluded), so
	/// this is someone else taking it — remove our world copy (if we still
	/// have one). An item that is NOT a world item on this side (inside an
	/// inventory) is left untouched: this side owns that copy already, the
	/// peer took its own — rolling it back would yank the item out of this
	/// side's inventory ("a duplicate fell out of the hand" — the item id
	/// matched the local carried item via a leftover world phantom).
	/// A lost optimistic-pickup race rolls back through ItemReject, never here.
	/// </summary>
	private void OnRemoteItemPickedUp(ulong itemId)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item != null && ItemWorldSync.IsWorldItem(item)) // Unity objects — ==
			{
				KillRemoteItem(item);
			}
		}
	}

	private void OnRemoteItemDropped(ulong itemId, CharacterItemMsg itemState, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item == null) // Unity object — ==; we never had it (it was in the dropper's inventory)
			{
				_log.LogInformation("[ItemDrop] {Type} (id {ItemId}) not present — materializing at ({X:F1},{Y:F1}), container {ContainerId}, parentPos ({PX:F1},{PY:F1}).",
					itemState.ItemId, itemId, pos.X, pos.Y, parentItemId, parentPos.X, parentPos.Y);
				SpawnWorldItem(new WorldItem(itemId, itemState, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity));
			}
			else
			{
				_log.LogInformation("[ItemDrop] {Type} (id {ItemId}) present — re-placing at ({X:F1},{Y:F1}), container {ContainerId}.",
					itemState.ItemId, itemId, pos.X, pos.Y, parentItemId);
				item.transform.SetParent(null);
				item.transform.position = new Vector3(pos.X, pos.Y, 0f);
				item.transform.eulerAngles = new Vector3(0f, 0f, rotation);
				item.rb.velocity = new Vector2(vel.X, vel.Y); // a throw: re-applied mid-flight
				item.rb.angularVelocity = angularVelocity; // the spin at the drop moment — same initial condition
				if (parentItemId != 0)
				{
					BindToContainer(item, parentItemId, parentPos);
				}
			}
		}
	}

	private void OnRemoteItemDestroyed(ulong itemId)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item != null) // Unity object — ==
			{
				KillRemoteItem(item);
			}
		}
	}

	/// <summary>
	/// The authoritative world-item snapshot arrived (world entry): reconcile —
	/// destroy local world items missing from the snapshot, materialize the
	/// snapshot's items (world first, then container contents — the parent
	/// objects must exist).
	/// Runs inside a RemoteApply scope like every other remote application — the
	/// parity is neutral by design (KillRemoteItem zeroes ids and SpawnWorldItem
	/// attaches them before Item.Start runs, so the local-report hooks observe
	/// the same things with or without the scope), and it makes "every remote
	/// mutation carries its call identity" an invariant rather than a habit.
	/// </summary>
	private void OnRemoteItemSnapshot(IReadOnlyList<WorldItem> items)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var killed = 0;
			var spawned = 0;
			var snapshot = items.ToDictionary(w => w.ItemId);

			foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
			{
				var idComp = item.GetComponent<ItemInstanceId>();
				// STANDALONE, not just world: a container's contents (a bag's
				// carried items) have an id but NO independent table entry — the
				// entry travels INSIDE the container's Contents. With IsWorldItem
				// here the keyframe killed them as stale ("put an item in the
				// legpouch, dropped it — the host sees it inside, the guest's
				// copy is empty"), which also later fed the "equip the empty
				// pouch → the item is swallowed" chain (the host's container
				// copy with the real contents gets deleted by the pickup).
				// Inventory items are character data (IsStandaloneWorldItem is
				// false on the Body chain).
				if (idComp == null || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
				{
					continue;
				}

				if (!snapshot.ContainsKey(idComp.Id))
				{
					// Snapshot-race guard: a fresh local drop registered AFTER the
					// keyframe was generated is not in it yet — killing it would
					// loop (destroy → ItemDestroy report → the host deletes the
					// table entry → the next keyframe misses it → reconcile kills
					// it again, forever).
					if (_guard.IsProtected(idComp.Id))
					{
						continue;
					}

					KillRemoteItem(item);
					_guard.Remove(idComp.Id);
					killed++;
				}
			}

			// POSITION is aligned continuously by the 10 Hz position stream (every
			// item, sleeping included) — the reconcile does NOT place anything:
			// a 5 s direct placement after the stream already lerped the copy there
			// would be a jump, and if the copy drifted again it would be yanked
			// back every keyframe ("bounces back every few seconds"). Only the
			// missing ones are materialized here (the snapshot-race window).
			foreach (var w in items.Where(w => w.ParentItemId == 0))
			{
				if (FindWorldItem(w.ItemId) == null) // Unity object — ==
				{
					SpawnWorldItem(w);
					spawned++;
				}
			}

			foreach (var w in items.Where(w => w.ParentItemId != 0))
			{
				if (FindWorldItem(w.ItemId) == null) // Unity object — ==
				{
					SpawnWorldItem(w);
					spawned++;
				}
			}

			if (killed > 0 || spawned > 0)
			{
				_log.LogInformation("[Reconcile] {Count} items: killed {Killed}, spawned {Spawned}.",
					items.Count, killed, spawned);
			}
		}
	}

	/// <summary>
	/// The host's authoritative item state arrived (our last action-report
	/// evidence diverged): apply the top-level state (condition/liquids/
	/// components), materialize contents missing on this side (with their
	/// instance ids — a corrected content must stay findable by id) and recurse
	/// into the contents already here. Slot is deliberately NOT applied: a
	/// guest's slot layout is its local fact (the host only records it), so the
	/// correction's slot would be stale by construction. Runs inside a
	/// RemoteApply scope like every remote application — the materialization's
	/// own hooks stay silent.
	/// </summary>
	private void OnItemCorrection(CharacterItemMsg item)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var target = FindWorldItem(item.InstanceId);
			if (target == null) // Unity object — ==
			{
				_log.LogWarning("[ItemCorrection] {Type} (Instance {InstanceId}) not found locally — ignored.", item.ItemId, item.InstanceId);
				return;
			}

			ApplyAuthoritativeState(target, item);
			_log.LogInformation("[ItemCorrection] applied authoritative state to {Type} (Instance {InstanceId}).", item.ItemId, item.InstanceId);
		}
	}

	/// <summary>Recursive authoritative-state apply: the top-level fields, then per-content (recurse into an existing one, materialize a missing one with its id).</summary>
	private static void ApplyAuthoritativeState(Item target, CharacterItemMsg authoritative)
	{
		target.condition = authoritative.Condition;
		target.favourited = authoritative.Favourited;
		ItemStateCodec.RestoreLiquids(target, authoritative.Liquids);
		ItemStateCodec.RestoreComponentStates(target, authoritative.Components);

		var container = target.GetComponent<Container>();
		if (container == null || authoritative.Contents.Count == 0) // Unity object — ==
		{
			return;
		}

		var children = new Dictionary<ulong, Item>();
		for (var i = 0; i < container.transform.childCount; i++)
		{
			var child = container.transform.GetChild(i).GetComponent<Item>();
			var idComp = child != null ? child.GetComponent<ItemInstanceId>() : null; // Unity objects — ==
			if (idComp != null && idComp.Id != 0)
			{
				children[idComp.Id] = child!; // idComp non-null ⇒ child non-null
			}
		}

		foreach (var childData in authoritative.Contents)
		{
			if (childData.InstanceId != 0 && children.TryGetValue(childData.InstanceId, out var child))
			{
				ApplyAuthoritativeState(child!, childData); // found ⇒ non-null
			}
			else
			{
				ItemStateCodec.RestoreContent(target, container, childData);
			}
		}
	}

	/// <summary>The host refused our pickup — take the item back out of the inventory and put it back where it was picked up.</summary>
	private void OnItemRejected(ulong itemId)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item != null) // Unity object — ==
			{
				RollbackPickup(item, itemId);
			}
		}
	}

	/// <summary>
	/// A refused pickup or a lost race — the item leaves the inventory back
	/// into the world, at the position it was picked up from.
	/// </summary>
	private void RollbackPickup(Item item, ulong itemId)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
		if (body != null && body.HoldingItem(item)) // Unity object — ==
		{
			body.DropItem(item);
		}
		else if (item.transform.parent != null)
		{
			item.transform.SetParent(null); // mid-drag or inside a container — free it
			item.rb.simulated = true;
		}

		if (PickupOrigins.TryGetValue(itemId, out var origin))
		{
			item.transform.position = origin;
			PickupOrigins.Remove(itemId);
		}
	}

	/// <summary>
	/// Place an item inside its world container: the container's instance id
	/// (parentItemId) was allocated by the ORIGINATOR (possibly a generation-time
	/// container on first use — trash bags have no id until then), so the local
	/// container may not have it yet — bind it by the carried position, mirroring
	/// the game's LoadItem semantics (position + physics off + visibility).
	/// </summary>
	private void BindToContainer(Item item, ulong parentItemId, NetVector2 parentPos)
	{
		var parent = FindWorldItem(parentItemId);
		if (parent != null && parent.GetComponent<Container>() != null) // Unity objects — ==
		{
			_log.LogInformation("[ItemBind] container {ContainerId} found — loading {Type} into it.", parentItemId, item.id);
			parent.GetComponent<Container>()?.LoadItem(item); // the game's own load semantics (position, physics, visibility)
			return;
		}

		// Generation-time container not bound yet — find it by position and
		// stamp the originator's id onto it (idempotent: already bound to a
		// different id → not ours, keep looking). The position tolerance is
		// generous: the container may have been nudged by physics since the
		// report; a lone unbound container of the same type is accepted as a
		// fallback.
		var candidates = UnityEngine.Object.FindObjectsOfType<Container>();
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null) // Unity object — ==
			{
				continue;
			}

			if (Vector2.Distance(container.transform.position, new Vector2(parentPos.X, parentPos.Y)) > 3f)
			{
				continue;
			}

			var idComp = containerItem.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id != parentItemId) // Unity object — ==; bound to a different container
			{
				continue;
			}

			if (idComp == null) // Unity object — ==
			{
				idComp = containerItem.gameObject.AddComponent<ItemInstanceId>();
				idComp.Id = parentItemId;
			}

			_log.LogInformation("[ItemBind] container {ContainerId} bound by position ({X:F1},{Y:F1}) — loading {Type} into it.",
				parentItemId, parentPos.X, parentPos.Y, item.id);
			container.LoadItem(item);
			return;
		}

		// Fallback: a lone unbound container of the same definition anywhere —
		// the position report may be stale (the container moved after the
		// report was sent).
		foreach (var container in candidates)
		{
			var containerItem = container.GetComponent<Item>();
			if (containerItem == null || containerItem.id != item.id) // Unity object — ==
			{
				continue;
			}

			if (containerItem.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already bound
			{
				continue;
			}

			containerItem.gameObject.AddComponent<ItemInstanceId>().Id = parentItemId;
			_log.LogInformation("[ItemBind] container {ContainerId} bound as the lone {Type} (stale position {X:F1},{Y:F1}).",
				parentItemId, item.id, parentPos.X, parentPos.Y);
			container.LoadItem(item);
			return;
		}

		_log.LogWarning("[ItemBind] container {ParentItemId} for {Type} not found at ({X:F1}, {Y:F1}) — item stays where it is.",
			parentItemId, item.id, parentPos.X, parentPos.Y);
	}

	/// <summary>
	/// Remove an item object as a REMOTE application: zero its instance id
	/// immediately, then Destroy. UnityEngine.Object.Destroy is deferred to
	/// end-of-frame, so the OnDestroy hook fires AFTER the reentry guard has
	/// been restored — without zeroing the id first, every remote deletion
	/// would echo back as a local destroy report and kill the peer's own copy
	/// (observed: picking up an item destroyed it on the picker's side too).
	/// </summary>
	internal static void KillRemoteItem(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			idComp.Id = 0;
		}

		UnityEngine.Object.Destroy(item.gameObject);
	}

	/// <summary>
	/// Find an item by its instance id. Item.allItems registers in Item.Start
	/// (Item.cs:118) — ONE frame after Instantiate — so a message arriving in
	/// the same frame as a materialization misses the table (observed: a pickup
	/// relay 3 ms after the drop left the world phantom forever — "moving an
	/// item makes a duplicate drop on the peer"). Fall back to a scene scan
	/// (slow, but only on the miss path) to cover the not-yet-registered window.
	/// </summary>
	internal static Item? FindWorldItem(ulong itemId)
	{
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		foreach (var item in UnityEngine.Object.FindObjectsOfType<Item>())
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp != null && idComp.Id == itemId) // Unity object — ==
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>
	/// Find a generation-time (id-less) item of the same definition near Pos —
	/// the materialization bind target. Only items outside any inventory count
	/// (world-gen determinism put them there on every side).
	/// </summary>
	internal static Item? FindExistingAt(NetVector2 pos, string itemId)
	{
		var target = new Vector2(pos.X, pos.Y);
		foreach (var item in Item.allItems)
		{
			if (item.id != itemId || !ItemWorldSync.IsWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already an item-domain object
			{
				continue;
			}

			if (Vector2.Distance(item.transform.position, target) > 1.5f)
			{
				continue;
			}

			return item;
		}

		return null;
	}

	/// <summary>
	/// Materialize a world item from its carried state: instantiate the
	/// definition prefab, restore condition/components/liquids/contents, attach
	/// the instance id and place it (into its container when the parent exists).
	/// The Item.Start hook sees the already-attached id and does not re-report.
	/// </summary>
	internal void SpawnWorldItem(WorldItem w)
	{
		// A generation-time object may already exist at this spot (world-gen
		// determinism puts the same objects on every side): bind the instance
		// id to it instead of materializing a duplicate — a second copy would
		// also be killed by the next snapshot reconcile (one table entry, two
		// scene objects) and a generation-time container that was already
		// bound must NOT be re-materialized either ("items overlapping").
		var existing = FindExistingAt(w.Pos, w.Item.ItemId);
		if (existing != null) // Unity object — ==
		{
			var existingId = existing.GetComponent<ItemInstanceId>();
			if (existingId == null || existingId.Id == w.ItemId) // Unity object — ==; ours or still unbound
			{
				if (existingId == null) // Unity object — ==
				{
					existingId = existing.gameObject.AddComponent<ItemInstanceId>();
					existingId.Id = w.ItemId;
					existing.condition = w.Item.Condition; // match the originator's carried state
					ItemStateCodec.RestoreLiquids(existing, w.Item.Liquids);
					ItemStateCodec.RestoreComponentStates(existing, w.Item.Components);
					if (w.FreshItemDrop)
					{
						existing.gameObject.AddComponent<FreshItemDrop>();
					}

					// Align the bound object to the reported state — the
					// generation-time object sits where the world-gen put it,
					// which may differ from the originator's current spot
					// ("item in the wrong place / overlapping" class of bugs).
					existing.transform.position = new Vector3(w.Pos.X, w.Pos.Y, 0f);
					existing.transform.eulerAngles = new Vector3(0f, 0f, w.Rotation);
					existing.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);
					existing.rb.angularVelocity = w.AngularVelocity;

					_log.LogInformation("[ItemBind] bound existing {Type} at ({X:F1}, {Y:F1}) to id {ItemId} (no materialization).",
						w.Item.ItemId, w.Pos.X, w.Pos.Y, w.ItemId);
				}

				if (w.ParentItemId != 0)
				{
					BindToContainer(existing, w.ParentItemId, w.ParentPosition);
				}

				return;
			}
		}

		_log.LogInformation("[ItemSpawn] materializing {Type} (id {ItemId}) at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}), container {ContainerId}.",
			w.Item.ItemId, w.ItemId, w.Pos.X, w.Pos.Y, w.Vel.X, w.Vel.Y, w.ParentItemId);
		var prefab = Resources.Load(w.Item.ItemId);
		if (prefab == null) // Unity object — ==
		{
			_log.LogWarning("Cannot materialize item {ItemId}: definition '{Type}' not found.", w.ItemId, w.Item.ItemId);
			return;
		}

		var obj = UnityEngine.Object.Instantiate(prefab, new Vector3(w.Pos.X, w.Pos.Y, 0f), Quaternion.Euler(0f, 0f, w.Rotation)) as GameObject;
		var item = obj!.GetComponent<Item>(); // the definition prefab carries Item — Instantiate succeeded, so it exists
		item.condition = w.Item.Condition; // direct write, like the save restore (SaveSystem.cs:306) — SetCondition would drain water by ratio
		item.favourited = w.Item.Favourited;
		item.gameObject.AddComponent<ItemInstanceId>().Id = w.ItemId;
		ItemStateCodec.RestoreLiquids(item, w.Item.Liquids);
		ItemStateCodec.RestoreComponentStates(item, w.Item.Components);
		ItemStateCodec.RestoreContents(item, w.Item.Contents);
		if (w.FreshItemDrop)
		{
			item.gameObject.AddComponent<FreshItemDrop>(); // the glowing floating pickup effect (self-destroys when the setting is off)
		}

		if (w.ParentItemId != 0)
		{
			BindToContainer(item, w.ParentItemId, w.ParentPosition);
		}

		item.rb.velocity = new Vector2(w.Vel.X, w.Vel.Y);
		item.rb.angularVelocity = w.AngularVelocity;
	}
}
