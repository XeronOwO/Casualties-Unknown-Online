using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// World-item reconciliation: the authoritative snapshot (periodic keyframe /
/// world entry) aligned against the local scene — kill the stale, materialize
/// the missing, re-align drifted condition. Split out of ItemApplication
/// (gate-driven — the application class kept growing with every remote
/// message shape). The materialization primitives live in
/// <see cref="ItemApplication"/>; this class owns the reconcile-only logic.
/// </summary>
internal sealed class ItemReconcile(
	ItemService items,
	ItemApplication itemApplication,
	DropProtectionGuard guard,
	ILogger<ItemReconcile> log)
{
	private readonly ItemService _items = items;
	private readonly ItemApplication _app = itemApplication;
	private readonly DropProtectionGuard _guard = guard;
	private readonly ILogger<ItemReconcile> _log = log;

	internal void BindToSession() => _items.ItemSnapshotReceived += OnRemoteItemSnapshot;

	internal void Unbind() => _items.ItemSnapshotReceived -= OnRemoteItemSnapshot;

	/// <summary>
	/// The authoritative world-item snapshot arrived (world entry): reconcile —
	/// destroy local world items missing from the snapshot, materialize the
	/// snapshot's items (world first, then container contents — the parent
	/// objects must exist).
	/// Runs inside a RemoteApply scope like every remote application — the
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

					ItemApplication.KillRemoteItem(item);
					_guard.Remove(idComp.Id);
					killed++;
				}
			}

			// State alignment: decay (Item.HandleDecay) runs per side on
			// Time.deltaTime — with the generation-time guard the sides start
			// decaying together, but edge conditions (an item wet on one side at
			// a liquid-block boundary, a Geiger counter toggled on one side) can
			// still drift the rate. The keyframe re-aligns the condition (the
			// host refreshed the table's condition right before sending).
			// Battery charge decays INTO the condition (BatteryItem.DrainCharge,
			// BatteryItem.cs:136) — a placed device's power drain is covered by
			// this same alignment. POSITION stays owned by the position stream —
			// never placed here.
			var aligned = 0;
			foreach (var item in Item.allItems)
			{
				var idComp = item.GetComponent<ItemInstanceId>();
				if (idComp == null || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
				{
					continue;
				}

				if (!snapshot.TryGetValue(idComp.Id, out var w))
				{
					continue;
				}

				if (Mathf.Abs(item.condition - w.Item.Condition) > 0.0005f)
				{
					item.condition = w.Item.Condition;
					aligned++;
				}
			}

			if (aligned > 0)
			{
				_log.LogInformation("[Reconcile] aligned condition of {Aligned} items.", aligned);
			}

			// POSITION is aligned continuously by the 10 Hz position stream (every
			// item, sleeping included) — the reconcile does NOT place anything:
			// a 5 s direct placement after the stream already lerped the copy there
			// would be a jump, and if the copy drifted again it would be yanked
			// back every keyframe ("bounces back every few seconds"). Only the
			// missing ones are materialized here (the snapshot-race window).
			foreach (var w in items.Where(w => w.ParentItemId == 0))
			{
				if (ItemApplication.FindWorldItem(w.ItemId) == null) // Unity object — ==
				{
					_app.SpawnWorldItem(w);
					spawned++;
				}
			}

			foreach (var w in items.Where(w => w.ParentItemId != 0))
			{
				if (ItemApplication.FindWorldItem(w.ItemId) == null) // Unity object — ==
				{
					_app.SpawnWorldItem(w);
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
}
