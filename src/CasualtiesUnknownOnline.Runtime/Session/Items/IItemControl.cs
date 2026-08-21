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

	/// <summary>An item was picked up locally (world → inventory) — drop it from the table (host/solo) and report/broadcast. Evidence (digest form) rides the guest report for the host's accept-with-correction check.</summary>
	void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null);

	/// <summary>Guest only: an item was used locally (Body.UseItem) — report the used state (digest evidence) so the host validates and corrects.</summary>
	void SendItemUse(ulong itemId, CharacterItemMsg item);

	/// <summary>Guest only: an item moved slots locally (SwapSlots / SwitchHands) — report the new slot so the host's record stays in sync. The digest evidence rides along (the host broadcasts it as the carried-fact event when it has no transfer-table entry — a starting-supply item).</summary>
	void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item);

	/// <summary>Guest only: a carried container's full fact changed internally (a nested-content move) — report the parent container so the host records it and relays it as the carried-fact event (one operation = one message).</summary>
	void SendItemContainerContent(ulong itemId, CharacterItemMsg item);

	/// <summary>An item was dropped/placed into the world locally (inventory → world/container) — record and report/broadcast. Vel is the item's velocity at the drop moment (a throw carries a big one); ParentPos is the container's world position when ParentItemId is set (the receiver binds a local generation-time container by position).</summary>
	void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f);

	/// <summary>A world item was destroyed locally — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemDestroyed(ulong itemId);

	/// <summary>
	/// Host/solo: a Heater cooker converted a raw meat item into a steak
	/// (Heater.OnCollisionEnter2D) — remove the source and register the cooked
	/// item in the world table in ONE transition, then broadcast the complete
	/// cooked-item state as one host→guest ItemCook event (never a decomposed
	/// ItemDestroy + ItemSpawn pair). The host's scene already ran the native
	/// conversion; the adapter calls this only from the verified patch postfix.
	/// </summary>
	void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity);

	// ===== Receive side (packet handlers surface the wire here) =====

	void FireItemSpawnedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity);

	void FireItemPickedUpReceived(ulong sender, ulong itemId, CharacterItemMsg? evidence);

	void FireItemDroppedReceived(ulong sender, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos = default);

	void FireItemDestroyedReceived(ulong sender, ulong itemId);

	/// <summary>Guest side: the host's authoritative heater-conversion event arrived — surface the source id and the full cooked-steak state for the adapter's one-scope apply.</summary>
	void FireItemCookedReceived(ulong sender, ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity);

	/// <summary>Guest side: the host refused an arbitration — roll back (UnknownItem = a refused pickup, BlockAlreadyBroken = a refused block break's drops to destroy).</summary>
	void FireItemRejectReceived(ulong sender, ulong itemId, ItemRejectMsg.Reason reason);

	/// <summary>Host/solo: record the drops of a LOCALLY broken block into the authoritative table (the report travels inside BlockDamagedMsg — never a standalone spawn report).</summary>
	void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops);

	/// <summary>A break with drops was applied — register (host only) and materialize every drop.</summary>
	void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops);

	/// <summary>Host only: refuse a reported break's drops (the break was already applied — first-writer-wins) — the reporter destroys its local drops.</summary>
	void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason);

	/// <summary>Guest side: the authoritative world-item snapshot arrived — reconcile locally. LayerModifierIndex/LayerModifierRandomState ride along (the world's current modifier and its decision's random start, -1/null = none).</summary>
	void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState);

	/// <summary>Guest side: the host's generation-time item snapshot arrived — bind local copies to the host's ids, materialize the host's version, destroy host-unknown locals. LayerModifierIndex/LayerModifierRandomState ride along (the host's rolled layer modifier and its decision's random start, -1/null = none).</summary>
	void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState);

	/// <summary>Guest side: the host's physics moved items — surface them for the local follow.</summary>
	void FireItemMoveReceived(IReadOnlyList<ItemMoveEntryMsg> items);

	/// <summary>Guest side: the host's authoritative item state arrived (our action-report evidence diverged) — apply it via the restore machinery.</summary>
	void FireItemCorrectionReceived(ulong sender, CharacterItemMsg item);

	/// <summary>An item was used locally (Body.UseItem) — report the used state (digest evidence) so the host validates and corrects.</summary>
	void FireItemUseReceived(ulong sender, ulong itemId, CharacterItemMsg item);

	/// <summary>An item moved slots locally (SwapSlots / SwitchHands) — report the new slot so the host's record stays in sync. The digest evidence rides along (the host broadcasts it when it has no transfer-table entry).</summary>
	void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex, CharacterItemMsg item);

	/// <summary>A carried container's full fact changed internally (nested-content move) — the host records it and relays it as the carried-fact event.</summary>
	void FireItemContainerContentReceived(ulong sender, ulong itemId, CharacterItemMsg item);

	/// <summary>Guest only: an item-instance id was allocated locally — report the counter high-water mark so the host can grant it back on a reconnect (a crashed-and-rejoined counter restarts from zero and would reuse ids the host still holds).</summary>
	void SendItemIdWatermark(ulong counter);

	/// <summary>Guest only: the carried inventory with self-assigned ids (the local generation finished) — the host registers it in the guest's transfer table.</summary>
	void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items);

	/// <summary>Host only: grant a member's id watermark (its allocations may resume from counter + 1).</summary>
	void GrantItemIdWatermark(ulong targetSteamId, ulong counter);

	/// <summary>The id counter high-water mark arrived: host records it, guest applies it (resume from counter + 1).</summary>
	void FireItemIdWatermarkReceived(ulong sender, ulong counter);

	/// <summary>A guest's carried inventory with self-assigned ids arrived (its local generation finished) — the host registers it in the guest's transfer table.</summary>
	void FireCarriedInventoryReceived(ulong sender, IReadOnlyList<CharacterItemMsg> items);

	/// <summary>The authoritative fact of one carried item arrived (host → guest): a use flipped its state, a slot move re-homed it, a pickup brought it in — update the owner's fact table entry and re-render the clone. SlotKnown = false means keep the fact table's existing slot.</summary>
	void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown);

	// ===== Host-only surface =====

	/// <summary>Host only: send the full world-item table to one member (on its world entry).</summary>
	void SendItemSnapshot(ulong targetSteamId);

	/// <summary>Host only: send one guest the authoritative state of an item (its action-report evidence diverged) — accept-with-correction, never a rejection.</summary>
	void SendItemCorrection(ulong targetSteamId, CharacterItemMsg item);

	/// <summary>Host only: correct every OTHER member's copy of a used world item (drinking from a ground canister, #194) — the user's own copy IS the fact, every peer's copy adopts it via the standard correction path.</summary>
	void SendWorldItemCorrection(ulong exceptSteamId, CharacterItemMsg item);

	/// <summary>Host only: the items a guest currently owns (the transfer table — where the host moved world-table entries as the guest's actions took them). The reconnect restore merges these into the character snapshot.</summary>
	IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId);

	/// <summary>Host only: broadcast one carried item's authoritative fact (use/slot move/pickup) to every guest except its owner — the peers update the owner's fact table and re-render the clone immediately (reliable; the 1 Hz snapshot is the fallback).</summary>
	void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item);

	/// <summary>Host only: the item's live state (position/velocity/rotation/condition) — the periodic keyframe must broadcast the CURRENT state, not the spawn-time one (stale positions yank settled items around; a stale condition re-aligns the peers' decay to the wrong value).</summary>
	void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition);

	/// <summary>
	/// Host only: periodically re-send the full table (unreliable) so physical
	/// and top-level state drift self-heals — the receiver aligns settled items
	/// on the next reconcile.
	/// </summary>
	void SendPeriodicItemSnapshot();

	/// <summary>Host only: a new world layer is generating — the table starts empty again.</summary>
	void ResetItems();

	/// <summary>Host only: the generation finished — register the generation-time items (host-assigned ids, ground + starting supplies) into the table and broadcast them as one snapshot (the guests bind their local copies or materialize the host's version). The current layer modifier (LayerModifierIndex) rides along.</summary>
	void PublishGeneratedItems(IReadOnlyList<ItemSnapshotEntryMsg> entries);

	/// <summary>Host side: the world's current layer modifier (index into the game's LayerModifier.availableModifiers, -1 = none) — rides the world-item snapshots so a world entry outside a generation still receives it. A projection of world state: the adapter refreshes it when a generation finishes.</summary>
	int LayerModifierIndex { get; set; }

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

	/// <summary>The host's heater conversion arrived — kill the raw-meat copy and materialize the cooked steak atomically.</summary>
	event Action<ulong, WorldItem>? ItemCookedReceived;

	/// <summary>Guest side: the host's physics moved items — follow (apply the authoritative positions).</summary>
	event Action<IReadOnlyList<ItemMoveEntryMsg>>? ItemMoveReceived;

	/// <summary>The host refused an item arbitration — roll back (UnknownItem = pickup, BlockAlreadyBroken = block-break drops to destroy).</summary>
	event Action<ulong, ItemRejectMsg.Reason>? ItemRejected;

	/// <summary>The authoritative snapshot arrived — reconcile the local world items against it (the layer modifier + its random start ride along).</summary>
	event Action<IReadOnlyList<WorldItem>, int, byte[]?>? ItemSnapshotReceived;

	/// <summary>The host's generation-time item snapshot arrived — the adapter binds local copies / materializes / destroys the host-unknown ones (the layer modifier + its random start ride along).</summary>
	event Action<IReadOnlyList<ItemSnapshotEntryMsg>, int, byte[]?>? WorldItemsSnapshotReceived;

	/// <summary>Guest side: the host's authoritative item state arrived (our action-report evidence diverged) — the adapter applies it (materialize missing contents, fix state, fix slot).</summary>
	event Action<CharacterItemMsg>? ItemCorrectionReceived;

	/// <summary>The authoritative fact of one carried item changed (host broadcast: use/slot move/pickup) — the adapter updates the owner's per-player fact table and re-renders the clone. Fired on the guests from the wire and on the host directly (its own arbitration decisions).</summary>
	event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived;

	/// <summary>Guest side: the host granted the id counter high-water mark (join/reconnect) — the adapter resumes the allocator from counter + 1.</summary>
	event Action<ulong>? ItemIdWatermarkReceived;
}
