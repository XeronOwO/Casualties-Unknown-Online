using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Instance-id allocation for the item domain: ids are (counter, account id) —
/// globally unique per session without host allocation. EnsureId stamps an
/// ItemInstanceId component on the object, returning 0 when the item is not
/// eligible (still generating — the world-gen determinism covers those). Split
/// out of ItemWorldSync (gate-driven — the report-side class keeps growing
/// with every pickup/drop path fix).
/// </summary>
internal sealed class ItemIdAllocator(SessionService session)
{
	private readonly SessionService _session = session;

	/// <summary>Instance-id counter: ids are (counter, account id) — globally unique per session without host allocation.</summary>
	private ulong _nextItemId;

	private ulong Next() => (_nextItemId++ << 32) | (uint)_session.LocalSteamId;

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
}
