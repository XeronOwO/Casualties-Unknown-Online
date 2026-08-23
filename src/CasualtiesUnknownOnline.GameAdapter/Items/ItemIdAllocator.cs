using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Instance-id allocation for the item domain: ids are (counter, account id) —
/// globally unique per session without host allocation. EnsureId stamps an
/// ItemInstanceId component on the object, returning 0 when the item is not
/// eligible (still generating — the world-gen determinism covers those). Every
/// allocation advances the counter and reports the high-water mark to the host
/// (guest side); the host grants it back on join/reconnect — a crashed-and-
/// rejoined guest's counter restarts from zero and would otherwise reuse ids
/// the host's tables still hold. Split out of ItemWorldSync (gate-driven — the
/// report-side class keeps growing with every pickup/drop path fix).
/// </summary>
internal sealed class ItemIdAllocator(SessionService session, ItemService items, ILogger<ItemIdAllocator> log)
{
	private readonly SessionService _session = session;
	private readonly ItemService _items = items;
	private readonly ILogger<ItemIdAllocator> _log = log;

	/// <summary>Instance-id counter: ids are (counter, account id) — globally unique per session without host allocation.</summary>
	private ulong _nextItemId;

	private ulong Next()
	{
		var id = (_nextItemId++ << 32) | (uint)_session.LocalSteamId;
		// The counter advanced — report the high-water mark (guest side; the
		// host's own allocations need no report — it IS the watermark
		// authority). SendItemIdWatermark guards on role + session active.
		_items.SendItemIdWatermark(_nextItemId - 1);
		return id;
	}

	/// <summary>Resume the counter from the host's grant (join/reconnect): the
	/// crashed-and-rejoined counter must not reuse ids the host still holds.
	/// Only ever raises — a late duplicate grant must not move it back.</summary>
	internal void SetWatermark(ulong counter)
	{
		if (counter >= _nextItemId)
		{
			_nextItemId = counter + 1;
		}

		_log.LogInformation("[IdWatermark] set counter {Counter} (resuming at {Next}).", counter, _nextItemId);
	}

	/// <summary>
	/// Return the item's instance id, allocating one when it does not have it
	/// yet — a generation-time item (world-gen determinism covers it, no id)
	/// that enters the world domain through a runtime act (dropped from an
	/// inventory, unloaded from a container, picked up from the ground) needs
	/// an id so the peers can materialize or bind it. Returns 0 when the item
	/// is not eligible (still generating).
	/// </summary>
	internal ulong EnsureId(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		if (idComp != null) // Unity object — ==
		{
			return idComp.Id;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			return 0; // generation-time instantiation — the world-gen determinism covers it
		}

		return Allocate(item);
	}

	/// <summary>Stamp a fresh id on an item that is known to have none (the Item.Start path — generation-time items entering the domain).</summary>
	internal ulong Allocate(Item item)
	{
		var idComp = item.gameObject.AddComponent<ItemInstanceId>();
		idComp.Id = Next();
		return idComp.Id;
	}

	/// <summary>
	/// Allocate a bare instance id without stamping it on a live item. Used by
	/// host-side services that produce a wire item fact from a prefab/stock
	/// entry; the receiving side attaches the id when it restores the item.
	/// </summary>
	internal ulong AllocateId() => Next();
}
