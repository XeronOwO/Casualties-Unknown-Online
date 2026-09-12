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

	internal void Unbind() => _items.WorldItemsSnapshotReceived -= OnWorldItemsSnapshot;

	// The layer modifier rides the snapshot — LayerModifierSync applies it
	// (its own subscription); this domain only consumes the item entries.
	private void OnWorldItemsSnapshot(IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState) => _pending = [.. items];

	/// <summary>Pump: apply the held snapshot once the local generation finished.</summary>
	internal void Update()
	{
		if (_pending is null || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var pending = _pending;
		_pending = null;
		var outcome = _reconcile.Apply(pending);
		_log.LogInformation("[GenItems] applied {Count} entries: {Bound} bound, {Materialized} materialized — destroyed {Destroyed} host-unknown locals.",
			outcome.Entries, outcome.Bound, outcome.Materialized, outcome.Destroyed);
	}
}
