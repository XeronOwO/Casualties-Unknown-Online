using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
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
/// the scene-read shell. The send rates now come from the shared adaptive
/// rate service so network pressure can lower the 10 Hz movement stream and
/// lengthen the 5 s keyframe fallback.
/// </summary>
internal sealed class ItemPositionAuthority(
	IItemControl items,
	ISessionControl session,
	AdaptiveStreamRateService adaptiveRates)
{
	private readonly IItemControl _items = items;
	private readonly ISessionControl _session = session;
	private readonly AdaptiveStreamRateService _adaptiveRates = adaptiveRates;

	private long _nextItemMoveMs;
	private long _nextItemSnapshotMs;

	private readonly SettledStreamThrottle _throttle = new();

	internal void Update()
	{
		var now = Environment.TickCount;
		if (now >= _nextItemMoveMs)
		{
			_nextItemMoveMs = now + _adaptiveRates.GetSendIntervalMs(
				AdaptiveStreamId.WorldItemMoveStream,
				GuestSteamIds());
			SendMovingItemMoves();
		}

		if (now >= _nextItemSnapshotMs)
		{
			_nextItemSnapshotMs = now + _adaptiveRates.GetSendIntervalMs(
				AdaptiveStreamId.WorldItemSnapshotStream,
				GuestSteamIds());
			RefreshWorldItemStates();
			_items.SendPeriodicItemSnapshot();
		}
	}

	internal void ResetSessionState()
	{
		_nextItemMoveMs = 0;
		_nextItemSnapshotMs = 0;
		_throttle.Reset();
	}

	private IEnumerable<ulong> GuestSteamIds() =>
		_session.Members
			.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId);

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
		var entries = new List<WireItemMoveEntry>();
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
			entries.Add(new WireItemMoveEntry
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
