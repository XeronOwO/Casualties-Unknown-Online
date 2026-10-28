using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Instance-id allocation for the item domain: ids are (counter, account id) —
/// globally unique per session without host allocation (the format itself lives in
/// <see cref="ItemInstanceIds"/>). EnsureId stamps an
/// ItemInstanceId component on the object, returning 0 when the item is not
/// eligible (still generating — the world-gen determinism covers those). Every
/// allocation advances the counter and reports the high-water mark to the host
/// (guest side); the host grants it back on join/reconnect — a crashed-and-
/// rejoined guest's counter restarts from zero and would otherwise reuse ids
/// the host's tables still hold. Split out of ItemWorldSync (gate-driven — the
/// report-side class keeps growing with every pickup/drop path fix).
///
/// <para>
/// The counter is per PROCESS, so the same restart happens on this side when a
/// world is CONTINUED: the archive's ids are stamped back onto the objects the
/// restore rebuilds while this process's counter still starts at zero, and the
/// restore answers with the resume point (<see cref="SetWatermark"/> — delivered
/// through the same watermark the host's grant arrives on, because it is the same
/// fact: the counter this side may resume from).
/// </para>
///
/// <para>
/// The CONTAINER owns the instance, not the adapter's domain graph
/// (<c>GameAdapterComposition</c> registers it): the aggregate's domains allocate
/// through it and the console <c>/give</c> seam allocates through the same one, and
/// a second allocator would be a second counter whose ids the watermark, the peers'
/// snapshots and the archive never saw. The type is public only because the
/// aggregate's public constructor takes it; every game-facing member stays internal,
/// so this is an implementation collaborator and never a contract.
/// </para>
/// </summary>
public sealed class ItemIdAllocator(ISessionControl session, IItemControl items, ILogger<ItemIdAllocator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IItemControl _items = items;
	private readonly ILogger<ItemIdAllocator> _log = log;

	/// <summary>Instance-id counter: ids are (counter, account id) — globally unique per session without host allocation.</summary>
	private ulong _nextItemId;

	private ulong Next()
	{
		var id = ItemInstanceIds.Compose(_nextItemId++, _session.LocalSteamId);
		// The counter advanced — report the high-water mark (guest side; the
		// host's own allocations need no report — it IS the watermark
		// authority). SendItemIdWatermark guards on role + session active.
		_items.SendItemIdWatermark(_nextItemId - 1);
		return id;
	}

	/// <summary>Resume the counter from a watermark (the host's grant on
	/// join/reconnect, or the restored world's own high-water mark): neither a
	/// crashed-and-rejoined counter nor a continued world's may reuse ids the
	/// tables still hold. Only ever raises — a late duplicate grant must not move
	/// it back.</summary>
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
		// THE ONE PLACE EVERY CREATION REPORT PASSES, AND IT ASKS FIRST. An object an eat's delegate created to REPLACE its item is the owner's row
		// becoming another definition in place, so it must never carry an id: with one, every reporting hook (instantiation, pickup, slot, use,
		// container) is free to announce a "new item", which is how a phantom opened fruit reached the world origin while the owner's slot went empty
		// (measured 2026-10-26, five runs). This is asked BEFORE the existing-component read, because the replacement can arrive with an id already
		// stamped (the prefab's own), and returning it was the last way the phantom kept a stable id across every run.
		if (ItemReplacementObservation.IsArmed || ItemReplacementObservation.IsTheCreatedReplacement(item))
		{
			ItemReplacementObservation.Record(item);
			_log.LogInformation("[ItemSpawn] {Type} seen inside the eat's replacement window — no id (the owner's row becomes it in place).", item.id);
			return 0;
		}

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
		// The same refusal as <see cref="EnsureId"/>'s, for the one caller that allocates directly (`ItemWorldSync.OnItemInstantiated`).
		if (ItemReplacementObservation.IsArmed || ItemReplacementObservation.IsTheCreatedReplacement(item))
		{
			ItemReplacementObservation.Record(item);
			return 0;
		}

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
