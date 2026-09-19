using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

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
///
/// <para>
/// The report is ABSOLUTE and REPEATABLE (sync-coverage row I8): one frame can
/// be lost in the lazy-P2P swallow window without either side noticing, so this
/// reporter only opens the registration window at the edges that make one
/// meaningful (the generation-finished edge below, and the host's id-watermark
/// grant on join/reconnect — the session binding forwards it) and then
/// RE-CAPTURES the current carried set whenever the runtime's cadence says a
/// report is due (<see cref="CarriedInventoryReportSchedule"/>). Re-capturing
/// rather than replaying the first frame is what keeps a repeat honest: an item
/// the guest destroyed, dropped or handed over is simply absent from the next
/// capture.
/// </para>
/// </summary>
internal sealed class CarriedInventoryReporter(
	IItemControl items,
	ItemIdAllocator ids)
{
	private readonly IItemControl _items = items;
	private readonly ItemIdAllocator _ids = ids;

	private bool _generating; // last frame's IsGenerating — the falling edge is the generation-finished moment

	/// <summary>Pump: detect the generation-finished falling edge and open the
	/// registration window — the report itself goes out on the next frame, when
	/// the runtime's cadence asks for it (that extra frame keeps the enumeration
	/// immune to start-order jitter: corpse loot spawns in CorpseScript.Start,
	/// the same rationale as GeneratedItemAuthority). After the dense window
	/// closes, this pump keeps re-asserting the registration on the steady
	/// cadence, which is how an id self-assigned later (a crafted product)
	/// converges too.</summary>
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
			_items.ArmCarriedInventoryRegistration("the local generation finished");
			return;
		}

		if (_items.IsCarriedInventoryRegistrationDue())
		{
			_items.SendCarriedInventory(CaptureItems());
		}
	}

	private List<CharacterItemMsg> CaptureItems()
	{
		var items = new List<CharacterItemMsg>();
		var body = PlayerCamera.main?.body; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return items; // nothing to capture yet — the window still spends the step
		}

		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			var item = body.GetItem(slot);
			if (item == null || item.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==; display proxies are never authoritative guest items
			{
				continue;
			}

			// EVERY authoritative item of the body belongs to the absolute set, including one
			// that already carries an instance id — a snapshot id the host knows, an id an
			// earlier report of THIS reporter stamped, or a product CraftingSync/ContainerItemSync
			// stamped. EnsureId returns an existing id unchanged, so a repeat states the same
			// set, and the host drops the ids it already has. Skipping bound items is exactly
			// how the re-report goes inert: the first capture stamps an id on every item it
			// reports, so every later capture would come back empty and send nothing at all.
			if (_ids.EnsureId(item) == 0)
			{
				continue; // no id to state: still generating, or a remote kill zeroed the id
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
				if (worn == null || worn.GetComponent<RemoteCloneRender>() != null) // Unity objects — ==; display proxies are never authoritative guest items
				{
					continue;
				}

				if (_ids.EnsureId(worn) == 0)
				{
					continue; // no id to state (same rule as the slot loop)
				}

				items.Add(ItemStateCodec.CaptureItem(worn, -(i + 2)));
			}
		}

		return items;
	}
}
