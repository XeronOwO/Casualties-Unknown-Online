using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
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
public interface IItemControl : IRestoredWorldItemSource
{
	// ===== Data facts (read-only) =====

	/// <summary>
	/// Read-only: does the authoritative item data hold this id as a WORLD row?
	/// The world table holds world rows alone, so a carried id, an id the data
	/// has never judged and id 0 all answer false.
	/// <para>
	/// The Game Adapter's standing-item-object category is anchored on this
	/// answer: such an object is the local incarnation of an item the data
	/// carries as somebody's CARRIED row, and the category ends the moment the
	/// data moves the id into the world. Ask it by id, never by scene state —
	/// the same fact the kernel, the projection, the position stream and the
	/// reconcile already read, so a category anchored here cannot drift from the
	/// data it mirrors.
	/// </para>
	/// </summary>
	bool IsWorldItemRegistered(ulong itemId);

	/// <summary>
	/// Read-only: the kernel's judged row for any item, or null when this host never judged the id.
	/// <see cref="IsWorldItemRegistered"/> answers "is it WORLD content"; this answers "what does the
	/// host know about it at all" — the trade give's credit is derived from the row the reporter names
	/// (its definition and its instance state), and an id the host never judged has no state to judge a
	/// claim against. The caller reads the row's location and data as the receiver's own facts; the
	/// report's claim never overwrites them.
	/// </summary>
	ItemState? FindJudgedItem(ulong instanceId);

	// ===== Report side (the adapter's local compute reports here) =====

	/// <summary>A runtime-generated item entered the world locally — record (host/solo) and report/broadcast.</summary>
	void SendItemSpawned(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity);

	/// <summary>An item was picked up locally (world → inventory) — drop it from the table (host/solo) and report/broadcast. Evidence (digest form) rides the guest report for the host's accept-with-correction check.</summary>
	void SendItemPickedUp(ulong itemId, CharacterItemMsg? evidence = null);

	/// <summary>
	/// Both sides: ONE item was taken out of a container into this client's own carried
	/// inventory (a bag's child released onto a slot). The kernel records a container's
	/// children as its rows, so the item is still <c>Contained</c> there and the slot report
	/// (data only) cannot move it — this is the report that states the arrival. Same kernel
	/// relocation as <see cref="SendItemPickedUp"/>; it travels under its own kind because a
	/// refusal settles differently (the item is in this client's hands, so the way back is the
	/// carried-set statement, never the pickup rollback that would unparent it into the
	/// world).
	/// </summary>
	void SendItemContainerTakeOut(ulong itemId, CharacterItemMsg? evidence = null);

	/// <summary>Guest only: an item was used locally (Body.UseItem) — report the used state (digest evidence) so the host validates and corrects.</summary>
	void SendItemUse(ulong itemId, CharacterItemMsg item);

	/// <summary>
	/// Guest only: an item's persistent state changed WITHOUT one of the game's two
	/// guarded use call sites having run — the gun chain's rack, safety, load, unload and
	/// timed auto-rack transitions. It travels as the plain state report, which claims no
	/// use: the host's native-use bound judges a claimed use by the item's own data, and a
	/// state transition is not one. Host-side changes are the host's own fact, never
	/// reported.
	/// </summary>
	void SendItemState(ulong itemId, CharacterItemMsg item);

	/// <summary>Guest only: an item moved slots locally (SwapSlots / SwitchHands) — report the new slot so the host's record stays in sync. The digest evidence rides along (the host broadcasts it as the carried-fact event when it has no transfer-table entry — a starting-supply item).</summary>
	void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item);

	/// <summary>
	/// Guest only: a CONSOLE command wrote one item's state (<c>/data set</c>) — the
	/// holder's own report of a value an operator typed. It travels under its own
	/// kind, so the host judges it against its two cheat rulings (the family switch,
	/// then the roster) instead of reading it as a native use; the row it writes is
	/// the same either way. A host refusal comes back as its own verdict and this
	/// client settles the value it already applied
	/// (<see cref="ItemStateSettlement"/>).
	/// </summary>
	void SendItemDataWrite(ulong itemId, CharacterItemMsg item);

	/// <summary>Guest only: a carried container's full fact changed internally (a nested-content move) — report the parent container so the host records it and relays it as the carried-fact event (one operation = one message).</summary>
	void SendItemContainerContent(ulong itemId, CharacterItemMsg item);

	/// <summary>An item was dropped/placed into the world locally (inventory → world/container) — record and report/broadcast. Vel is the item's velocity at the drop moment (a throw carries a big one); ParentPos is the container's world position when ParentItemId is set (the receiver binds a local generation-time container by position).</summary>
	void SendItemDropped(ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, NetVector2 parentPos = default, float angularVelocity = 0f);

	/// <summary>A world item was destroyed locally — drop it from the table (host/solo) and report/broadcast.</summary>
	void SendItemDestroyed(ulong itemId);

	/// <summary>
	/// The local body's own limb action turned a carried item into a limb component
	/// (the game's own <c>useLimbAction</c> adding a <c>SplintLimb</c> /
	/// <c>TourniquetScript</c> and destroying the object it was handed): the item
	/// gains a location on that limb and KEEPS its instance id, so this is a
	/// relocation rather than the destroy the generic <c>Item.OnDestroy</c> hook
	/// would report. Host/solo records it through the kernel; a guest reports it as
	/// the one command the host judges, and the committed batch brings the row back.
	/// </summary>
	void SendItemAppliedToLimb(ulong itemId, int limbIndex);

	/// <summary>
	/// The local body's limb component came off as an item again (the game's own
	/// <c>TakeOff</c> instantiating the item from the component's template): the item
	/// moves from the limb into the body's own hands and keeps the id it had while it
	/// was installed. The caller names the item, because it is the one that resolved
	/// the id from the applied row before the game created the object.
	/// <para>
	/// <paramref name="item"/> is the revived object's own state at the moment it lands
	/// — its condition and, when it went to a slot or a limb, the real placement
	/// (<c>SlotIndex</c>; -1 for a container or the world). The relocation keeps the
	/// applied row's data when this is null, and that data was captured BEFORE the
	/// apply: its slot is where the item used to be, and it names no new placement. So
	/// the caller that can see the revived object passes what it sees.
	/// </para>
	/// </summary>
	void SendItemRemovedFromLimb(ulong itemId, int limbIndex, CharacterItemMsg? item = null);

	/// <summary>
	/// Read-only: the item the authoritative data records as INSTALLED on this limb —
	/// the applied row whose definition is the component's own template, or 0 when the
	/// data holds none. The native <c>TakeOff</c> is the only gesture that turns a limb
	/// component back into an item, and it has to know WHICH item it is reviving; the
	/// limb row says the component is there, and this answers which item row belongs to
	/// it. 0 is the honest "this component was never an id CUO tracked", never a
	/// licence to mint one.
	/// </summary>
	ulong FindAppliedLimbItem(ulong owner, int limbIndex, string definitionId);

	/// <summary>
	/// Host/solo: a Heater cooker converted a raw meat item into a steak
	/// (Heater.OnCollisionEnter2D) — remove the source and register the cooked
	/// item in the world table in ONE transition, then broadcast the complete
	/// cooked-item state as one host→guest ItemCook event (never a decomposed
	/// ItemDestroy + ItemSpawn pair). The host's scene already ran the native
	/// conversion; the adapter calls this only from the verified patch postfix.
	/// </summary>
	void SendItemCooked(ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity);

	/// <summary>
	/// Composition seam: register an owner of DEFERRED creation reports. The item
	/// domain settles every registered source before it reports an operation, so a
	/// creation report always precedes an operation on the same item — the host
	/// judges the creation first and never has to wait for it (see
	/// <see cref="PendingItemCreations"/>).
	/// </summary>
	void RegisterPendingCreationSource(IPendingItemCreationSource source);

	// ===== Receive side (packet handlers surface the wire here) =====

	/// <summary>Host/solo: record the drops of a LOCALLY broken block into the authoritative table (the report travels inside BlockDamagedMsg — never a standalone spawn report).</summary>
	void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops);

	/// <summary>A break with drops was applied — register (host only) and materialize every drop.</summary>
	void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops);

	/// <summary>Host/solo: record the building-death drops caused by a LOCALLY broken support block into the authoritative table (they travel inside the same BlockDamagedMsg as the break).</summary>
	void RegisterBuildingDrops(IReadOnlyList<TrapDropEntryMsg> drops);

	/// <summary>A break with building-death drops was applied — register (host only) and materialize every drop with its full transient initial state.</summary>
	void FireBuildingDropsReceived(ulong sender, IReadOnlyList<TrapDropEntryMsg> drops);

	/// <summary>Host only: refuse a reported break's drops (the break was already applied — first-writer-wins) — the reporter destroys its local drops.</summary>
	void SendItemReject(ulong targetSteamId, ulong itemId, ItemRejectMsg.Reason reason);

	/// <summary>Guest side: the authoritative world-item snapshot arrived — reconcile locally. LayerModifierIndex/LayerModifierRandomState ride along (the world's current modifier and its decision's random start, -1/null = none).</summary>
	void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState);

	/// <summary>Guest side: the host's generation-time item snapshot arrived — bind local copies to the host's ids, materialize the host's version, destroy host-unknown locals. LayerModifierIndex/LayerModifierRandomState ride along (the host's rolled layer modifier and its decision's random start, -1/null = none).</summary>
	void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState);

	/// <summary>Guest side: the host's physics moved items — surface them for the local follow.</summary>
	void FireItemMoveReceived(IReadOnlyList<WireItemMoveEntry> items);

	/// <summary>Guest only: an item-instance id was allocated locally — report the counter high-water mark so the host can grant it back on a reconnect (a crashed-and-rejoined counter restarts from zero and would reuse ids the host still holds).</summary>
	void SendItemIdWatermark(ulong counter);

	/// <summary>
	/// Both sides: report this client's carried inventory with the ids it holds. A GUEST sends the
	/// absolute statement the host registers in its transfer table, and every call spends one step of
	/// the registration window (an empty capture spends one too — sync-coverage row I8). A HOST writes
	/// the same set into its OWN authority, once per world entry: it is the authority for its items,
	/// so the rows (and the members' view of those ids) are its own local write, and a host item the
	/// player has not used, moved or dropped yet would otherwise have neither a row nor an id.
	/// </summary>
	void SendCarriedInventory(IReadOnlyList<CharacterItemMsg> items);

	/// <summary>
	/// Both sides: open the carried-registration opportunity. A GUEST opens the report window — the
	/// local generation finished, or the host granted the id watermark (the join/reconnect signal) —
	/// and the pump reports the current carried set as soon as the cadence is due. A HOST owes one
	/// registration at the same edges (its generation, or its session appearing on a world it is
	/// already in).
	/// </summary>
	void ArmCarriedInventoryRegistration(string reason);

	/// <summary>Whether the pump should capture this client's carried set now — the guest's report cadence, or the host's unspent one-shot. False outside an active session.</summary>
	bool IsCarriedInventoryRegistrationDue();

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

	/// <summary>Host only: correct every OTHER member's copy of a used world item (drinking from a ground canister, #194) — the user's own copy IS the fact, every peer's copy adopts it via the standard correction path.</summary>
	void SendWorldItemCorrection(ulong exceptSteamId, CharacterItemMsg item);

	/// <summary>Host only: the items a guest currently owns (the transfer table — where the host moved world-table entries as the guest's actions took them). The reconnect restore merges these into the character snapshot.</summary>
	IReadOnlyList<WorldItem> GetTransferredItems(ulong steamId);

	/// <summary>Host only: adopt a carried item into a guest's transfer table (a cross-player transfer result — the recipient now owns it for use/slot/drop arbitration and reconnect restore).</summary>
	void AdoptTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item);

	/// <summary>
	/// Host only: record an item in a guest's transfer table when the kernel has ALREADY
	/// placed it there — the removal family's hand-over (the applied row was relocated to
	/// the operator, and the table row is the reconnect/arbitration record). Unlike
	/// <see cref="AdoptTransferredItem"/> it issues no kernel command.
	/// </summary>
	void RegisterTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item);

	/// <summary>Host only: remove a carried item from a guest's transfer table (a cross-player transfer result — the source no longer owns it).</summary>
	void RemoveTransferredItem(ulong guest, ulong itemId);

	/// <summary>Host only: replace a carried item's authoritative state in a guest's transfer table (a cross-player heal consumes part of the item's condition; the restore merge must not resurrect the consumed amount).</summary>
	void UpdateTransferredItem(ulong guest, ulong itemId, CharacterItemMsg item);

	/// <summary>Host only: broadcast one carried item's authoritative fact (use/slot move/pickup) to every guest except its owner — the peers update the owner's fact table and re-render the clone immediately (reliable; the 1 Hz snapshot is the fallback).</summary>
	void SendItemCarriedSync(ulong ownerSteamId, CharacterItemMsg item);

	/// <summary>A guest's carried inventory with self-assigned ids arrived (its local generation finished) — the Game Adapter seeds the owner's fact table.</summary>
	event Action<ulong, IReadOnlyList<CharacterItemMsg>>? CarriedInventoryReceived;

	/// <summary>
	/// Host only: report a world-item impact presentation this side's native
	/// collision call just played (the <c>drop</c> clip, the landing block's step
	/// sound and the dust, or a plush's squeak), so every guest presents the same
	/// landing. A guest never sends: its world-item copies are the
	/// non-authoritative, suppressed half of the family, so the host's copy is the
	/// only source of an authoritative landing.
	/// </summary>
	void SendItemImpact(ItemImpactMsg msg);

	void FireItemImpactReceived(ulong sender, ItemImpactMsg msg);

	/// <summary>An authority's world-item impact arrived — the receiver replays the presentation on its own world.</summary>
	event Action<ulong, ItemImpactMsg>? ItemImpactReceived;

	/// <summary>Host only: the item's live state (position/velocity/rotation/condition) — the periodic keyframe must broadcast the CURRENT state, not the spawn-time one (stale positions yank settled items around; a stale condition re-aligns the peers' decay to the wrong value).</summary>
	void RefreshItemState(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition);

	/// <summary>
	/// Host only: periodically re-send the full table (unreliable) so physical
	/// and top-level state drift self-heals — the receiver aligns settled items
	/// on the next reconcile.
	/// </summary>
	void SendPeriodicItemSnapshot();

	/// <summary>Host only: a new world layer is generating — the table starts empty again. The generation a restore drives is the one exception: its restored item set IS the layer's table, so an armed reconcile keeps it.</summary>
	void ResetItems();

	/// <summary>Host only: the generation finished — register the generation-time items (host-assigned ids, ground + starting supplies) into the table and broadcast them as one snapshot (the guests bind their local copies or materialize the host's version). The current layer modifier (LayerModifierIndex) rides along. Suppressed while a restore's item reconcile is pending (see below): the restored ids are the truth, not freshly assigned ones.</summary>
	void PublishGeneratedItems(IReadOnlyList<WorldItem> entries);

	// ===== Restored world items (mid-run restore) =====
	// The "a restored set is waiting" flag lives on IRestoredWorldItemSource — the
	// world-entry gate reads the same member, so the gate and the reconcile can never
	// disagree about whether the set is still owed. This section is the reconcile side.

	/// <summary>Host/solo: the restored world items — the set the generation reconcile must land in the live world (empty when nothing is pending).</summary>
	IReadOnlyList<WorldItem> ReadRestoredWorldItems();

	/// <summary>
	/// Host/solo: the generation finished reconciling its objects against the
	/// restored set. <paramref name="applied"/> counts the entries that landed
	/// (bound to the regenerated object or materialized);
	/// <paramref name="refused"/> names the entries the live world did not take
	/// (empty = complete). The account rides the restore report (§6: no silent loss).
	/// </summary>
	void CompleteRestoredWorldItems(int applied, IReadOnlyList<string> refused);

	// CancelRestoredWorldItems — the release the save layer drives — is declared by the
	// port this surface implements (IRestoredWorldItemSource), beside the pending flag
	// it pairs with: the reconcile side adds no second declaration of it.

	/// <summary>Host side: the world's current layer modifier (index into the game's LayerModifier.availableModifiers, -1 = none) — rides the world-item snapshots so a world entry outside a generation still receives it. A projection of world state: the adapter refreshes it when a generation finishes.</summary>
	int LayerModifierIndex { get; set; }

	/// <summary>Host side: the layer modifier decision's random-state seed (rides the world-item snapshots for deterministic cross-side replay).</summary>
	byte[]? LayerModifierRandomState { get; set; }

	/// <summary>Host only: broadcast the moving world items' authoritative positions (unreliable — the host's physics is the position authority, the guests follow).</summary>
	void SendItemMove(IReadOnlyList<WireItemMoveEntry> items);

	// ===== Application events (the adapter applies these) =====

	/// <summary>An item now exists in the world — materialize it (spawn from the carried state).</summary>
	event Action<WorldItem>? ItemSpawned;

	/// <summary>An item left the world into someone's inventory — remove it (or roll a local pickup back).</summary>
	event Action<ulong>? ItemPickedUp;

	/// <summary>An item now lies in the world at Pos (or inside the container item ParentItemId) — move an existing object there or materialize it (Vel = drop-moment velocity, a throw's flight; ParentPos = the container's position when it needs binding).</summary>
	event Action<ulong, CharacterItemMsg, NetVector2, NetVector2, ulong, float, float, NetVector2>? ItemDropped;

	/// <summary>An item was destroyed — remove it locally. A WORLD fact: a carried row is retired through <see cref="CarriedItemRetired"/> instead (a remote destroy must never kill a carried item's object).</summary>
	event Action<ulong>? ItemDestroyed;

	/// <summary>
	/// The authority says ONE owner's carried row no longer exists (a craft report's <c>Destroyed</c> entry
	/// applied by the host or by the relay; the id was attributed to that owner by the host's transfer table or
	/// its kernel row) — the adapter retires the row from THAT owner's clone fact table, so the next character
	/// snapshot finds it already gone instead of warning that it left without an event. Never a scene action:
	/// the object is on the owner's side, not this one.
	/// </summary>
	event Action<ulong, ulong>? CarriedItemRetired; // (owner SteamId, item instance id)

	/// <summary>The host's heater conversion arrived — kill the raw-meat copy and materialize the cooked steak atomically.</summary>
	event Action<ulong, WorldItem>? ItemCookedReceived;

	/// <summary>Guest side: the host's physics moved items — follow (apply the authoritative positions).</summary>
	event Action<IReadOnlyList<WireItemMoveEntry>>? ItemMoveReceived;

	/// <summary>The host refused an item arbitration — roll back (UnknownItem = pickup, BlockAlreadyBroken = block-break drops to destroy).</summary>
	event Action<ulong, ItemRejectMsg.Reason>? ItemRejected;

	/// <summary>The authoritative snapshot arrived — reconcile the local world items against it (the layer modifier + its random start ride along).</summary>
	event Action<IReadOnlyList<WorldItem>, int, byte[]?>? ItemSnapshotReceived;

	/// <summary>The host's generation-time item snapshot arrived — the adapter binds local copies / materializes / destroys the host-unknown ones (the layer modifier + its random start ride along).</summary>
	event Action<IReadOnlyList<WorldItem>, int, byte[]?>? WorldItemsSnapshotReceived;

	/// <summary>Guest side: the host's authoritative item state arrived (our action-report evidence diverged) — the adapter applies it (materialize missing contents, fix state, fix slot).</summary>
	event Action<CharacterItemMsg>? ItemCorrectionReceived;

	/// <summary>The authoritative fact of one carried item changed (host broadcast: use/slot move/pickup) — the adapter updates the owner's per-player fact table and re-renders the clone. Fired on the guests from the wire and on the host directly (its own arbitration decisions).</summary>
	event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived;

	/// <summary>Guest side: the host granted the id counter high-water mark (join/reconnect) — the adapter resumes the allocator from counter + 1.</summary>
	event Action<ulong>? ItemIdWatermarkReceived;
}
