# A favourite mark belongs to the local player, and it is saved

- Status: Review — implemented, tested, documented and independently reviewed on 2026-10-11. The
  seven acceptance rows below are judged by the agent-run batch on the real machine (three clients);
  the code facts they rest on are named in the self-check.
- Priority: High
- Category: Remote inventory / local preference / persistence
- Source: **User ruling, 2026-10-11** (in reply to the batch `20261011-d` finding that row 7's
  favourite half could not be driven): "收藏是本地当前玩家的行为,不应该是对远程玩家施加的行为,这样
  才符合逻辑" and "这个收藏应当支持存档功能,就不需要每次玩家加入游戏都重新收藏一遍". The same ruling answered
  the question "what does an item the local player never marked show?" — the mark is a local PERSONAL
  behaviour, so a remote item does not wear the owner's mark in the local player's view.
- Supersedes: decision 220's favourite paragraph — "CUO only turns the store it observes into
  `ToggleFavourite` … so the operator's star lights up from the owner's answer"
  (`docs/decisions/active.md`), now amended in place — and the two places that judged the old
  behaviour: `docs/backlog/todo/remote-inventory-native-parity-rework.md` matrix row 7 and
  `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md` §3 row 7.
  The numbered decision entry for the new rule is 258.
- Related: `todo/remote-inventory-native-parity-rework.md` (the family this sits in, and the batch that
  produced the finding), `docs/architecture/remote-inventory-native-parity.md` §3.2.7–§3.2.9 (the
  while-dragging frame and the new rule), `docs/evidence/selfchecks/items/favourite-marks-local-and-saved-selfcheck.md`
  (the cycle's own record), `review/save-native-character-field-parity.md` and
  `review/save-multiplayer-restore-and-backups.md` (the save surfaces a local mark rides)

## Required behaviour (settled by the ruling)

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
   shows.
5. **Nothing else changes**: a favourite pressed on the local player's OWN inventory keeps the game's own
   behaviour, and the owner's own favourite state — theirs to set — is untouched by any of this.

## Landed

| # | What the requirement needed | Where it lives |
|---|---|---|
| 1 | the mark set itself, with the guard that drops a stale id | `Runtime/Session/PlayerInteraction/LocalFavouriteMarks.cs` |
| 2 | the mark riding the MARKING player's own character record, for durability | `CharacterFavouriteMarkMsg` + `CharacterDataMsg.FavouriteMarks` (ProtoMember 10), captured by `LocalFavouriteMarkSync.Capture` (the seam `CharacterDataSync` calls on every report), seeded on the restore's second pass |
| 3 | one writer for a display proxy's star, and the owner's value out of the projection | `GameAdapter/Character/FavouriteMarkOverlay.cs`; `RemoteItemPresentation.ApplySourceValues` no longer writes `favourited` |
| 4 | the gesture writing the local set instead of an intent | `PlayerCameraHandleWhileDraggingPatch.ApplyFavouriteStores` |
| 5 | the mark becoming the item's own flag | `GameAdapter/Character/FavouriteMarkAdoption.cs`, run from the one capture every item-gaining path ends in |
| 6 | the old path deleted, not shadowed | `RemoteInventoryIntentKind.ToggleFavourite`, `RemoteDragIntentCapture.CaptureFavourite`, `RemoteIntentApplier.ApplyToggleFavourite` and the host's validation arm are gone; the enum keeps the hole at 16 |
| 7 | the rule under a failing gate | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FavouriteMarkWriterGateTests.cs` |

## Questions the ticket left to the cycle, and their answers

- **What identifies a marked item across a save?** CUO's authoritative instance id, plus the owner and
  definition the item carried when it was marked. The archive stores the id and the restore re-stamps it
  onto the rebuilt object, which is the property every id-keyed seam in CUO already rests on. A mark
  whose id now names another owner or another definition is DROPPED with one line. (The counter that
  mints ids is NOT resumed by a restore — a pre-existing property this ticket depends on and does not
  change; it is filed as `todo/item-id-counter-is-not-resumed-by-a-restore.md`.)
- **Where do the marks live so they are saved?** On the marking player's own character record. That is
  the only store CUO keeps per world (`characters/<playerKey>.json`), so it is what makes the marks come
  back with the player and what keeps another world's marks out. A client-local file beside the plugin
  was rejected for the second half: ids restart with the process, so a different world can hand the same
  id, owner and definition to a different item, and a stale key must never be applied.
- **What happens to a mark whose item is destroyed, dropped or leaves the world?** It is never applied to
  another item; it is dropped when the id is observed to name a different item, or when the item becomes
  this player's own. It deliberately survives a drop-and-repickup, because the item keeps its id and
  requirement 3 then applies. Pruning on mere absence is refused on purpose: a character snapshot losing
  an id cannot tell "destroyed" from "put into a container on the ground".
  - Two consequences of that guard, both deliberate and both recorded rather than discovered later: a
    mark does NOT follow an item that changes hands to a THIRD player (the owner is part of the guard, so
    the mark is dropped and the player marks it again — the safe side of the trade, and the item that
    comes to THIS player is unaffected because the adoption matches on the definition), and "no other
    item inherits it" is only as strong as the instance id itself: if an id is ever re-issued to a
    different item with the SAME owner and the SAME definition, the guard cannot tell them apart. The
    id-reuse path is `todo/item-id-counter-is-not-resumed-by-a-restore.md`, which this ticket depends on
    and does not fix.
- **The third peer's view changes in no way** — nothing about the owner's item changes on any client.

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Operator marks an item in the owner's backpack | The operator's own view shows the mark; the OWNER's own item is unchanged on the owner's client and on the third peer, and no intent for it reaches the owner | code fact; real-machine judgement pending |
| 2 | The same mark, after the operator leaves and rejoins the same world | Still there, without pressing the key again | code fact (the record is handed back with the character, and `LocalFavouriteMarks.Seed` replaces the set from it); real-machine judgement pending |
| 3 | The same mark, after a save and a load of that world | Still there | covered by `WorldCharacterFavouriteMarkTests.LayerEndCutThenContinue_RoundTripsTheLocalCharactersFavouriteMarks` (the archive round trip); the rejoin/continue on the real machine is still pending |
| 4 | The marked item is later picked up / transferred to the operator | The item is favourited as the operator's own from that moment, and the local mark entry is gone (no double state) | code fact; real-machine judgement pending |
| 5 | An item in the owner's backpack that the operator never marked, where the OWNER has favourited it | No mark is shown in the operator's view (the mark is personal) | code fact for the inventory view (`RemoteItemPresentation.ApplySourceValues` no longer carries the owner's value at all); the world-item case is the scope limit below; real-machine judgement pending |
| 6 | The favourite key on the operator's OWN inventory | Unchanged native behaviour | code fact; real-machine judgement pending |
| 7 | A mark whose item is destroyed, or belongs to a different world | Dropped, and no other item inherits it | code fact; real-machine judgement pending |

## Limits recorded with the landing

- **Requirement 4's SCOPE, stated because the ruling does not decide it.** The rule as landed — and as the
  acceptance rows judge it — governs what this client shows for items in ANOTHER PLAYER'S INVENTORY, which
  is the clone display path the batch finding came from. An item that has left an inventory (dropped on
  the ground, or inside a container lying in the world) still shows the flag the ITEM carries, whoever set
  it: `RemoteItemSceneOps` / `ItemReconcile` / `ItemStateCodec.RestoreContents` write `favourited` from the
  authoritative item state, and the game's own hover text (`PlayerCamera.cs:1313`) and inventory buttons
  (`InvButton.cs:150`) draw it. So a player who drops an item they favourited still shows that star to
  whoever hovers it, and a mark this player made stops being shown once its item leaves the inventory it
  was made in (the mark itself survives; see the hand-off consequence above). Extending the ruling to "no
  flag any other player set is ever drawn for me" is a behaviour change on a surface the user has not
  ruled on, and it is left as an OPEN QUESTION rather than silently decided here.
- **The mark's identity rests on the item instance id**, which the archive stores and the restore
  re-stamps. The allocator's counter is not resumed by a restore, which is the sibling ticket named above;
  the guard cannot separate two items that share an id, an owner and a definition.
- The item's own sound and the operating feel of the gesture remain the family's open acceptance items
  (`docs/architecture/remote-inventory-native-parity.md` §6); this ticket changes none of them.
- The mark set is bounded by what the player marks by hand; it is not pruned by age. A mark whose item
  never comes back is inert, and the record it rides is replaced whenever the character comes back.
- The writer census of the gate reads the display domain directory; a display path built outside it would
  land beside the rule.
