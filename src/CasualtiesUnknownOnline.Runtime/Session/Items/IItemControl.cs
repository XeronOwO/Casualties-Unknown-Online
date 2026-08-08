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
	void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity);

	/// <summary>An item was picked up locally (world → inventory) — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemPickedUp(ulong itemId);

	/// <summary>An item was dropped/placed into the world locally (inventory → world/container) — record and report/broadcast. Vel is the item's velocity at the drop moment (a throw carries a big one); ParentPos is the container's world position when ParentItemId is set (the receiver binds a local generation-time container by position).</summary>
	void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f);

	/// <summary>A world item was destroyed locally — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemDestroyed(ulong itemId);

	/// <summary>Guest only: an item this side GENERATED settled — report its position so the table and the host's phantom align to the generator's physics.</summary>
	void SendItemSettle(ulong itemId, NetVector2 pos, float rotation);

	// ===== Receive side (packet handlers surface the wire here) =====

	void FireItemSpawnedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity);

	void FireItemPickedUpReceived(ulong sender, ulong itemId);

	void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos = default);

	void FireItemDestroyedReceived(ulong sender, ulong itemId);

	/// <summary>Guest side: the host refused an arbitration — roll the local pickup back.</summary>
	void FireItemRejectReceived(ulong sender, ulong itemId);

	/// <summary>Guest side: the authoritative world-item snapshot arrived — reconcile locally.</summary>
	void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items);

	/// <summary>Host side: a guest's generated item settled — update the table entry (generator-side position authority) and align the local phantom.</summary>
	void FireItemSettleReceived(ulong sender, ulong itemId, NetVector2 pos, float rotation);

	/// <summary>Guest side: the host's physics moved items — surface them for the local follow.</summary>
	void FireItemMoveReceived(IReadOnlyList<ItemMoveEntryMsg> items);

	// ===== Host-only surface =====

	/// <summary>Host only: send the full world-item table to one member (on its world entry).</summary>
	void SendItemSnapshot(ulong targetSteamId);

	/// <summary>Host only: the item's live state (position/velocity/rotation) — the periodic keyframe must broadcast the CURRENT positions, not the spawn-time ones.</summary>
	void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation);

	/// <summary>Host only: the table's position for an item (a guest-generated item's settle report) — the host aligns its drifted phantom to it.</summary>
	bool TryGetItemPosition(ulong itemId, out NetVector2 pos);

	/// <summary>
	/// Host only: periodically re-send the full table (unreliable) so physical
	/// drift self-heals — the receiver aligns settled items on the next
	/// reconcile (todo: periodic keyframes).
	/// </summary>
	void SendPeriodicItemSnapshot();

	/// <summary>Host only: a new world layer is generating — the table starts empty again.</summary>
	void ResetItems();

	/// <summary>Host only: broadcast the moving world items' authoritative positions (unreliable — the host's physics is the position authority, the guests follow).</summary>
	void SendItemMove(IReadOnlyList<ItemMoveEntryMsg> items);

	// ===== Application events (the adapter applies these) =====

	/// <summary>An item now exists in the world — materialize it (spawn from the carried state).</summary>
	event Action<WorldItem>? ItemSpawned;

	/// <summary>An item left the world into someone's inventory — remove it (or roll a local pickup back).</summary>
	event Action<ulong>? ItemPickedUp;

	/// <summary>An item now lies in the world at Pos (or inside the container item ParentItemId) — move an existing object there or materialize it (Vel = drop-moment velocity, a throw's flight; ParentPos = the container's position when it needs binding).</summary>
	event Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>? ItemDropped;

	/// <summary>An item was destroyed — remove it locally.</summary>
	event Action<ulong>? ItemDestroyed;

	/// <summary>Host side: a guest's generated item settled — align the local phantom to the generator's position.</summary>
	event Action<ulong, NetVector2, float>? ItemSettledReceived;

	/// <summary>Guest side: the host's physics moved items — follow (apply the authoritative positions).</summary>
	event Action<IReadOnlyList<ItemMoveEntryMsg>>? ItemMoveReceived;

	/// <summary>The host refused our pickup — take the item back out of the inventory.</summary>
	event Action<ulong>? ItemRejected;

	/// <summary>The authoritative snapshot arrived — reconcile the local world items against it.</summary>
	event Action<IReadOnlyList<WorldItem>>? ItemSnapshotReceived;
}
