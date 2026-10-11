# A favourite mark belongs to the local player, and it is saved

- Status: Todo
- Priority: High
- Category: Remote inventory / local preference / persistence
- Source: **User ruling, 2026-10-11** (this session, in reply to the batch `20261011-d` finding that
  row 7's favourite half could not be driven): "收藏是本地当前玩家的行为,不应该是对远程玩家施加的行为,这样
  才符合逻辑" and "这个收藏应当支持存档功能,就不需要每次玩家加入游戏都重新收藏一遍". The same ruling answered
  the question "what does an item the local player never marked show?" — the mark is a local PERSONAL
  behaviour, so a remote item does not wear the owner's mark in the local player's view.
- Supersedes: decision 220's favourite paragraph — "CUO only turns the store it observes into
  `ToggleFavourite` … so the operator's star lights up from the owner's answer"
  (`docs/decisions/active.md`) — and the two places that judge the old behaviour:
  `docs/backlog/todo/remote-inventory-native-parity-rework.md` matrix row 7 ("the favourite star follows
  the owner's own value") and `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`
  §3 row 7. Those texts are corrected to point here; the numbered decision entry for the new rule is
  written by the cycle that lands it.
- Related: `todo/remote-inventory-native-parity-rework.md` (the family this sits in, and the batch that
  produced the finding), `docs/architecture/remote-inventory-native-parity.md` §3.2 (the while-dragging
  frame the store is observed in), `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261011-d.md`
  (the reading that started this), `review/save-native-character-field-parity.md` and
  `review/save-multiplayer-restore-and-backups.md` (the save surfaces a local mark has to ride)

## Today's behaviour (evidence)

Pressing the favourite key over a display proxy is turned into an action ON THE OWNER'S ITEM:

1. the frame's own store is observed across CUO's while-dragging bracket
   (`GameAdapter/Patches/PlayerCameraHandleWhileDraggingPatch`, `CaptureFavouriteStores`) and the proxy's
   field is put back, so the operator's own copy shows nothing of its own;
2. the observed store becomes a `ToggleFavourite` intent, forwarded by the host to the item's owner;
3. the owner's client runs `RemoteIntentApplier.ApplyToggleFavourite`, whose own doc comment says it:
   "the owner flips its OWN item's field" — the owner's item really changes, and the new value then rides
   the item state every peer sees.

So a keystroke in someone else's backpack edits that person's item, and a third peer watches it happen.
The ruling says that is not what the gesture is.

## Required behaviour

1. **The mark is local and personal.** A favourite gesture made while looking at another player's items
   NEVER changes an item that player owns, on their client or on any other peer, and no wire message
   carries it as an action on their item. One mutation site stays: the item's owner mutates their own
   items, and this gesture stops being one of the operation kinds that reaches them.
2. **The local player's marks are remembered**, so a returning player does not re-mark everything: they
   survive a save and a load, and a rejoin of the same world. (The player's OWN items already carry
   `favourited` natively and are saved with them — the new part is the mark on an item the local player
   does not own.)
3. **A mark becomes real when the item does.** If a marked item later comes into the local player's own
   possession (pickup, a custody move), the mark becomes that item's own `favourited` state from then on:
   it is that item's fact, it rides the item's own state and the save like any other item field, and the
   local mark entry for it is dropped.
4. **The local view shows the local player's marks.** An item the local player has not marked carries no
   mark in their view; the owner's own favourite state is the owner's business and is not what this view
   shows. (Ruling above; the words are quoted in *Source* so a later reader sees the basis rather than a
   paraphrase.)
5. **Nothing else changes**: a favourite pressed on the local player's OWN inventory keeps the game's own
   behaviour, and the owner's own favourite state — theirs to set — is untouched by any of this.

## Questions the cycle must settle, with what is already known

- **What identifies a marked item across a save?** The cycle has to name the key before it can promise 2.
  Known: CUO's item instance ids are host-assigned and continued after a restore through
  `ItemIdAllocator` / `ItemIdWatermarkMsg` (`[IdWatermark] granted N to <peer>` on rejoin), while the
  game's own save shape for an item is `reversing/…/SavedItem.cs`. The cycle reads which of the two
  survives a save/load and a rejoin of the same world, and says in the ticket what a mark on an item of a
  DIFFERENT world does (a stale key must be dropped, never applied to whatever now holds that id).
- **Where do the marks live so they are saved?** The local player's own character data (which the world
  save already carries, so it comes back with them) against a client-local store beside the plugin. The
  choice decides whether the host can see the marks — naming the consumer each one serves, per the
  repository's mechanism rule.
- **What happens to a mark whose item is destroyed, dropped, or leaves the world?** Named and decided in
  the cycle rather than left to whatever the map happens to do.
- **The third peer's view changes in no way**, and the record's acceptance row says so.

## Deletion scope (no dual paths, pre-release policy)

The `ToggleFavourite` intent kind, the owner-side apply (`RemoteIntentApplier.ApplyToggleFavourite`) and
the host-forwarding entry for it are DELETED in the same change, not kept beside the new local mark: the
released-policy rule that compatibility is never a design input applies, and decision 241 freezes the
protocol number pre-release, so the handshake stays the compatibility boundary and the change is recorded
by its commit and this ticket. The frame bracket's favourite-candidate snapshot is re-pointed at the local
mark (it is still the only seam that sees the store happen) or deleted with it, whichever the cycle's
mechanism inventory says carries its weight.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Operator marks an item in the owner's backpack | The operator's own view shows the mark; the OWNER's own item is unchanged on the owner's client and on the third peer, and no intent for it reaches the owner |
| 2 | The same mark, after the operator leaves and rejoins the same world | Still there, without pressing the key again |
| 3 | The same mark, after a save and a load of that world | Still there |
| 4 | The marked item is later picked up / transferred to the operator | The item is favourited as the operator's own from that moment, and the local mark entry is gone (no double state) |
| 5 | An item in the owner's backpack that the operator never marked, where the OWNER has favourited it | No mark is shown in the operator's view (the mark is personal) |
| 6 | The favourite key on the operator's OWN inventory | Unchanged native behaviour |
| 7 | A mark whose item is destroyed, or belongs to a different world | Dropped, and no other item inherits it |
