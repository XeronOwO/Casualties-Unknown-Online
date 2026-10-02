using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Generation-item application (guest side): applies the host's generation
/// snapshot. Ground items are bound to the host's ids — the geometry-identical
/// local copy (isolated stream) gets the id, a divergent one (corpse-loot
/// rolls that ran on the real stream, WorldGeneration.cs:3625 suspension
/// period) is replaced by the host's materialization; local ground items the
/// host does not know are destroyed. After the application every world item on
/// this side carries a host-assigned id — the pickup race (two sides, two ids,
/// one object) is structurally gone. The starting supplies are NOT in the
/// snapshot anymore: every side self-assigns its own ids (the id space is
/// per-SteamId, ItemIdAllocator) and the guests report their carried inventory
/// to the host's transfer table (CarriedInventoryReporter).
///
/// The snapshot is held back until the local generation finished: applying
/// earlier would materialize the host's items on top of the local copies that
/// are still being created (a duplicate per entry). The bind/materialize/drop
/// algorithm itself is shared with the host's restore reconcile
/// (<see cref="GeneratedItemReconcile"/>).
/// </summary>
internal sealed class GeneratedItemApplication(
	IItemControl items,
	GeneratedItemReconcile reconcile,
	ILogger<GeneratedItemApplication> log)
{
	private readonly IItemControl _items = items;
	private readonly GeneratedItemReconcile _reconcile = reconcile;
	private readonly ILogger<GeneratedItemApplication> _log = log;

	/// <summary>The host's latest snapshot, held until the local generation finished. A layer switch's newer snapshot replaces an older one — the pending list is always the current layer's.</summary>
	private List<WorldItem>? _pending;

	internal void BindToSession() => _items.WorldItemsSnapshotReceived += OnWorldItemsSnapshot;

	internal void Unbind()
	{
		_items.WorldItemsSnapshotReceived -= OnWorldItemsSnapshot;
		_pending = null; // a snapshot held from the previous session must not land in the next one
	}

	// The layer modifier rides the snapshot — LayerModifierSync applies it
	// (its own subscription); this domain only consumes the item entries.
	private void OnWorldItemsSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState) => _pending = [.. items];

	/// <summary>Pump: apply the held snapshot once the local generation finished.
	/// The snapshot is held while the local generation runs AND while a run entry
	/// is still loading its world scene: the host can publish before this side's
	/// world object exists (the send stays a handshaken broadcast precisely so a
	/// still-loading member receives it), and the publish is one-shot per
	/// generation — dropping it in that window would lose the id bind and the
	/// host-unknown-local cleanup for the whole layer (adversarial review
	/// major-1). It is dropped only in the menu with no entry in flight: there the
	/// snapshot belongs to a world this side is not entering (batch 20261002-h).</summary>
	internal void Update(bool enteringWorld)
	{
		if (_pending is null)
		{
			return;
		}

		if (HarmonyTraverse.IsGenerating() || (!HarmonyTraverse.HasWorld && enteringWorld))
		{
			return; // hold: our generation runs, or the entry is still loading before the world object exists
		}

		if (!HarmonyTraverse.HasWorld)
		{
			_log.LogDebug("[GenItems] held snapshot dropped: no world scene and no entry in flight ({Count} entry/entries).", _pending.Count);
			_pending = null;
			return;
		}

		var pending = _pending;
		_pending = null;
		var outcome = _reconcile.Apply(pending);
		_log.LogInformation("[GenItems] applied {Count} entries: {Bound} bound, {Materialized} materialized, {Deferred} deferred — destroyed {Destroyed} host-unknown locals.",
			outcome.Entries, outcome.Bound, outcome.Materialized, outcome.Deferred, outcome.Destroyed);
	}
}
