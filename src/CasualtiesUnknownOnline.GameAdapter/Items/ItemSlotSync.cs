using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The slot-move report side (the domain owner): an item moved between slots
/// (Body.SwapSlots / Body.SwitchHands — the internal drop+pick pair that the
/// reorder scope already keeps silent) or onto a limb (Body.WearWearable). The
/// guest's slot layout is its local fact; the report exists so the host's
/// transfer-table record stays current for corrections and the reconnect
/// merge. The host's OWN moves are its own authority — it broadcasts the full
/// carried item instead, so the peers' clones of the host re-home the item the
/// moment the move lands.
/// <para>
/// This report IS the item's move, so it also resolves a pending DEPARTURE of
/// that item (<see cref="ItemDropState"/>) — a wear straight out of a carried
/// container is detached by <c>Body.WearWearable</c> and never passes a
/// container load or a pickup, and a departure left registered for it could
/// never settle (the item is on a limb, not a standalone world item), which
/// would hold the owner's immediate snapshots back for the rest of the session.
/// </para>
/// </summary>
internal sealed class ItemSlotSync(
	IItemControl items,
	ISessionControl session,
	ItemDropState dropState,
	ItemIdAllocator ids,
	OperationTrace trace,
	ILogger<ItemSlotSync> log)
{
	private readonly IItemControl _items = items;
	private readonly ISessionControl _session = session;
	private readonly ItemDropState _dropState = dropState;
	private readonly ItemIdAllocator _ids = ids;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<ItemSlotSync> _log = log;

	/// <summary>Report the occupant of one slot after a slot move (SwapSlots/SwitchHands). An empty or unbound slot is skipped.</summary>
	internal void OnSlotMoved(Body body, int slot, string origin)
	{
		var item = body.GetItem(slot);
		if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==; empty slot, or a display proxy the release window's redirected read resolved
		{
			return;
		}

		ReportCarried(item, slot, origin);
	}

	/// <summary>
	/// An item was worn straight from the inventory (WearWearable — the radial
	/// menu's center drop): it moved from a slot to a limb, the same carried-fact
	/// report with the limb wear encoding (ItemStateCodec.SlotOf, -(limbIndex + 2))
	/// as the new slot — the peers' clones re-home it the moment the wear lands.
	/// The world-item wear path reports a pickup instead (WearWearablePatch
	/// decides by its captured IsWorldItem verdict).
	/// </summary>
	internal void OnItemWorn(Item item) => ReportCarried(item, ItemStateCodec.SlotOf(item), "Wear");

	/// <summary>
	/// An item was re-homed by the drag-to-slot sequence (PlayerCamera.cs:1623
	/// DropItem + 1629 PickUpItem — the pickup sync cancels the pending drop as
	/// a body-internal reorder, but the move itself still needs the carried-fact
	/// report; before this the character snapshot carried it at 1 Hz).
	/// </summary>
	internal void OnItemRehomed(Item item) => ReportCarried(item, ItemStateCodec.SlotOf(item), "Drag");

	private void ReportCarried(Item item, int slot, string origin)
	{
		// The item was re-homed inside the inventory/body: a pending DEPARTURE of it
		// is resolved here rather than reported as a drop — another carrier (this one)
		// owns the move. Without this edge a wear straight out of a carried container
		// leaves an entry that can never settle, and every later inventory change on
		// this client would lose its immediate clone re-report.
		if (_dropState.TryCancel(item, out var rehomedOp))
		{
			_trace.End(rehomedOp, OperationTrace.IdOf(item), origin, "Cancelled", "ReHomed");
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp == null || idComp.Id == 0) // Unity object — ==; no table entry to record
		{
			// A starting supply may still be id-less — the host assigns lazily
			// on first domain entry (the id must travel with the fact broadcast);
			// the guests' supplies were self-assigned at generation finish.
			if (_session.Role != SessionRole.Host)
			{
				return;
			}

			if (_ids.EnsureId(item) == 0)
			{
				return; // still generating — no allocation possible
			}

			idComp = item.GetComponent<ItemInstanceId>();
		}

		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			_items.SendItemCarriedSync(_session.LocalSteamId, ItemStateCodec.CaptureItem(item, slot));
			_log.LogInformation("[SlotMoved] {Type} (id {ItemId}) → slot {Slot} ({Origin}) — host fact broadcast.", item.id, idComp!.Id, slot, origin);
			return;
		}

		_items.SendItemSlot(idComp!.Id, slot, ItemStateCodec.CaptureDigest(item, slot));
		_log.LogInformation("[SlotMoved] {Type} (id {ItemId}) → slot {Slot} ({Origin}) reported.", item.id, idComp.Id, slot, origin);
	}
}
