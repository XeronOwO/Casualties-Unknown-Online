using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// World-item position authority: who owns an item's position and how the
/// others follow it. Generator-side settle reports (guest), the host's
/// periodic keyframe (refreshed live state + sleeping-phantom alignment),
/// the settle-arrival alignment and the world-entry snapshot reconcile.
/// Pure position/rotation state — the reports and materialization live in
/// <see cref="ItemWorldSync"/>.
/// </summary>
internal sealed class ItemPositionSync(
	SessionService session,
	ItemService items,
	ItemApplication itemApplication,
	ILogger<ItemPositionSync> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ItemApplication _itemApplication = itemApplication;
	private readonly ILogger<ItemPositionSync> _log = log;

	private const int ItemMoveIntervalMs = 100; // moving-item position stream (unreliable, 10 Hz)
	private long _nextItemMoveMs;

	private const int ItemSnapshotIntervalMs = 5000; // periodic world-item keyframe (unreliable)
	private long _nextItemSnapshotMs;

	/// <summary>Guest: id → the host's authoritative move target (position/velocity/rotation) — interpolated toward every frame.</summary>
	private readonly Dictionary<ulong, (Vector2 Pos, Vector2 Vel, float Rot, float AngVel)> _followTargets = [];

	/// <summary>Guest: items that just became world items here — either dropped locally (their local physics is authoritative for the roll-out; the host materializes only on the drop report, one network delay late) or materialized from a remote message. The guard serves two purposes: (1) the position stream must not yank a just-dropped item back to the drop point ("bounces back, then rolls again"); (2) the reconcile must not kill a fresh item whose keyframe was generated before it registered (a stale keyframe misses it → kill → ItemDestroy report → the host deletes the table entry → the next keyframe misses it → killed again — "totally desynced, an item disappears").</summary>
	private readonly Dictionary<ulong, long> _localDropProtectUntil = [];

	private const int LocalDropProtectMs = 400;

	/// <summary>Guest: an item was dropped/thrown locally — protect it from the host's position stream for the roll-out.</summary>
	internal void MarkLocalDrop(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null && idComp.Id != 0) // Unity object — ==
		{
			_localDropProtectUntil[idComp.Id] = Environment.TickCount + LocalDropProtectMs;
		}
	}

	internal void BindToSession()
	{
		_items.ItemSettledReceived += OnRemoteItemSettled;
		_items.ItemSnapshotReceived += OnRemoteItemSnapshot;
		_items.ItemMoveReceived += OnRemoteItemMove;
		_items.ItemSpawned += OnRemoteItemBecameWorld;
		_items.ItemDropped += OnRemoteItemBecameWorld;
	}

	internal void Unbind()
	{
		_items.ItemSettledReceived -= OnRemoteItemSettled;
		_items.ItemSnapshotReceived -= OnRemoteItemSnapshot;
		_items.ItemMoveReceived -= OnRemoteItemMove;
		_items.ItemSpawned -= OnRemoteItemBecameWorld;
		_items.ItemDropped -= OnRemoteItemBecameWorld;
	}

	/// <summary>Guest: an item was materialized from a remote message (spawn/drop broadcast) — same snapshot-race protection as a local drop.</summary>
	private void OnRemoteItemBecameWorld(WorldItem item) => _localDropProtectUntil[item.ItemId] = Environment.TickCount + LocalDropProtectMs;

	private void OnRemoteItemBecameWorld(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos) => _localDropProtectUntil[itemId] = Environment.TickCount + LocalDropProtectMs;

	/// <summary>
	/// Pump: the host's position authority — moving items at 10 Hz (unreliable
	/// position stream), the full-table keyframe at 5 s. Guest settle reports
	/// are retired: drops are host-rolled (host-authoritative), so the host
	/// generates every item-domain object and its physics is the single truth.
	/// </summary>
	internal void Update()
	{
		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			if (Environment.TickCount >= _nextItemMoveMs)
			{
				_nextItemMoveMs = Environment.TickCount + ItemMoveIntervalMs;
				SendMovingItemMoves();
			}

			if (Environment.TickCount >= _nextItemSnapshotMs)
			{
				_nextItemSnapshotMs = Environment.TickCount + ItemSnapshotIntervalMs;
				RefreshWorldItemStates();
				AlignGuestCopies(); // the phantoms first, so the keyframe broadcasts the truth
				_items.SendPeriodicItemSnapshot();
			}

			return;
		}

		FollowHostMoves();
	}

	/// <summary>
	/// Host only: broadcast EVERY world item's authoritative position (10 Hz,
	/// unreliable — a lost tick is overwritten by the next), sleeping items
	/// included. Every world item is host-authoritative: the host's physics is
	/// the single simulation, the guests follow. Filtering sleeping items out
	/// made a settled item's position diverge again (the guest's local physics
	/// kept settling it elsewhere, and the 5 s reconcile kept yanking it back —
	/// "bounces back every few seconds"); streaming everything keeps the
	/// settled spot aligned continuously and the reconcile only handles the
	/// snapshot races.
	/// </summary>
	private void SendMovingItemMoves()
	{
		var entries = new List<ItemMoveEntryMsg>();
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || idComp.Id == 0 || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			var pos = item.transform.position;
			var vel = item.rb.velocity;
			entries.Add(new ItemMoveEntryMsg
			{
				ItemId = idComp.Id,
				X = pos.x,
				Y = pos.y,
				VelX = vel.x,
				VelY = vel.y,
				Rotation = item.transform.eulerAngles.z,
				AngularVelocity = item.rb.angularVelocity,
			});
		}

		_items.SendItemMove(entries);
	}

	/// <summary>
	/// Guest side: the host's physics moved items — store the authoritative
	/// targets; FollowHostMoves interpolates toward them every frame. Direct
	/// placement per stream tick (10 Hz) made items that occupy the same spot
	/// (a dropped bag and its contents) visibly snap and jitter — "twitching
	/// in place"; pursuit keeps the follow smooth.
	/// </summary>
	private void OnRemoteItemMove(IReadOnlyList<ItemMoveEntryMsg> items)
	{
		foreach (var e in items)
		{
			_followTargets[e.ItemId] = (new Vector2(e.X, e.Y), new Vector2(e.VelX, e.VelY), e.Rotation, e.AngularVelocity);
		}
	}

	/// <summary>
	/// Guest side: pursue the host's authoritative move targets (velocity as a
	/// bias, position eased toward the target). Stale targets (the host stopped
	/// streaming an item — settled, picked up) drop out on the item's absence.
	/// </summary>
	private void FollowHostMoves()
	{
		if (_followTargets.Count == 0)
		{
			return;
		}

		foreach (var key in _followTargets.Keys.ToList()) // copy — removed while iterating
		{
			var item = ItemApplication.FindWorldItem(key);
			// Unity object — ==. Gone (picked up/destroyed), not yet materialized,
			// or no longer a WORLD item (picked into an inventory/hand — the item
			// object persists in Item.allItems, so FindWorldItem still finds it;
			// without this check the stale target keeps yanking the carried item
			// toward the host's last world position — "everything desynced"
			// after picking things up).
			if (item == null || !ItemWorldSync.IsStandaloneWorldItem(item))
			{
				_followTargets.Remove(key);
				_localDropProtectUntil.Remove(key);
				continue;
			}

			var (pos, vel, rot, angVel) = _followTargets[key];
			// The guest copy is a RENDER of the host's simulation — it must not
			// simulate on its own (local gravity/collisions fight the host's
			// stream forever: "dropped — immediately desynced"). Kinematic
			// bodies take no physics input (no push/pull/gap accumulation), yet
			// their colliders still register in pickup queries, so the player
			// can still grab them.
			if (item.rb.bodyType != RigidbodyType2D.Kinematic)
			{
				item.rb.bodyType = RigidbodyType2D.Kinematic;
			}

			item.transform.position = Vector3.Lerp(item.transform.position, new Vector3(pos.x, pos.y, 0f), Mathf.Clamp01(Time.deltaTime * 12f));
			item.transform.eulerAngles = new Vector3(0f, 0f, rot);
			item.rb.velocity = vel;
			item.rb.angularVelocity = angVel;
		}
	}

	/// <summary>
	/// Host side: a guest's item settled — align the local phantom to the
	/// generator's position (zero velocity + sleep so the local physics does
	/// not push it back). The table entry was already updated by ItemService.
	/// </summary>
	private void OnRemoteItemSettled(ulong itemId, NetVector2 pos, float rotation)
	{
		var item = ItemApplication.FindWorldItem(itemId);
		if (item != null && item.rb != null) // Unity objects — ==
		{
			item.transform.position = new Vector3(pos.X, pos.Y, 0f);
			item.transform.eulerAngles = new Vector3(0f, 0f, rotation);
			item.rb.velocity = Vector2.zero;
			item.rb.angularVelocity = 0f;
			item.rb.Sleep();
		}
	}

	/// <summary>Host only: push every world item's live state into the
	/// authoritative table before the periodic keyframe — the entries otherwise
	/// hold the spawn-time positions and the keyframe would yank settled items
	/// around. The host's physics is the single position authority (the guests'
	/// copies follow the position stream), so the table always mirrors it; the
	/// keyframe reconciles the guests to it.</summary>
	private void RefreshWorldItemStates()
	{
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || idComp.Id == 0 || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			_items.RefreshItemState(idComp.Id,
				new NetVector2(item.transform.position.x, item.transform.position.y),
				new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
				item.transform.eulerAngles.z);
		}
	}

	/// <summary>
	/// Host only, before the keyframe: align SLEEPING phantoms of guest-generated
	/// items to the table (their settle reports) — the phantom's own physics
	/// diverges from the generator's (different start tick) and nothing else
	/// ever pulls it back ("the item is at the wrong spot").
	/// </summary>
	private void AlignGuestCopies()
	{
		var me = (uint)_session.LocalSteamId;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) == me || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			if (!item.rb.IsSleeping() || item.rb.velocity.magnitude > 0.1f)
			{
				continue; // moving — leave it alone
			}

			if (_items.TryGetItemPosition(idComp.Id, out var pos)
				&& Vector2.Distance(item.transform.position, new Vector2(pos.X, pos.Y)) > 0.5f)
			{
				item.transform.position = new Vector3(pos.X, pos.Y, 0f);
				item.rb.velocity = Vector2.zero;
				item.rb.angularVelocity = 0f;
				item.rb.Sleep();
				_log.LogInformation("[ItemBind] aligned guest phantom {ItemId} to the table ({X:F1},{Y:F1}).",
					idComp.Id, pos.X, pos.Y);
			}
		}
	}

	/// <summary>
	/// The authoritative world-item snapshot arrived (world entry): reconcile —
	/// destroy local world items missing from the snapshot, materialize the
	/// snapshot's items (world first, then container contents — the parent
	/// objects must exist).
	/// </summary>
	private void OnRemoteItemSnapshot(IReadOnlyList<WorldItem> items)
	{
		var killed = 0;
		var spawned = 0;
		var aligned = 0;
		var snapshot = items.ToDictionary(w => w.ItemId);

		foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || !ItemWorldSync.IsWorldItem(item)) // Unity object — ==; inventory items are character data
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
				if (_localDropProtectUntil.TryGetValue(idComp.Id, out var until) && until > Environment.TickCount)
				{
					continue;
				}

				ItemApplication.KillRemoteItem(item);
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
			var item = ItemApplication.FindWorldItem(w.ItemId);
			if (item == null) // Unity object — ==
			{
				_itemApplication.SpawnWorldItem(w);
				spawned++;
			}
		}

		foreach (var w in items.Where(w => w.ParentItemId != 0))
		{
			if (ItemApplication.FindWorldItem(w.ItemId) == null) // Unity object — ==
			{
				_itemApplication.SpawnWorldItem(w);
				spawned++;
			}
		}

		if (killed > 0 || spawned > 0 || aligned > 0)
		{
			_log.LogInformation("[Reconcile] {Count} items: killed {Killed}, spawned {Spawned}, aligned {Aligned}.",
				items.Count, killed, spawned, aligned);
		}
	}
}
