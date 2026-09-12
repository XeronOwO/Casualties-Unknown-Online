using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
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
///
/// A generation a RESTORE drives is the exception to the whole description: the
/// archive's item set is already the truth for that layer, so nothing is
/// assigned or published. The regenerated objects are reconciled against the
/// restored ids instead (<see cref="ReconcileRestoredItems"/>), which is what
/// keeps one physical object from becoming two item families.
/// </summary>
internal sealed class GeneratedItemAuthority(
	ISessionControl session,
	IItemControl items,
	ItemIdAllocator ids,
	GeneratedItemReconcile reconcile,
	ILogger<GeneratedItemAuthority> log)
{
	private readonly ISessionControl _session = session;
	private readonly IItemControl _items = items;
	private readonly ItemIdAllocator _ids = ids;
	private readonly GeneratedItemReconcile _reconcile = reconcile;
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

		if (_items.RestoredWorldItemsPending)
		{
			// The restored layer is not published, but its layer modifier is still this
			// world's definition: without this refresh the host would keep the previous
			// index for the whole restored layer (the normal path below is the only other
			// writer of it).
			RefreshLayerModifier();
			ReconcileRestoredItems();
			return;
		}

		var entries = new List<WorldItem>();
		var ground = 0;

		// Ground items: every standalone world item without an id is a
		// generation-time item (runtime drops/throws got ids the moment they
		// entered the domain). Container contents ride inside their parent's
		// Contents — never enumerated independently. The starting supplies are
		// NOT enumerated anymore: every side self-assigns its own ids (the id
		// space is per-SteamId, ItemIdAllocator) and the guests report their
		// carried inventory to the host's transfer table (CarriedInventoryReporter)
		// — the old host-distributed carried ids gave every side the SAME ids
		// for different objects (a drop collided with another side's backpack
		// copy).
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

		if (entries.Count == 0)
		{
			return; // the Clear edge (a layer switch clears before generating) — nothing to publish
		}

		RefreshLayerModifier();

		_items.PublishGeneratedItems(entries);
		_log.LogInformation("[GenItems] host published {Ground} ground items (modifier {Modifier}).",
			ground, _items.LayerModifierIndex);
	}

	/// <summary>
	/// The layer modifier the host's world rolled at generation finish — the
	/// world definition, riding the snapshot (the modifier decision reads the
	/// random stream AFTER the darken-wait suspension, which the isolation
	/// does not restore, so every side rolls its own — the host's is
	/// authoritative). The decision's random start rides along so the guests
	/// replay the draws before Initialize (identical world effects). The restore
	/// reconcile refreshes it too: that generation is not published, but it is
	/// still a layer the host's world defined.
	/// </summary>
	private void RefreshLayerModifier()
	{
		var modifierIndex = LayerModifier.availableModifiers.FirstOrDefault(m => m.active)?.modifierIndex ?? -1;
		_items.LayerModifierIndex = modifierIndex;
		_items.LayerModifierRandomState = modifierIndex >= 0 ? LayerModifierApplyPatch.LastEntryState : null;
	}

	/// <summary>
	/// A mid-run restore owns this layer's item set: the objects the game just
	/// regenerated are the SAME physical objects the cut described, so they are
	/// reconciled against the restored ids — bind a matching object, materialize
	/// what generation did not create, destroy the leftovers the cut never
	/// described — instead of being published under fresh ids. It runs at the
	/// publish moment (one frame after the generation-finished edge), NOT at the
	/// baseline capture: corpse loot spawns in CorpseScript.Start a frame after the
	/// edge, and reconciling earlier would leave those objects behind as duplicates.
	/// </summary>
	private void ReconcileRestoredItems()
	{
		var restored = _items.ReadRestoredWorldItems();
		var outcome = _reconcile.Apply(restored);
		_items.CompleteRestoredWorldItems(outcome.Applied, outcome.Refused);
		_log.LogInformation(
			"[GenItems] restored cut: {Applied} of {Entries} restored item(s) landed ({Bound} bound, {Materialized} materialized), {Destroyed} regenerated leftover(s) destroyed.",
			outcome.Applied, outcome.Entries, outcome.Bound, outcome.Materialized, outcome.Destroyed);
	}

	/// <summary>Allocate the host's id (the host's counter — ids can never collide with a guest's) and capture the full state.</summary>
	private WorldItem BuildEntry(Item item, int slotIndex)
	{
		var itemId = _ids.Allocate(item);
		var pos = item.transform.position;
		var vel = item.rb.velocity;
		return new WorldItem(
			itemId,
			ItemStateCodec.CaptureItem(item, slotIndex),
			new NetVector2(pos.x, pos.y),
			new NetVector2(vel.x, vel.y),
			0,
			item.transform.eulerAngles.z,
			false);
	}
}
