using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Remote world-item application: everything a received message does to the
/// local scene — materialize (or bind a generation-time object), re-place,
/// bind containers, kill, roll back and apply corrections. The snapshot
/// reconcile lives in <see cref="ItemReconcile"/>. Owns the "applying remote"
/// guard: the local-report hooks (ItemWorldSync) read it so a remote
/// application never echoes back as a local report.
/// </summary>
internal sealed class ItemApplication
{
	private readonly IItemControl _items;
	private readonly ILogger<ItemApplication> _log;
	private readonly ItemCookReplayApplier _cookReplay;
	private readonly RemoteItemSceneOps _scene;

	internal ItemApplication(
		IItemControl items,
		ISessionControl session,
		ILogger<ItemApplication> log)
	{
		_items = items;
		_log = log;
		_scene = new RemoteItemSceneOps(session, log);
		_cookReplay = new ItemCookReplayApplier(this, session, log);
	}

	/// <summary>Pickup origin cache (id → world position) — the rollback target for a refused pickup (the pickup-start hook fills it).</summary>
	internal readonly Dictionary<ulong, Vector2> PickupOrigins = [];

	internal void BindToSession()
	{
		_items.ItemSpawned += OnRemoteItemSpawned;
		_items.ItemPickedUp += OnRemoteItemPickedUp;
		_items.ItemDropped += OnRemoteItemDropped;
		_items.ItemDestroyed += OnRemoteItemDestroyed;
		_items.ItemCookedReceived += _cookReplay.OnRemoteItemCooked;
		_items.ItemRejected += OnItemRejected;
		_items.ItemCorrectionReceived += OnItemCorrection;
	}

	internal void Unbind()
	{
		_items.ItemSpawned -= OnRemoteItemSpawned;
		_items.ItemPickedUp -= OnRemoteItemPickedUp;
		_items.ItemDropped -= OnRemoteItemDropped;
		_items.ItemDestroyed -= OnRemoteItemDestroyed;
		_items.ItemCookedReceived -= _cookReplay.OnRemoteItemCooked;
		_items.ItemRejected -= OnItemRejected;
		_items.ItemCorrectionReceived -= OnItemCorrection;
	}

	/// <summary>A world item now exists on a remote side — materialize it locally (full state: condition + components + contents).</summary>
	private void OnRemoteItemSpawned(WorldItem worldItem)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			_scene.SpawnWorldItem(worldItem);
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
				_scene.KillRemoteItem(item);
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
				_scene.SpawnWorldItem(new WorldItem(itemId, itemState, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity));
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
					_scene.BindToContainer(item, parentItemId, parentPos);
				}
			}
		}
	}

	private void OnRemoteItemDestroyed(ulong itemId)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item == null) // Unity object — ==
			{
				return;
			}

			// A destroy report is a WORLD-item fact. A carried item or a remote
			// clone display proxy must never be killed by a remote destroy —
			// that is how a viewer's proxy destroy emptied the owner's real bag.
			if (!ItemWorldSync.IsWorldItem(item)) // Unity object — ==
			{
				_log.LogDebug("[ItemDestroy] {Type} (id {ItemId}) is not a world item — remote destroy ignored.",
					item.id, itemId);
				return;
			}

			_scene.KillRemoteItem(item);
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

			RemoteItemSceneOps.ApplyAuthoritativeState(target, item);
			_log.LogInformation("[ItemCorrection] applied authoritative state to {Type} (Instance {InstanceId}).", item.ItemId, item.InstanceId);
		}
	}

	/// <summary>
	/// The host refused an item arbitration: UnknownItem — our pickup lost a
	/// race, take the item back out of the inventory to where it was picked up;
	/// BlockAlreadyBroken — our block break's drops were refused (another report
	/// of the same break won first-writer-wins), destroy the local drops: they
	/// were never picked up, there is no ground position to roll back to.
	/// </summary>
	private void OnItemRejected(ulong itemId, ItemRejectMsg.Reason reason)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var item = FindWorldItem(itemId);
			if (item == null) // Unity object — ==
			{
				return;
			}

			if (reason == ItemRejectMsg.Reason.BlockAlreadyBroken)
			{
				_scene.KillRemoteItem(item);
				_log.LogInformation("[ItemReject] block drop {ItemId} ({Type}) refused — destroying the local copy.", itemId, item.id);
			}
			else
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
	/// Scene primitives moved to <see cref="RemoteItemSceneOps"/>; these narrow
	/// delegations keep the external surface used by the reconcile/cook/position
	/// domains stable while this coordinator keeps message routing and rollback.
	/// </summary>
	internal void KillRemoteItem(Item item) => _scene.KillRemoteItem(item);

	internal void SpawnWorldItem(WorldItem w) => _scene.SpawnWorldItem(w);

	internal static Item? FindWorldItem(ulong itemId) => RemoteItemSceneOps.FindWorldItem(itemId);

	internal static Item? FindExistingAt(NetVector2 pos, string itemId) => RemoteItemSceneOps.FindExistingAt(pos, itemId);
}
