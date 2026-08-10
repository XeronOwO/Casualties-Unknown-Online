using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Generation-item application (guest side): applies the host's generation
/// snapshot. Ground items are bound to the host's ids — the geometry-identical
/// local copy (isolated stream) gets the id, a divergent one (corpse-loot
/// rolls that ran on the real stream, WorldGeneration.cs:3625 suspension
/// period) is replaced by the host's materialization; carried entries bind the
/// starting supplies by slot; local ground items the host does not know are
/// destroyed. After the application every world item on this side carries a
/// host-assigned id — the pickup race (two sides, two ids, one object) is
/// structurally gone.
///
/// The snapshot is held back until the local generation finished: applying
/// earlier would materialize the host's items on top of the local copies that
/// are still being created (a duplicate per entry).
/// </summary>
internal sealed class GeneratedItemApplication(
	ItemService items,
	ItemApplication itemApplication,
	ILogger<GeneratedItemApplication> log)
{
	private readonly ItemService _items = items;
	private readonly ItemApplication _itemApplication = itemApplication;
	private readonly ILogger<GeneratedItemApplication> _log = log;

	/// <summary>The host's latest snapshot, held until the local generation finished. A layer switch's newer snapshot replaces an older one — the pending list is always the current layer's.</summary>
	private List<ItemSnapshotEntryMsg>? _pending;

	internal void BindToSession() => _items.WorldItemsSnapshotReceived += OnWorldItemsSnapshot;

	internal void Unbind() => _items.WorldItemsSnapshotReceived -= OnWorldItemsSnapshot;

	// The layer modifier rides the snapshot — LayerModifierSync applies it
	// (its own subscription); this domain only consumes the item entries.
	private void OnWorldItemsSnapshot(IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState) => _pending = [.. items];

	/// <summary>Pump: apply the held snapshot once the local generation finished.</summary>
	internal void Update()
	{
		if (_pending is null || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var pending = _pending;
		_pending = null;
		Apply(pending);
	}

	private void Apply(List<ItemSnapshotEntryMsg> entries)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var ground = 0;
			var bound = 0;
			var materialized = 0;
			foreach (var entry in entries)
			{
				// Wire encoding: slotIndex + 1, 0 = a world item (protobuf-net
				// omits 0-valued ints — see ItemSnapshotEntryMsg.SlotIndex).
				if (entry.SlotIndex > 0)
				{
					if (TryBindCarried(entry))
					{
						bound++;
					}
					else
					{
						_log.LogWarning("[GenItems] carried entry slot {Slot} ({Type}) has no matching local item — skipped.",
							entry.SlotIndex - 1, entry.Item.ItemId);
					}

					continue;
				}

				ground++;
				var w = new WorldItem(entry.ItemId, entry.Item,
					entry.Position.ToNetVector2(), entry.Velocity.ToNetVector2(),
					entry.ParentItemId, entry.Rotation, entry.FreshItemDrop);
				if (ItemApplication.FindExistingAt(w.Pos, w.Item.ItemId) != null) // Unity object — ==
				{
					bound++; // the local copy gets the host's id (SpawnWorldItem binds, never duplicates)
				}
				else
				{
					materialized++; // a divergent local copy — the host's version is materialized instead
				}

				_itemApplication.SpawnWorldItem(w);
			}

			// Reconciliation: destroy local ground items the host does not know —
			// per-side random spawns (corpse-loot rolls on the real stream) that
			// matched no entry. Bound copies carry the host's id and are
			// untouched.
			var destroyed = 0;
			foreach (var item in Item.allItems.ToList()) // copy: destroying while iterating
			{
				if (item.GetComponent<ItemInstanceId>() != null) // Unity object — ==
				{
					continue;
				}

				if (!ItemWorldSync.IsStandaloneWorldItem(item))
				{
					continue;
				}

				UnityEngine.Object.Destroy(item.gameObject);
				destroyed++;
			}

			_log.LogInformation("[GenItems] applied {Count} entries: {Bound} bound, {Materialized} materialized ({Ground} ground) — destroyed {Destroyed} host-unknown locals.",
				entries.Count, bound, materialized, ground, destroyed);
		}
	}

	/// <summary>Bind the host's id to the local starting supply in the same slot (the guest's slot layout matches the host's — same runSettings, isolated stream).</summary>
	private static bool TryBindCarried(ItemSnapshotEntryMsg entry)
	{
		var body = PlayerCamera.main?.body; // Unity object — ==
		var slotIndex = entry.SlotIndex - 1; // wire encoding: slotIndex + 1
		if (body == null || slotIndex < 0 || slotIndex >= body.slots.Length) // Unity object — ==
		{
			return false;
		}

		var slot = body.slots[slotIndex];
		for (var c = 0; c < slot.transform.childCount; c++)
		{
			var item = slot.transform.GetChild(c).GetComponent<Item>();
			if (item == null || item.GetComponent<ItemInstanceId>() != null) // Unity objects — ==
			{
				continue;
			}

			var idComp = item.gameObject.AddComponent<ItemInstanceId>();
			idComp.Id = entry.ItemId;
			return true;
		}

		return false;
	}
}
