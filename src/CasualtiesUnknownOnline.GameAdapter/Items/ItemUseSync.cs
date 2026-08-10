using CasualtiesUnknownOnline.Runtime.Session;
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
/// heals on the next ordinary action. The host's OWN use needs no arbitration
/// (its local object IS the fact) — it broadcasts the full carried item
/// instead, so the peers' clones of the host flip the moment the use lands.
/// </summary>
internal sealed class ItemUseSync(ItemService items, ISessionControl session, ItemIdAllocator ids, ILogger<ItemUseSync> log)
{
	private readonly ItemService _items = items;
	private readonly ISessionControl _session = session;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<ItemUseSync> _log = log;

	internal void OnItemUsed(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp == null || idComp.Id == 0) // Unity object — ==; unbound items have no table entry to arbitrate
		{
			// A starting supply may still be id-less — the host assigns lazily
			// on first domain entry (the id must travel with the fact broadcast
			// or the receiver cannot match the instance); the guests' supplies
			// were self-assigned at generation finish (CarriedInventoryReporter).
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
			// The host's own fact — broadcast the full carried item (SlotOf
			// resolves the hand slot or the wear limb; -1 = unresolvable, the
			// receiver keeps the fact table's slot).
			_items.SendItemCarriedSync(_session.LocalSteamId, ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item)));
			_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) — host fact broadcast.", item.id, idComp!.Id);
			return;
		}

		_items.SendItemUse(idComp.Id, ItemStateCodec.CaptureDigest(item));
		_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) reported (digest).", item.id, idComp.Id);
	}
}
