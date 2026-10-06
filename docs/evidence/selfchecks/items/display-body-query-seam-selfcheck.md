# Display-Body Query Seam — Self-Check (2026-10-06)

Delivery fact sheet for `docs/backlog/todo/remote-inventory-native-parity-rework.md` matrix row 6's swap half,
the row batch `20261006-f` lost. That batch drove the gesture on three clients (operator = physical-machine
host, owner = sandbox guest) and read the release aborting inside the game's own release body:
`m1-host-release-onto-occupied.json` carries `calls: TargetInvocationException…` with `dragAfter: "Item"` (the
drag left staged) and no intent, and its diagnostic probe run (`p-f1-diag2.json`) named the throw —
`UnityException | Transform child out of bounds`, `Transform.GetChild` ← `Body.GetItem` ← `Body.SlotOf(Item)` ←
`PlayerCamera.TryPerformInventoryAction` ← `TryPerformUIActions` ← `HandleReleaseDragging`. Record:
`docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-f.md` row 6b.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | R8 is the branch a release onto an OCCUPIED slot of the displayed clone takes, and the throw happens while its own second argument is evaluated — before any CUO call seam could absorb the call. | `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:1614-1618` (`if (this.body.HoldingItem(invButton.slot) && this.body.HoldingItem(this.dragItem)) { this.body.SwapSlots(invButton.slot, this.body.SlotOf(this.dragItem)); …`) |
| 2 | `Body.SlotOf(item)` walks every slot through `GetItem(i)` and answers 0 when nothing matches. | `reversing/…/Body.cs:1333-1343` |
| 3 | `Body.GetItem(int)` guards on `HoldingItem(slot)` and then indexes the INSTANCE's own slot transform — the local body's, because the release branch calls it on `PlayerCamera.body`. | `reversing/…/Body.cs:1346-1353` (`if (this.HoldingItem(slot)) { return this.slots[slot].transform.GetChild(0).GetComponent<Item>(); }`) |
| 4 | The redirect answered the GUARD and not the body. Both `HoldingItem` overloads were postfixes, so the native body had already run (harmless there: `slots[slot].transform.childCount > 0`) and the postfix then replaced the answer with the displayed clone's; `GetItem`'s postfix ran only after its own body had thrown, so it answered nothing. | `Patches/RemoteDragPredicatePatches.cs` (pre-fix: four `Postfix` methods on `Body`), `Patches/InvButtonBodyPatch.cs` (the ring's buttons read the focused clone), `Runtime/Session/PlayerInteraction/RemoteDragIntentCapture.cs` (`ShouldAnswerFromDisplayBody`: an open bracket whose ring shows the owner's body) |
| 5 | The failing state was measured, not inferred: at the abort the host's LOCAL slots read `0 none, 1 none, 2 none, 3 emergencylight, 4 none, 5 none` while the same ring buttons read the clone's `slot 0 trashbag, slot 1 bandage, slot 3 emergencylight` — so `SlotOf(dragItem)` threw at `i = 0`, the first index the clone has occupied and the local body has not. | `.agent-local/acceptance-20261006-f-report.md` F1 (the diagnostic's own state read, outside any bracket; the artifact names are in the row above) |
| 6 | The loss is total and quiet, which is why it read as a dead gesture: nothing was captured (no intent), no refusal was recorded, and the dispatcher's own unclassified line is all that was written; the native body never reached `this.dragItem = null` (`:1496`), so the drag was never cleared — the ring closed after the abort with `camera.dragItem` still set, and the batch's probe cleared it before the next row. | `RemoteDragIntentDispatcher.Emit` (`IsUnclassified`), `reversing/…/PlayerCamera.cs:1456-1497` (the release body's own tail: `dragItem = null` after the branch), `.agent-local/acceptance-20261006-f-report.md` F1 (the ring's state and the probe) |
| 7 | The other three redirected queries are the same family, and two of them run before any CUO seam too: `HoldingItem(Item)` walks the instance's own slots, `HoldingItem(int)` indexes them, and `GetWearable(string)` walks the LOCAL body's limb transforms (whose items are the local player's) — all of them answered by the clone only after the local body had been read. | `reversing/…/Body.cs:1301-1311`, `:1327-1330`, `:1539-1553` |
| 8 | No other caller inside either bracket kind can be answered wrongly. `HandleReleaseDragging` (`:1456-1497`) calls `Body.DoPickupCheck`, `TryPerformUIActions`/`TryPerformWorldActions`, `OpenContainer`, `RepopulateContainer` and `UpdateWearables`; of those, `RepopulateContainer` reads the CONTAINER's own children (`:2824-2843`), and `UpdateWearables` runs with `PlayerCamera.body` temporarily swapped to the clone (`Patches/PlayerCameraUpdateWearablesPatch.cs`) and reads `this.body.GetAllWearables()` (`:2767-2784`) — a call on the clone, which the redirect's `answering == instance` guard leaves native. `HandleWhileDragging` (`:1713-1783`) reads the frame's own buttons' `GetItem()` (a call on the clone) and the drain tick's `DoPickupCheck`. Every `GetItem(handSlot)` site in `PlayerCamera` (e.g. `HandleAttacks`, `:1830`) is outside both brackets. | the same sources |
| 9 | Both bodies carry the same slot array, so the ring's own button index names a slot of either: the clone is `Object.Instantiate` of the scene's "Experiment" template, and `slots` is that template's serialized array. This is what bounds the range guard — an index the displayed body does not have is not a read about the ring, and keeps the native answer. | `Character/RemoteBodyFactory.cs` (`Object.Instantiate(template)`), `reversing/…/Body.cs:3977` (`public InventorySlot[] slots;`) |
| 10 | The swap the intent replays is the native R8 sequence on the OWNER's body, guarded by the owner's own slots; this cycle changes nothing on that side. | `RemoteIntentApplier.ApplySwapSlots` (`IsValidSlot` + `body.HoldingItem(item)` + `body.SwapSlots(targetSlot, body.SlotOf(item))`), `reversing/…/Body.cs:1413-1428` |

## 2. The change

- `RemoteDragPredicatePatches`' four `Body` query seams (`HoldingItem(Item)`, `HoldingItem(int)`,
  `GetItem(int)`, `GetWearable(string)`) are prefixes that assign the displayed body's own answer and return
  `false`, so the game's body never runs against state the redirect did not answer. `GetItem` is the row that
  threw; the other three are the same shape and are aligned with it rather than left as postfixes, because the
  invariant is what makes the family gateable — a postfix answers AFTER the native body has read the local body.
- The range guard (`RemoteDragPredicateView.NamesASlotOfTheRing`) is unchanged in meaning: a slot index the
  displayed body does not have keeps the native answer, and row 9 is why that is sound rather than a hole.
- The file's one `PlayerCamera.OpenContainer` seam is untouched: it patches an ACTION whose native body must run
  (CUO only tracks which container the game showed), so the rule's scope is the patched type — the bodies that
  index their own slots — and not the file.
- Deliberately NOT changed: the bracket, the capture seams, the intent vocabulary, the owner-side applier, and
  the host half. No wire change, so `ProtocolVersion.Current` is untouched.
- **This cycle's independent review found two adjacent shapes, and both are folded in here.**
  `BodyItemPatches.DropWearablePatch` now carries the display-proxy guard its two siblings always had
  (`PickUpItemPatch`, `DropItemPatch` — the same `GetComponentInParent<RemoteCloneRender>()` test): inside an
  open bracket its own `GetWearable` read is answered by the DISPLAYED body (`RemoteDragPredicatePatches`), so a
  dragged proxy can pass the check and be reported — and a display proxy is never a local drop, so the report
  would have handed another player's item to `ItemWorldSync.OnItemDropped`, which stamps it an id (the "extra
  item" family). The rule is one seam over from the release window's own report refusal (`SwapSlotsPatch`'s
  `DestinationIsOwnerBody` early return: "nothing was swapped locally and nothing may be reported here") and it
  is AGENTS.md convention 6 applied across patch classes. `PlayerCameraUpdateWearablesPatch` gained a finalizer
  that restores `camera.body` when `UpdateWearables` throws — that call sits INSIDE every release bracket
  (`PlayerCamera.cs:1494`, before the drag is cleared), a postfix does not run for a throwing body, and the
  sticky swap would leave `camera.body` on a clone, which is exactly the premise
  `RemoteDragPredicateView.AnsweringBody` tests (`instance == camera.body`) — so a throw there would silently
  stop this whole fix from answering. The finalizer restores the field and rethrows the same exception; it
  hardens state, it never swallows a failure.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| Every `Body` query seam in the file answers by skipping the native body, and the family cannot silently go back | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RemoteDragQuerySeamGateTests.cs` — `EveryBodyQuerySeam_AnswersBySkippingTheNativeBody` (census floor 5, one violation per postfix, per non-skipping prefix, per prefix that never leaves the native answer reachable, and per prefix that never consults the redirect) plus eight matcher samples. **Read RED on the pre-fix tree** with `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~RemoteDragQuerySeamGateTests"`: the assertion read `4 of 5 Body query seam(s) do not answer by skipping the native body —` `RemoteDragHoldingItemPatch`, `RemoteDragHoldingSlotPatch`, `RemoteDragGetItemPatch`, `RemoteDragGetWearablePatch` (it backticks each name), 1 failed / 6 passed; green after the change |
| The native body can no longer run behind a redirected guard, at the exact frame that threw | the throw site is `Body.GetItem`'s index (`reversing/…/Body.cs:1346-1353`), and the prefix returns before that body for exactly the slots the displayed body answers — so the `i = 0` read of row 5 answers the clone's `trashbag`, the walk continues to the dragged proxy, and `SwapSlots` is reached. Source-shaped here, runtime-read by the row below |
| The runtime row: the same gesture produces the intent instead of the exception | the next three-client batch re-drives F1's own recipe (`remote-gesture mode=release,item=13787924117,cast=<occupied slot>,moved=1`) and must read the operator's `[RemoteIntent] SwapSlots captured for item … (… slot …)` and the owner's replay (`[RemoteIntent] replayed native SwapSlots`, `[SlotMoved] …`) with no `UnityException … Transform child out of bounds` and no `produced no intent — unclassified native gesture` line. Named as the row this fix unlocks in `docs/backlog/todo/remote-inventory-native-parity-rework.md` |
| The owner's half is the native sequence on its own body, unchanged | `RemoteIntentApplier.ApplySwapSlots` (row 10's anchors); its unit pins stay green |
| The redirect cannot answer a query that is not the branch's own | row 8's call-surface read of both bracket bodies and their callees, and row 8's `UpdateWearables` body swap, which the `answering == instance` guard leaves native (`RemoteDragPredicateView.AnsweringBody`) |
| The review's two adjacent findings are pinned where they can be | the proxy guard takes the shape its siblings already use in the same file (`BodyItemPatches.PickUpItemPatch`/`DropItemPatch` test `GetComponentInParent<RemoteCloneRender>()`), and the release window's own report refusal is the same rule one seam over (`SwapSlotsPatch`'s `DestinationIsOwnerBody` early return); the body swap now declares `Prefix`/`Postfix`/`Finalizer`, the shape `PlayerCameraDragUsePatch` and `PinyinSearchPatches` already use. No gate pins the report-hook family yet — see the limits |

## 4. Limits

- **No session ran while writing this change.** The Unity facts it rests on are batch `20261006-f`'s own reads
  (row 5's local-versus-ring slot state, the throw's frame) plus `reversing/`; the fix's runtime proof is the
  next batch's re-drive of that recipe, and this cycle claims no green runtime row for it.
- **The gate pins WHERE the answer happens, not that the displayed body is the right one.** The matcher reads
  the shape (the answer skips the native body, the native path stays reachable, the redirect decision is
  consulted) and cannot read the CONDITION on the answering path: a future seam that consults the redirect and
  branches on the wrong side of it satisfies the gate. The right-body half is the acceptance run's, and the
  bracket's own scope (`ShouldAnswerFromDisplayBody`) is the runtime unit's.
- **The report-hook family has no gate yet.** This cycle aligned the one hook the review found
  (`DropWearablePatch`) beside its two guarded siblings, but "a report hook reached inside a display-proxy
  bracket must not report the proxy" is not machine-checked: deciding which hook can ever see a proxy needs
  per-hook reachability evidence this cycle did not produce, so it is named here rather than pinned by a gate
  written from a guess. The proxy release that would produce it is row 7's wearable half — `unproven`.
- **The finalizer's own state has never been reached.** No batch has made `UpdateWearables` throw; the change is
  strictly fail-safe (restore the field, rethrow the same exception) and is recorded because this fix's premise
  depends on that swap being restored rather than on the throw being likely.
- **The `NamesASlotOfTheRing` fall-through is argued, not measured.** Row 9 is the shared-template argument;
  no session has shown two bodies with different slot counts, and if one ever exists the out-of-range index
  would keep the native local answer for that slot rather than a refusal.
- **The swap's own sound is where the mutation is.** `Body.SwapSlots` plays `Sound.Play("switch", …)` on
  whichever client runs it (`reversing/…/Body.cs:1427`), and the swap now runs on the OWNER — this is the landed
  stage-3 limit that an item's own sound stays with the client that mutates it (decision 221), not a new one.
- **The pre-fix red is the previous batch's, not a new one.** The tree changed only by this fix after that batch;
  re-deriving the throw on this tree would mean re-deploying the pre-fix build, so the red cited here is
  `20261006-f`'s record (its two diagnostic runs) and this cycle's new gate, which was read RED before the fix
  was written.
