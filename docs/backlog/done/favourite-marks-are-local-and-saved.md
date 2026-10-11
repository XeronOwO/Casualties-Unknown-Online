# A favourite mark belongs to the local player, and it is saved

- Status: Done — batch `20261011-f` accepted the three rows batch `20261011-e` left `unproven` (row 1's
  **gesture** half, row 4 and row 6): the mark was made by a real press over the owner's item, it became
  the item's own flag once the item changed hands, and the same key over the player's OWN inventory kept
  the game's own behaviour. Every row of the matrix now carries a verdict.
- Acceptance records: `docs/evidence/acceptance/favourite-marks-are-local-and-saved-20261011-f.md` (row 1's
  gesture half, row 4 and row 6) and
  `docs/evidence/acceptance/favourite-marks-are-local-and-saved-20261011-e.md` (row 1's mark-exists halves,
  rows 2, 3, 5 and 7).
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
  behaviour: `docs/backlog/done/remote-inventory-native-parity-rework.md` matrix row 7 and
  `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md` §3 row 7.
  The numbered decision entry for the new rule is 258.
- Related: `done/remote-inventory-native-parity-rework.md` (the family this sits in, and the batch that
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
  change; it is filed as `review/item-id-counter-is-not-resumed-by-a-restore.md`.)
- **Where do the marks live so they are saved?** On the marking player's own character record. That is
  the only store CUO keeps per world (`characters/<playerKey>.json`), so it is what makes the marks come
  back with the player and what keeps another world's marks out. A client-local file beside the plugin
  was rejected for the second half: ids restart with the process, so a different world can hand the same
  id, owner and definition to a different item, and a stale key must never be applied.
- **What happens to a mark whose item is destroyed, dropped or leaves the world?** It is never applied to
  another item; it is dropped when the id is observed to carry an item of another DEFINITION, or when the
  item becomes this player's own. It deliberately survives a drop-and-repickup, because the item keeps its
  id and requirement 3 then applies.
  - **The mark is on the ITEM, so it follows the item.** An item handed from one player to another is
    still the item this player starred, so the recorded owner is context the mark refreshes rather than a
    second identity check; the alternative (dropping on an owner change) would unmark an item that passed
    through a third player's hands and then reached this player, which contradicts requirement 3. The
    reading is logged once per hand-off.
  - **Pruning on mere absence is refused on purpose**: a character snapshot losing an id cannot tell
    "destroyed" from "put into a container on the ground".
  - **"No other item inherits it" is only as strong as the instance id itself**: if an id is ever re-issued
    to a different item of the SAME definition, the guard cannot tell them apart. The id-reuse path is
    `review/item-id-counter-is-not-resumed-by-a-restore.md`, which this ticket depends on and does not fix.
- **The third peer's view changes in no way** — nothing about the owner's item changes on any client.

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | Operator marks an item in the owner's backpack | The operator's own view shows the mark; the OWNER's own item is unchanged on the owner's client and on the third peer, and no intent for it reaches the owner | the **gesture** half by batch `20261011-f` — a real press with the owner's item button read under the pointer, `[FavouriteMark] marked paincream in <owner>'s inventory` at 16:13:36 — and the mark-exists halves by batch `20261011-e` (created by the declared fixture there) |
| 2 | The same mark, after the operator leaves and rejoins the same world | Still there, without pressing the key again | **pass** (batch `20261011-e`: `Character restore brought back 1 local favourite mark(s).` after `leave-world` → `home.leave` → `join-lobby`) |
| 3 | The same mark, after a save and a load of that world | Still there | **pass** (batch `20261011-e`: the same restore line after the host's cut + continue, and the world's own `live/characters/<playerKey>.json` carries the mark) |
| 4 | The marked item is later picked up / transferred to the operator | The item is favourited as the operator's own from that moment, and the local mark entry is gone (no double state) | **pass** (batch `20261011-f`: the operator took the item through the game's own pickup with the item's instance id kept, the item's own flag then read `favourited=true` and the local entry was gone) — batch `20261011-e` could not move it, because that operator's limbs sat at zero force |
| 5 | An item in the owner's backpack that the operator never marked, where the OWNER has favourited it | No mark is shown in the operator's view (the mark is personal) | **pass** (batch `20261011-e`: the owner's own census reads `favourited=true`, the operator's proxy of the same item reads `false`) — the owner's own half was staged as a declared substitution |
| 6 | The favourite key on the operator's OWN inventory | Unchanged native behaviour | **pass** (batch `20261011-f`: with the pointer over the operator's own item button the press flipped that item's own native flag, and the session's whole mark log holds no line for it) |
| 7 | A mark whose item is destroyed, or belongs to a different world | Dropped, and no other item inherits it | **pass** for both staged halves (batch `20261011-e`: `dropped a stale mark: mark on item … names a bandage, but the id now carries a paincream.` with the entry gone, and `mark on item … followed it from … to …` with the owner refreshed) — the hand-off is staged by its recorded owner, not by a real transfer |


## Limits recorded with the landing

- **An item that has left an inventory shows the flag the ITEM carries, which is what the game itself
  does.** The native tooltip builder draws the favourite sprite for whatever item the cursor is over —
  `PlayerCamera.ItemHoverDescription` is static and appends `<sprite index=22>` on `item.favourited`
  (`PlayerCamera.cs:1313-1316`) — while the inventory button's tint is the inventory's own
  (`InvButton.cs:150`). CUO therefore keeps syncing a world item's own `favourited` with the rest of its
  state (`RemoteItemSceneOps`, `ItemReconcile`, `ItemStateCodec.RestoreContents`) and keeps drawing it,
  exactly as the native game does; the ruling changed who OWNS a mark and what the backpack view shows,
  not what an item says about itself. Nothing here conflicts with requirement 3: the mark this player made
  is still theirs when the item reaches them, and from then on it IS the item's own flag.
- **The mark's identity rests on the item instance id**, which the archive stores and the restore
  re-stamps. The allocator's counter is not resumed by a restore, which is the sibling ticket named above;
  the guard cannot separate two items that share an id and a definition.
- The item's own sound and the operating feel of the gesture remain the family's open acceptance items
  (`docs/architecture/remote-inventory-native-parity.md` §6); this ticket changes none of them.
- The mark set is bounded by what the player marks by hand; it is not pruned by age. A mark whose item
  never comes back is inert, and the record it rides is replaced whenever the character comes back.
- The writer census of the gate reads the display domain directory; a display path built outside it would
  land beside the rule.
