using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The world-item surface packet handlers operate on — implemented by
/// ItemService. Handlers depend on this narrow interface instead of the
/// concrete service, which keeps the constructor graph acyclic (abstract
/// extraction, user rule). One surface serves both roles: on the host the
/// receive-side calls arbitrate against the authoritative table and relay; on
/// the guest they surface the events for the adapter to apply.
/// </summary>
public interface IItemControl
{
	// ===== Report side (the adapter's local compute reports here) =====

	/// <summary>A runtime-generated item entered the world locally — record (host/solo) and report/broadcast.</summary>
	void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation);

	/// <summary>An item was picked up locally (world → inventory) — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemPickedUp(ulong itemId);

	/// <summary>An item was dropped/placed into the world locally (inventory → world/container) — record and report/broadcast.</summary>
	void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, ulong parentItemId, float rotation);

	/// <summary>A world item was destroyed locally — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemDestroyed(ulong itemId);

	// ===== Receive side (packet handlers surface the wire here) =====

	void FireItemSpawnedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation);

	void FireItemPickedUpReceived(ulong sender, ulong itemId);

	void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, ulong parentItemId, float rotation);

	void FireItemDestroyedReceived(ulong sender, ulong itemId);

	/// <summary>Guest side: the host refused an arbitration — roll the local pickup back.</summary>
	void FireItemRejectReceived(ulong sender, ulong itemId);

	/// <summary>Guest side: the authoritative world-item snapshot arrived — reconcile locally.</summary>
	void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items);

	// ===== Host-only surface =====

	/// <summary>Host only: send the full world-item table to one member (on its world entry).</summary>
	void SendItemSnapshot(ulong targetSteamId);

	/// <summary>Host only: a new world layer is generating — the table starts empty again.</summary>
	void ResetItems();

	// ===== Application events (the adapter applies these) =====

	/// <summary>An item now exists in the world — materialize it (spawn from the carried state).</summary>
	event Action<WorldItem>? ItemSpawned;

	/// <summary>An item left the world into someone's inventory — remove it (or roll a local pickup back).</summary>
	event Action<ulong>? ItemPickedUp;

	/// <summary>An item now lies in the world at Pos (or inside the container item ParentItemId) — move an existing object there or materialize it.</summary>
	event Action<ulong, CharacterItemMsg, NetVector2, ulong, float>? ItemDropped;

	/// <summary>An item was destroyed — remove it locally.</summary>
	event Action<ulong>? ItemDestroyed;

	/// <summary>The host refused our pickup — take the item back out of the inventory.</summary>
	event Action<ulong>? ItemRejected;

	/// <summary>The authoritative snapshot arrived — reconcile the local world items against it.</summary>
	event Action<IReadOnlyList<WorldItem>>? ItemSnapshotReceived;
}
