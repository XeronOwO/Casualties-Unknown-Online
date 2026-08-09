using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

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
	/// a lost tick is overwritten by the next), sleeping items included.
	/// Filtering sleeping items out made a settled item's position diverge
	/// again (the guest copy settled it elsewhere and the keyframe kept yanking
	/// it back — "bounces back every few seconds"); streaming everything keeps
	/// the settled spot aligned continuously and the reconcile only handles the
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
