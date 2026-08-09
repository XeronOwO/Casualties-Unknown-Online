using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Host-side world-item position authority: the host's physics is the single
/// simulation. Its 10 Hz position stream (EVERY standalone world item,
/// sleeping included) drives the guests' kinematic copies, and the 5 s
/// keyframe refreshes the authoritative table before re-sending it. No state
/// on the guest side — the guest pump is <see cref="ItemPositionFollow"/>.
/// The host's role gate lives in the caller (GameAdapter dispatches by
/// session mode); ItemService re-checks on send.
/// </summary>
internal sealed class ItemPositionAuthority(ItemService items)
{
	private readonly ItemService _items = items;

	private const int ItemMoveIntervalMs = 100; // position stream (unreliable, 10 Hz)
	private long _nextItemMoveMs;

	/// <summary>Settled items re-align every Nth tick (1 Hz at 10 Hz tick rate) — the stream is the dominant bandwidth consumer and a settled item's payload is identical every tick, so 10 Hz re-sends the same bytes nine times out of ten.</summary>
	private const int SettledIntervalTicks = 10;
	private int _settledTick;

	private const int ItemSnapshotIntervalMs = 5000; // periodic world-item keyframe (unreliable)
	private long _nextItemSnapshotMs;

	internal void Update()
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
			_items.SendPeriodicItemSnapshot();
		}
	}

	/// <summary>
	/// Broadcast every world item's authoritative position (10 Hz, unreliable —
	/// a lost tick is overwritten by the next), moving and settled alike, but a
	/// SETTLED item (at rest below the noise floor) re-aligns at 1 Hz instead.
	/// Throttling, never filtering: the "filter sleeping items" attempt REMOVED
	/// them from the stream and a settled copy diverged again (the keyframe
	/// kept yanking it back — "bounces back every few seconds"); a settled item
	/// still rides the stream, just at 1/10 the rate — its payload is identical
	/// every tick anyway. A host-side physics nudge (earthquake, a push) makes
	/// it moving again and it is back on the full rate within a second.
	/// </summary>
	private void SendMovingItemMoves()
	{
		var entries = new List<ItemMoveEntryMsg>();
		var settledRound = ++_settledTick % SettledIntervalTicks == 0;
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || idComp.Id == 0 || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			// At rest: velocity below the noise floor AND no spin. The guest
			// copy is a kinematic render — it cannot drift by itself, so the
			// 1 Hz re-align only has to catch host-side nudges.
			var settled = item.rb.velocity.sqrMagnitude < 0.01f && Mathf.Abs(item.rb.angularVelocity) < 0.1f;
			if (settled && !settledRound)
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

	/// <summary>Push every world item's live state into the authoritative table
	/// before the periodic keyframe — the entries otherwise hold the
	/// spawn-time positions and the keyframe would yank settled items around.
	/// The host's physics is the single position authority (the guests' copies
	/// follow the position stream), so the table always mirrors it.</summary>
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
}
