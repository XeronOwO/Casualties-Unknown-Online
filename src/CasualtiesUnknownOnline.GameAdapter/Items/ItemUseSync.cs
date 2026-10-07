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
internal sealed class ItemUseSync(IItemControl items, ISessionControl session, ItemIdAllocator ids, ILogger<ItemUseSync> log)
{
	private readonly IItemControl _items = items;
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

		// A STANDING item object is the local incarnation of another member's carried item, so this
		// side's copy is not the fact and NEITHER branch below may take it: the world branch would
		// publish a correction for an id the projection holds no row for (a carried row is not a
		// world row), and the carried branch would publish another member's item as THIS side's own
		// fact. The report that reaches the owner's real item is the food chain's own step — the
		// open direction is the host as the eating side (ticket
		// `docs/backlog/todo/mod-cross-player-solid-food-semantics.md`, §3's use-report row and §6).
		if (StandingItems.Is(item))
		{
			_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) is a standing item object — the use stays local; the report to its owner is the food chain's step.",
				item.id, idComp!.Id);
			return;
		}

		if (_session.Role == SessionRole.Host && _session.SessionActive)
		{
			var capture = ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
			if (ItemWorldSync.IsWorldItem(item))
			{
				// A WORLD item used in place (a ground canister): the local copy
				// IS the fact — the peers' world copies adopt it via the
				// correction path (#194; the carried-fact broadcast below never
				// touched the world copies, so the two sides' canisters diverged
				// permanently).
				_items.SendWorldItemCorrection(_session.LocalSteamId, capture);
				_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) — world item, peers corrected.", item.id, idComp!.Id);
				return;
			}

			// The host's own fact — broadcast the full carried item (SlotOf
			// resolves the hand slot or the wear limb; -1 = unresolvable, the
			// receiver keeps the fact table's slot).
			_items.SendItemCarriedSync(_session.LocalSteamId, capture);
			_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) — host fact broadcast.", item.id, idComp.Id);
			return;
		}

		_items.SendItemUse(idComp.Id, ItemStateCodec.CaptureDigest(item));
		_log.LogInformation("[ItemUsed] {Type} (id {ItemId}) reported (digest).", item.id, idComp.Id);
	}
}
