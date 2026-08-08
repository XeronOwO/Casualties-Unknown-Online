using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
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

	/// <summary>Guest: id → last reported settle position (re-armed when the body wakes).</summary>
	private readonly Dictionary<ulong, Vector2> _settledReported = [];
	private long _nextSettleCheckMs;

	private const int ItemSnapshotIntervalMs = 5000; // periodic world-item keyframe (unreliable)
	private long _nextItemSnapshotMs;

	internal void BindToSession()
	{
		_items.ItemSettledReceived += OnRemoteItemSettled;
		_items.ItemSnapshotReceived += OnRemoteItemSnapshot;
	}

	internal void Unbind()
	{
		_items.ItemSettledReceived -= OnRemoteItemSettled;
		_items.ItemSnapshotReceived -= OnRemoteItemSnapshot;
	}

	/// <summary>Pump: periodic keyframe (host) + generator-side settle reports (guest).</summary>
	internal void Update()
	{
		// Periodic world-item keyframes (host): re-send the full table
		// (unreliable) so physical drift self-heals — settled items get their
		// positions re-aligned by the receivers' next reconcile. The table
		// entries are refreshed to the CURRENT item positions first — the
		// spawn-time positions would pull settled items back into the air
		// every tick. Guest-generated items keep the guest's settled reports
		// (RefreshWorldItemStates only refreshes our own — generator authority).
		if (_session.Role == SessionRole.Host && _session.SessionActive && Environment.TickCount >= _nextItemSnapshotMs)
		{
			_nextItemSnapshotMs = Environment.TickCount + ItemSnapshotIntervalMs;
			RefreshWorldItemStates();
			AlignGuestCopies(); // the phantoms first, so the keyframe broadcasts the truth
			_items.SendPeriodicItemSnapshot();
		}

		// Generator-side position authority (guest): items this side generated
		// report their settled position once, so the table and the host's
		// phantom align to the generator's physics.
		UpdateItemSettleReports();
	}

	/// <summary>
	/// Guest only: items this side generated (instance-id low bits = local
	/// SteamId, see NextItemId) report their SETTLED position once — the
	/// authoritative table (and the host's phantom) align to the generator's
	/// physics instead of the receiver-side drift ("item fell through the
	/// world" / "pulled back to the host's spot" class of bugs). "Settled" is
	/// the rigidbody's own sleep state — a velocity threshold re-armed on
	/// every roll-and-stop cycle and re-sent the same item dozens of times,
	/// yanking the host's phantom around (observed in the settle log spam).
	/// Sleeping is a stable terminal state: report exactly once, re-arm when
	/// the body wakes (kicked/picked up).
	/// </summary>
	private void UpdateItemSettleReports()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var nowMs = Environment.TickCount;
		if (nowMs < _nextSettleCheckMs)
		{
			return;
		}

		_nextSettleCheckMs = nowMs + 500;
		var me = (uint)_session.LocalSteamId;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) != me || !ItemWorldSync.IsWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			var speed = item.rb.velocity.magnitude;
			if (!item.rb.IsSleeping() || speed > 0.1f)
			{
				_settledReported.Remove(idComp.Id); // awake or moving — it may settle later
				continue;
			}

			// Re-report when a SLEEPING item drifted from its last reported
			// spot: sleeping bodies still creep under micro-collisions (a
			// player walking past pushes without waking them), the table went
			// stale, and the keyframe pulled the item back to the OLD spot —
			// physics shoved it out again, forever ('item keeps bouncing',
			// observed: reconcile aligned 2-6 items every keyframe for
			// minutes). The table now always follows the item's current spot,
			// so the keyframe's pull lands exactly where the item already is.
			var pos = item.transform.position;
			if (_settledReported.TryGetValue(idComp.Id, out var last) && Vector2.Distance(last, pos) < 0.5f)
			{
				continue; // still at the reported spot — nothing new
			}

			_settledReported[idComp.Id] = pos;
			_items.SendItemSettle(idComp.Id,
				new NetVector2(pos.x, pos.y),
				item.transform.eulerAngles.z);
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

	/// <summary>Host only: push the items' live state into the authoritative
	/// table before the periodic keyframe — the entries otherwise hold the
	/// spawn-time positions and the keyframe would yank settled items around.
	/// Only items the host itself generated are refreshed from the local
	/// physics: the guest's items are position-authoritative on the guest side
	/// (the instance id's low 32 bits are the generator's SteamId — see
	/// NextItemId), and their table entries keep the guest's settled reports
	/// (ItemSettle) instead of the host-side phantom's drift.</summary>
	private void RefreshWorldItemStates()
	{
		var me = (uint)_session.LocalSteamId;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) != me) // Unity object — ==; only our own items
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
			if (idComp == null || (uint)(idComp.Id & 0xFFFFFFFF) == me || !ItemWorldSync.IsWorldItem(item)) // Unity object — ==
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
				ItemApplication.KillRemoteItem(item);
				killed++;
			}
		}

		foreach (var w in items.Where(w => w.ParentItemId == 0))
		{
			var item = ItemApplication.FindWorldItem(w.ItemId);
			if (item == null) // Unity object — ==
			{
				_itemApplication.SpawnWorldItem(w);
				spawned++;
			}
			else if (item.transform.parent == null && item.rb.IsSleeping()
				&& (Vector2.Distance(item.transform.position, new Vector2(w.Pos.X, w.Pos.Y)) > 0.5f
					|| Mathf.Abs(Mathf.DeltaAngle(item.transform.eulerAngles.z, w.Rotation)) > 2f))
			{
				// A SLEEPING item drifted from the authoritative state —
				// re-align both and put the body to sleep (the re-position
				// would otherwise be "corrected" back by the local physics,
				// restarting the yank-roll loop). Only sleeping bodies are
				// aligned: creeping items (0.05-0.1 velocity, awake) were
				// pulled every keyframe and visibly stepped/jumped together
				// ("items suddenly overlapping / clumping").
				item.transform.position = new Vector3(w.Pos.X, w.Pos.Y, 0f);
				item.transform.eulerAngles = new Vector3(0f, 0f, w.Rotation);
				item.rb.velocity = Vector2.zero;
				item.rb.angularVelocity = 0f;
				item.rb.Sleep();
				aligned++;
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
