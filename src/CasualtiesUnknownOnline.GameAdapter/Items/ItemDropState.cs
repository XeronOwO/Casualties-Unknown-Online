using System.Collections.Generic;

using CasualtiesUnknownOnline.Runtime.Session.Items;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The drop-operation state (game side): one departure spans several hooks
/// (DropItem → ThrowItem / a re-pick / a container load / the frame-end flush,
/// and Container.UnloadItem → the container load that may re-home the item),
/// and the pending entries carry the operation across them. The transition
/// DECISIONS live in the pure <see cref="DropPendingState"/> machine (Runtime —
/// testable, version-independent); this shell holds the game-side mapping (the
/// Item reference, the departure position) and feeds the machine its explicit
/// inputs (item id, Time.frameCount, alive/standalone checks). Every transition
/// goes through this shell so the two states stay in sync.
/// </summary>
internal sealed class ItemDropState
{
	/// <summary>One departure whose report is due: the game-side place plus the operation trace id it resolves with.</summary>
	internal readonly record struct Departure(Item Item, Vector2 Pos, long Op);

	private readonly DropPendingState _machine = new();
	private readonly Dictionary<ulong, (Item Item, Vector2 Pos)> _places = [];

	/// <summary>True while ANY departure entry is registered, owed or not — the flush's cheap gate (it must not walk the scene every frame). The re-report hold asks <see cref="HasReportOwed"/> instead.</summary>
	internal bool HasPending => _machine.HasPending;

	/// <summary>
	/// True while at least one pending departure's report is still OWED: the item it
	/// belongs to is a standalone world item right now. An entry whose item re-entered
	/// the inventory/body (a wear straight out of a carried container, a drag-to-hand
	/// reorder) can never settle — its carrier resolves it — and until one does, this
	/// is what keeps a stuck entry from turning into a PERMANENT hold on the owner's
	/// immediate snapshots (the consequence this cycle's independent review found on
	/// the wear path). A drop that is really coming always qualifies: the
	/// `Body.DropItem` / `DropWearable` patches register the departure in their
	/// PREFIX, the native body detaches the item, and the re-report entry points ask
	/// this AFTER that call — the item is already parentless by then.
	/// </summary>
	internal bool HasReportOwed
	{
		get
		{
			foreach (var place in _places.Values)
			{
				if (place.Item != null && ItemWorldSync.IsStandaloneWorldItem(place.Item)) // Unity object — ==
				{
					return true;
				}
			}

			return false;
		}
	}

	/// <summary>
	/// Opens a departure for one item — where it left and where it stood, plus the
	/// operation trace id. A second departure of the same item replaces its own
	/// entry (the item cannot leave twice); a departure of another item is held
	/// beside it, so a same-frame second departure no longer overwrites the first.
	/// </summary>
	internal void EnterDrop(ulong itemId, Item item, Vector2 pos, long op, DropPendingState.Source source)
	{
		_places[itemId] = (item, pos);
		_machine.EnterDrop(itemId, Time.frameCount, op, source);
	}

	/// <summary>
	/// Takes one item's pending departure WITHOUT a report: the item re-entered the
	/// world's owner (a re-pick into an inventory, a destruction, a replacing
	/// unload) and another path reports that move. Returns the op id for the trace.
	/// </summary>
	internal bool TryCancel(Item item, out long op)
	{
		if (!Take(item, out var pending, out _))
		{
			op = 0;
			return false;
		}

		op = pending.Op;
		return true;
	}

	/// <summary>Takes it AS the report: the game's own throw set the final velocity, so the departure's position and op are what the drop report carries.</summary>
	internal bool TryConsumeByThrow(Item item, out Departure dropped)
	{
		if (!Take(item, out var pending, out var place))
		{
			dropped = default;
			return false;
		}

		dropped = new Departure(place.Item, place.Pos, pending.Op);
		return true;
	}

	/// <summary>
	/// Takes it for a container load: that load is the move and reports it itself,
	/// so the departure must not also go out — and it answers where the item came
	/// from, which is what classifies the load (the scene cannot: the departure's
	/// own unload already detached the item).
	/// </summary>
	internal bool TryConsumeByContainerLoad(Item item, out DropPendingState.Source source, out long op)
	{
		if (!Take(item, out var pending, out _))
		{
			source = default;
			op = 0;
			return false;
		}

		source = pending.Source;
		op = pending.Op;
		return true;
	}

	/// <summary>
	/// The reports due now (appended to <paramref name="settled"/>): every pending
	/// departure whose frame has passed and whose item is alive and a standalone
	/// world item. A same-frame settle is refused (the throw velocity may still
	/// land), and an entry that cannot report yet stays pending for a later hook.
	/// </summary>
	internal void Settle(List<Departure> settled)
	{
		if (!_machine.HasPending)
		{
			return;
		}

		foreach (var itemId in _machine.CopyItemIds())
		{
			if (!_places.TryGetValue(itemId, out var place))
			{
				continue;
			}

			var alive = place.Item != null; // Unity object — ==; destroyed while pending
			if (_machine.TrySettle(itemId, Time.frameCount, alive, alive && ItemWorldSync.IsStandaloneWorldItem(place.Item!), out var pending))
			{
				_places.Remove(itemId);
				settled.Add(new Departure(place.Item!, place.Pos, pending.Op));
			}
		}
	}

	/// <summary>The world was left (scene switch / session end) — the pending items are gone with it; the machine's entries come back so the trace stays balanced.</summary>
	internal void ResetAll(List<DropPendingState.Pending> cancelled)
	{
		_places.Clear();
		_machine.ResetAll(cancelled);
	}

	/// <summary>Takes one item's entry from BOTH halves — the game-side place FIRST, so a place that went missing can never drop the machine's report on the floor.</summary>
	private bool Take(Item item, out DropPendingState.Pending pending, out (Item Item, Vector2 Pos) place)
	{
		var itemId = ItemIdOf(item);
		if (!_places.TryGetValue(itemId, out place) || !_machine.TryTake(itemId, out pending))
		{
			pending = default;
			place = default;
			return false;
		}

		_places.Remove(itemId);
		return true;
	}

	private static ulong ItemIdOf(Item item) => item == null ? 0 : item.GetComponent<ItemInstanceId>()?.Id ?? 0;
}
