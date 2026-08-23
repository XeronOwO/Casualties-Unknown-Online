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
/// sleeping included) feeds the guests' local simulations — they simulate the
/// same trajectory and the stream soft-corrects (velocity sync + snap past a
/// threshold, see <see cref="ItemPositionFollow"/>). The 5 s keyframe
/// refreshes the authoritative table before re-sending it. The host's role
/// gate lives in the caller (GameAdapter dispatches by session mode);
/// IItemControl re-checks on send. The settled throttle (which items ride the
/// 1 Hz round — <see cref="SettledStreamThrottle"/>) is pure; this class is
/// the scene-read shell.
/// </summary>
internal sealed class ItemPositionAuthority(IItemControl items)
{
	private readonly IItemControl _items = items;

	private const int ItemMoveIntervalMs = 100; // position stream (unreliable, 10 Hz)
	private long _nextItemMoveMs;

	private const int ItemSnapshotIntervalMs = 5000; // periodic world-item keyframe (unreliable)
	private long _nextItemSnapshotMs;

	private readonly SettledStreamThrottle _throttle = new();

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
		_throttle.BeginPump();
		foreach (var item in Item.allItems)
		{
			var idComp = item.GetComponent<ItemInstanceId>();
			if (idComp == null || idComp.Id == 0 || !ItemWorldSync.IsStandaloneWorldItem(item)) // Unity object — ==
			{
				continue;
			}

			// At rest: velocity below the noise floor AND no spin. The guest
			// copy simulates locally and stops by itself, so the 1 Hz re-align
			// only has to close the residual gap (and catch host-side nudges).
			// The motion→rest edge forces one immediate tick (the throttle's
			// first send for a settled id).
			var settled = ItemMotionState.IsSettled(item.rb.velocity.sqrMagnitude, Mathf.Abs(item.rb.angularVelocity));
			if (!_throttle.ShouldSend(idComp.Id, settled))
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
				item.transform.eulerAngles.z,
				item.condition); // decay advances on the host too — the keyframe carries the CURRENT condition or the peers re-align to a stale one
		}
	}
}
