using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The slot-move report side (the domain owner): an item moved between slots
/// (Body.SwapSlots / Body.SwitchHands — the internal drop+pick pair that the
/// reorder scope already keeps silent). The guest's slot layout is its local
/// fact; the report exists so the host's transfer-table record stays current
/// for corrections and the reconnect merge.
/// </summary>
internal sealed class ItemSlotSync(ItemService items, ILogger<ItemSlotSync> log)
{
	private readonly ItemService _items = items;
	private readonly ILogger<ItemSlotSync> _log = log;

	/// <summary>Report the occupant of one slot after a slot move (SwapSlots/SwitchHands). An empty or unbound slot is skipped.</summary>
	internal void OnSlotMoved(Body body, int slot, string origin)
	{
		var item = body.GetItem(slot);
		if (item == null) // Unity object — ==; empty slot
		{
			return;
		}

		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp == null || idComp.Id == 0) // Unity object — ==; no table entry to record
		{
			return;
		}

		_items.SendItemSlot(idComp.Id, slot);
		_log.LogInformation("[SlotMoved] {Type} (id {ItemId}) → slot {Slot} ({Origin}) reported.", item.id, idComp.Id, slot, origin);
	}
}
