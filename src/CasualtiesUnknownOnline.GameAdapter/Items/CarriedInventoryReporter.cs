using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The carried inventory with self-assigned ids (guest side): once the local
/// world generation finished, every id-less carried item — the starting
/// supplies and the worn items — gets a self-assigned instance id (ids are
/// (counter &lt;&lt; 32) | SteamId, so the guest allocates without host
/// round-trips; the watermark keeps a crashed-and-rejoined counter from
/// reusing ids the host still holds) and the full list is reported. The host
/// registers it in the guest's transfer table — the authoritative record that
/// makes the guest's use/slot reports arbitrate normally (before this, a
/// starting-supply item had no host-side record and the report's evidence was
/// broadcast instead). The host's own supplies need no report — its local
/// objects ARE the authority; their ids are assigned lazily on first domain
/// entry (EnsureId in the use/slot/drop chains).
/// </summary>
internal sealed class CarriedInventoryReporter(
	ISessionControl session,
	ItemService items,
	ItemIdAllocator ids,
	ILogger<CarriedInventoryReporter> log)
{
	private readonly ISessionControl _session = session;
	private readonly ItemService _items = items;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<CarriedInventoryReporter> _log = log;

	private bool _generating; // last frame's IsGenerating — the falling edge is the generation-finished moment
	private bool _reportPending; // one frame after the edge (the same pattern as GeneratedItemAuthority)

	/// <summary>Pump: detect the generation-finished falling edge and report one
	/// frame later — the extra frame makes the enumeration immune to start-order
	/// jitter (corpse loot spawns in CorpseScript.Start, same rationale as
	/// GeneratedItemAuthority).</summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (generating)
		{
			_generating = true;
			return;
		}

		if (_generating)
		{
			_generating = false;
			_reportPending = true;
			return;
		}

		if (!_reportPending)
		{
			return;
		}

		_reportPending = false;
		Report();
	}

	private void Report()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return; // the host's supplies get ids lazily (EnsureId on first domain entry) — no report
		}

		var body = PlayerCamera.main?.body; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return;
		}

		var items = new List<CharacterItemMsg>();
		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null) // Unity object — ==
			{
				continue;
			}

			if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already bound (a snapshot id)
			{
				continue;
			}

			if (_ids.EnsureId(item) == 0)
			{
				continue; // still generating — the pump runs after the edge, so this should not happen
			}

			items.Add(ItemStateCodec.CaptureItem(item, slot));
		}

		// Worn items (limb children — the wear encoding, ItemStateCodec.SlotOf).
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i].transform;
			for (var c = 0; c < limb.childCount; c++)
			{
				var worn = limb.GetChild(c).GetComponent<Item>();
				if (worn == null) // Unity object — ==
				{
					continue;
				}

				if (worn.GetComponent<ItemInstanceId>() != null) // Unity object — ==
				{
					continue;
				}

				if (_ids.EnsureId(worn) == 0)
				{
					continue;
				}

				items.Add(ItemStateCodec.CaptureItem(worn, -(i + 2)));
			}
		}

		if (items.Count == 0)
		{
			return;
		}

		_items.SendCarriedInventory(items);
		_log.LogInformation("[CarriedInventory] reported {Count} carried items with self-assigned ids.", items.Count);
	}
}
