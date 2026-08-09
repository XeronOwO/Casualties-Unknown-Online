using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The use-chain's report side (the domain owner): an item was used
/// (Body.UseItemInHand — the LMB click on a usable item; Body.UseItem — the
/// radial-menu drag and the recipe consumption). One use = one report carrying
/// the post-use digest: the host validates the state against the item's
/// transfer-table entry, adopts a matching evidence (the guest is the fact
/// source for its own body) and sends an ItemCorrection when it diverges.
/// Usage itself is never rejected — this report exists so wrong stored state
/// heals on the next ordinary action.
/// </summary>
internal sealed class ItemUseSync(ItemService items, ILogger<ItemUseSync> log)
{
	private readonly ItemService _items = items;
	private readonly ILogger<ItemUseSync> _log = log;

	internal void OnItemUsed(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp == null || idComp.Id == 0) // Unity object — ==; unbound items have no table entry to arbitrate
		{
			return;
		}

		_items.SendItemUse(idComp.Id, ItemStateCodec.CaptureDigest(item));
		_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) reported (digest).", item.id, idComp.Id);
	}
}
