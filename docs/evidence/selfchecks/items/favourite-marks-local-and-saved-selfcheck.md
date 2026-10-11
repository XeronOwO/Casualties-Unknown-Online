# A favourite mark is local and saved — self-check (2026-10-11)

Ticket: `docs/backlog/done/favourite-marks-are-local-and-saved.md`. Design record:
`docs/architecture/remote-inventory-native-parity.md` §3.2.7–§3.2.9, §3.3, §3.6, §6. Decision: 258
(amending decision 220's favourite paragraph). Source: the user ruling of 2026-10-11, quoted verbatim
in the ticket, which came out of acceptance batch `20261011-d` ("row 7's favourite half could not be
driven"). No protocol bump: decision 241 freezes the number pre-release and the handshake stays the
compatibility boundary.

## What landed (our own paths plus quoted text; the decompiled tree keeps line numbers)

| # | Mechanism | Where it lives | What it does |
|---|---|---|---|
| 1 | The mark set | `Runtime/Session/PlayerInteraction/LocalFavouriteMarks.cs`, "The local player's OWN favourite marks" | toggle, the lookup that drops a stale id, the spend that turns a mark into the item's own flag, the snapshot a capture reports and the replace-not-merge seed; holds no scene, so the rules are covered without a game |
| 2 | The record it rides | `Runtime/Protocol/Messages/CharacterFavouriteMarkMsg.cs` + `CharacterDataMsg.FavouriteMarks` (ProtoMember 10) | the mark is (owner, instance id, definition) on the MARKING player's own character record — the only store CUO keeps per world (`characters/<playerKey>.json`), so it is what makes a reconnect, a respawn and a continue bring the marks back and what keeps another world's marks out |
| 3 | The one writer of a proxy's field | `GameAdapter/Character/FavouriteMarkOverlay.cs` + `CloneInventoryRenderer.SetRemoteInventoryItemId` | a display proxy's `favourited` is the local mark and nothing else; the owner's value is no longer a projection source value (`RemoteItemPresentation.ApplySourceValues` writes condition and liquids only) |
| 4 | The gesture | `GameAdapter/Patches/PlayerCameraHandleWhileDraggingPatch.ApplyFavouriteStores` | the native condition still decides whether a store happens (the frame's candidate buttons are compared across the bracket); the proxy's field goes back, the LOCAL set toggles, and the overlay writes that answer so the star flips in the same frame; a proxy with no instance id is refused with one line |
| 5 | The item becoming the player's own | `GameAdapter/Character/FavouriteMarkAdoption.cs`, run from `LocalFavouriteMarkSync.Capture` (the seam every character report goes through) | the marked item's own `favourited` is set and the entry spent, on the ONE enumeration every path that can hand this player an item ends in — a pickup, a custody transfer, a craft product, a restored character all converge within the report that follows |
| 6 | Durability | `CharacterDataSync` (marks on every capture, seed on the restore's second pass, reset on session end and on every run entry through `RunSaveCoordinator.BeginRun`) | the marks come back with the character record and leave with the session OR the run — the run-entry reset runs for every role, because a guest that stays connected while the host starts a new world never sees a session end and would otherwise report the previous world's marks into the new one |
| 7 | The old path, deleted | `RemoteInventoryIntentKind.ToggleFavourite`, `RemoteDragIntentCapture.CaptureFavourite`, `RemoteIntentApplier.ApplyToggleFavourite`, the host's validation arm | the gesture is no longer one of the operation kinds that reaches an owner; `GiveToTrader` keeps its number 17 and the enum keeps the hole at 16, because renumbering an unrelated kind in a favourites change would rewrite its wire identity for nothing |
| 8 | The rule, gated | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/FavouriteMarkWriterGateTests.cs` | the display domain's `favourited` writers are an explicit, reasoned set; a fourth one fails the gate, and the gate also asserts the two seams that must use the writer still do |

## The four design points the ticket left to this cycle, settled

| Question | Answer | Evidence |
|---|---|---|
| What identifies a marked item across a save? | CUO's authoritative instance id, plus the owner and the definition the item carried when it was marked. The id survives because the archive stores it (`CharacterItemMsg.InstanceId` in `characters/<playerKey>.json`, `items.json` rows) and the restore re-stamps it onto the rebuilt object (`ItemStateCodec.RestoreItem`, `WearableRestorer`, `RemoteItemSceneOps`) — the same property every id-keyed seam in CUO already rests on (the container window's re-bind, the intent operands, the position follow). Acceptance batch `20261001-o` read it back on three clients ("every client's local tree held its own marker with the **same instance id**"). The owner and the definition are the guard: a mark whose id now names another item is DROPPED. | `docs/evidence/acceptance/save-multiplayer-restore-and-backups-20261001-o.md`; `LocalFavouriteMarksTests.AMarkWhoseIdNowNamesAnotherItem_IsDroppedInsteadOfApplied` |
| Where do the marks live so they are saved? | On the marking player's OWN character record. Named consumer: that player's own client — the overlay reads it and the restore seeds the live set from it; the host stores it exactly as it stores the rest of the character record and interprets none of it. The alternative (a client-local file beside the plugin) was rejected because it cannot tell two worlds apart, which is precisely what requirement 7 forbids: ids are `(counter << 32) \| SteamId` and the counter restarts with the process, so a different world can hand the same id, the same owner and the same definition to a different item. | `docs/architecture/save-archive-format.md` §3.4 ("`characters/<playerKey>.json` holds one player character per file … the ONLY persistent copy of a character"); `LocalFavouriteMarksTests.SeedReplacesTheWholeSet` |
| What happens to a mark whose item is destroyed, dropped or leaves the world? | It is never applied to another item; it is dropped when the id is observed to carry an item of another DEFINITION, or when the item becomes this player's own. The mark is on the ITEM, so it FOLLOWS the item: a hand-off to another player refreshes the recorded owner (logged once) instead of dropping the mark, which is what keeps requirement 3 true for an item that passed through a third player's hands. It deliberately survives a drop-and-repickup. Pruning on mere absence is refused on purpose — a character snapshot losing an id cannot tell "destroyed" from "put into a container lying on the ground". | `docs/architecture/remote-inventory-native-parity.md` §3.2.9; `LocalFavouriteMarksTests.AMarkFollowsTheItemToItsNewOwner`, `...AMarkWhoseIdNowNamesAnotherDefinition_IsDroppedInsteadOfApplied` |
| The third peer's view | Nothing about the owner's item changes on any client: the gesture writes one local set, sends nothing, and the owner's own `favourited` is not applied to any proxy. | `FavouriteMarkWriterGateTests.OnlyTheNamedWriters_TouchAFavouriteFieldInTheDisplayDomain`; acceptance row 1 |

## Red-then-green record

`SourceValues_CarryConditionAndLiquidsButNeverTheOwnersFavourite` was run against the OLD
`RemoteItemPresentation.SourceValues` (the record that still carried `Favourited`, i.e. the shape that
put the owner's answer on the operator's star): **1 failed, 0 passed**. With the record reduced to
condition and liquids it passes. This is a behaviour change rather than a defect fix, so no other red
was owed; the two rules that are STRUCTURAL are pinned by the gate instead, both of them as source
shape: no `.favourited` write outside the reasoned writer set, and no favourite member in
`RemoteInventoryIntentKind` (the vocabulary the gesture must never return to). A missing type would
have been a compile error rather than a red.

## Verification limits (recorded, not claimed)

- Whether the star is drawn correctly, whether the owner's item stays untouched on the owner's own
  screen and on a third peer, and whether the marks really come back after a rejoin are REAL-MACHINE
  facts: the seams are Harmony-bound to the game's own `Item`/`InvButton` and to a live
  `CharacterDataSync`, which no test host can materialize. The ticket's seven acceptance rows are the
  judgement, run by the agent's batch on the real machine; nothing here claims them. The archive half of
  row 3 is machine-covered (`WorldCharacterFavouriteMarkTests`), the rejoin half of row 2 is not.
- **An item that has left an inventory shows what the game itself shows.** The native tooltip builder
  draws the favourite sprite for whatever item the cursor is over — `PlayerCamera.ItemHoverDescription`
  is static and appends `<sprite index=22>` on `item.favourited` (`PlayerCamera.cs:1313-1316`), while
  `InvButton`'s tint is the inventory's own (`InvButton.cs:150`). So a world item keeps carrying and
  drawing its own flag exactly as the native game does; the ruling changed who owns a MARK and what the
  backpack view shows, not what an item says about itself, and there is no conflict with requirement 3
  (the mark is still this player's when the item reaches them, and from then on it IS the item's flag).
- The mark's identity rests on the item instance id. "No other item inherits it" is therefore only as
  strong as the id: the guard is the item's DEFINITION (the owner follows the item), and it cannot
  separate two items that share an id and a definition. The id-reuse path is filed as
  `docs/backlog/todo/item-id-counter-is-not-resumed-by-a-restore.md` rather than fixed here.
- The `.favourited` writer census reads the display domain directory; a display path built OUTSIDE
  `src/CasualtiesUnknownOnline.GameAdapter/Character` would land beside the rule rather than inside it.
  The gate's own doc says so.
