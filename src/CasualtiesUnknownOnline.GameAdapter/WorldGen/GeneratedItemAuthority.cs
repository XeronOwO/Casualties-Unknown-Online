using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Generation-item authority (host side of the world-gen item sync): when a
/// world generation finishes, the host assigns an instance id to every
/// generation-time item — the ground items (bandages, corpse loot, oil pipes,
/// … — created inside WorldPlaceEntities / CorpseScript.Start, which the
/// IsGenerating guard keeps out of the item domain) AND the starting supplies
/// in its own backpack — then registers them in the authoritative table and
/// broadcasts the full set as one snapshot. The guests bind their local copies
/// to the host's ids or materialize the host's version; a divergent local copy
/// is destroyed. Without this, every side allocates its own id when a
/// generated item first enters the domain (picked up) — two sides, two ids,
/// one object: the pickup race (host "unknown item" refusals, duplicate
/// materializations).
///
/// Generation is isolated unconditionally (solo too), so the enumeration runs
/// for solo/host alike: a solo-turned-lobby host already has the table
/// populated and a late joiner receives the items via the ordinary snapshot
/// (SendItemSnapshot) — no special "solo → lobby" backfill path exists.
/// </summary>
internal sealed class GeneratedItemAuthority(
	SessionService session,
	ItemService items,
	ItemIdAllocator ids,
	ILogger<GeneratedItemAuthority> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ItemIdAllocator _ids = ids;
	private readonly ILogger<GeneratedItemAuthority> _log = log;

	private bool _generating; // last frame's IsGenerating — the falling edge is the generation-finished moment
	private bool _publishPending; // one frame after the edge: every Start has run (corpse loot spawns in Start) — safe to enumerate

	/// <summary>
	/// Pump: detect the generation-finished falling edge and publish one frame
	/// later. The extra frame matters: corpse loot spawns in CorpseScript.Start,
	/// which runs a frame after the corpse instantiation while the generation
	/// coroutine is suspended in FinishWorldGeneration's darken wait — the edge
	/// (generatingWorld = false) fires after the fade, and one more frame makes
	/// the enumeration immune to start-order jitter. The layer-switch Clear
	/// phase (generatingWorld flips once more) produces an empty publish, which
	/// is filtered below.
	/// </summary>
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
			_publishPending = true;
			return;
		}

		if (!_publishPending)
		{
			return;
		}

		_publishPending = false;
		Publish();
	}

	private void Publish()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return; // guests never enumerate — the host's snapshot is authoritative
		}

		var entries = new List<ItemSnapshotEntryMsg>();
		var ground = 0;
		var carried = 0;

		// Ground items: every standalone world item without an id is a
		// generation-time item (runtime drops/throws got ids the moment they
		// entered the domain). Container contents ride inside their parent's
		// Contents — never enumerated independently.
		foreach (var item in Item.allItems)
		{
			if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==; already in the domain
			{
				continue;
			}

			if (!ItemWorldSync.IsStandaloneWorldItem(item))
			{
				continue;
			}

			entries.Add(BuildEntry(item, slotIndex: -1));
			ground++;
		}

		// Starting supplies: the host's own backpack at the generation-finished
		// moment (the game hands them out inside generation,
		// WorldGeneration.WorldPlacePlayer:1904-1913). Bound by slot — the
		// guests' copies sit in the same slots (same runSettings, isolated
		// stream). Only here, before any play can change the inventory.
		var body = PlayerCamera.main?.body; // Unity object — ==
		if (body != null)
		{
			for (var slot = 0; slot < body.slots.Length; slot++)
			{
				var item = body.GetItem(slot);
				if (item == null) // Unity object — ==
				{
					continue;
				}

				if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==
				{
					continue;
				}

				entries.Add(BuildEntry(item, slot));
				carried++;
			}
		}

		if (entries.Count == 0)
		{
			return; // the Clear edge (a layer switch clears before generating) — nothing to publish
		}

		// The layer modifier the host's world rolled at generation finish — the
		// world definition, riding the snapshot (the modifier decision reads the
		// random stream AFTER the darken-wait suspension, which the isolation
		// does not restore, so every side rolls its own — the host's is
		// authoritative). The decision's random start rides along so the guests
		// replay the draws before Initialize (identical world effects).
		var modifierIndex = LayerModifier.availableModifiers.FirstOrDefault(m => m.active)?.modifierIndex ?? -1;
		_items.LayerModifierIndex = modifierIndex;
		_items.LayerModifierRandomState = modifierIndex >= 0 ? LayerModifierApplyPatch.LastEntryState : null;

		_items.PublishGeneratedItems(entries);
		_log.LogInformation("[GenItems] host published {Ground} ground + {Carried} carried items (modifier {Modifier}).",
			ground, carried, _items.LayerModifierIndex);
	}

	/// <summary>Allocate the host's id (the host's counter — ids can never collide with a guest's) and capture the full state.</summary>
	private ItemSnapshotEntryMsg BuildEntry(Item item, int slotIndex)
	{
		var itemId = _ids.Allocate(item);
		var pos = item.transform.position;
		var vel = item.rb.velocity;
		return new ItemSnapshotEntryMsg
		{
			ItemId = itemId,
			Item = ItemStateCodec.CaptureItem(item, slotIndex),
			Position = new NetVector2(pos.x, pos.y).ToNetVector2Msg(),
			Velocity = new NetVector2(vel.x, vel.y).ToNetVector2Msg(),
			Rotation = item.transform.eulerAngles.z,
			FreshItemDrop = false,
			// Wire encoding is slotIndex + 1 (0 = world item) — protobuf-net omits
			// 0-valued ints, and backpack slot 0 is a valid raw index (see
			// ItemSnapshotEntryMsg.SlotIndex).
			SlotIndex = slotIndex + 1,
		};
	}
}
