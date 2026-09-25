# Remote inventory native parity — stage 4 family audit and acceptance preparation (2026-09-25)

Ticket: `docs/backlog/review/remote-inventory-native-parity-rework.md` (stage 4). Design record:
`docs/architecture/remote-inventory-native-parity.md`. Decisions: 217 (stage 0), 218 (the window's
shape, stage 1), 219 (the container family and the while-dragging body), 220 (the item interactions),
221 (this audit and the limits it closes). Cycle scope: the ticket's acceptance matrix row by row —
both directions, a third peer, worn items, containers, the craft screen and the container window —
plus the re-evaluation of the two projection tickets the design adjudicated in §5.

## 1. What this stage is, and what it is not

This stage changes no runtime behaviour. It is the audit the design's §4 stage table reserves for the
end of the rework: every row of the matrix is traced to the mechanism that implements it, to the test
that pins it and to the part that only a two- or three-client session can settle. Nothing here is a
completion claim for the acceptance run: the rows marked *user's run* below are the checklist entries
the delivered build must pass on the real machine, and the three limits in §4 are the questions that
run has to answer.

Two consequences of that framing are worth stating up front, because they decide how to read the
tables:

- A row whose mechanism is a **code fact** is closed for development purposes: the mechanism exists,
  its guard order is the game's own and a test pins the decision. It is not evidence that the row
  *feels* right on a real client.
- A row marked **user's run** has no automated substitute at all: no test in this repository drives
  the game's rendering, input interception, animation or audio.

## 2. The acceptance matrix, row by row

Anchors are our own paths plus quoted text (or `reversing/` file:line, whose line numbers are stable
because that tree is never edited).

| # | Mechanism audited | Test anchor | Verdict |
|---|---|---|---|
| 1 | Guest opens the host's backpack and operates an item. `PlayerCameraDragUsePatch` opens the release window only for a dragged `RemoteCloneRender` proxy that carries an authoritative instance id and owner; the native `HandleReleaseDragging` body then runs and its mutation calls become intents (`RemoteDragIntentCapture`), the host validates and forwards (`PlayerRemoteInventoryIntentService`), the owner replays (`RemoteIntentApplier`) and re-reports (`CharacterDataSync.ReportInventoryChanged`). | `RemoteDragIntentCaptureTests`, `RemoteIntentRequestTests` (`GuestOwner_IntentRequest_IsForwardedToTheOwner`) | code fact; the *feel* is the user's run |
| 2 | Host opens the guest's backpack. The same capture runs on the host: `SendRemoteInventoryIntent` routes the host's own gesture through `HandleRemoteInventoryIntentRequest(_session.LocalSteamId, msg)` instead of the wire, and `Forward` sends `NetMsg.RemoteInventoryIntent` (registered `HostToGuest`) to the guest owner. | `RemoteIntentRequestTests` — `ContainerTakeOutAndBack_ForAGuestOwner_LeavesTheHostsCopyOfTheInventoryAlone` sends from the HOST's own client with the guest as the owner and asserts the two forwarded `NetMsg.RemoteInventoryIntent` frames (the forward hop); `HostOwner_IntentRequest_RaisesTheIntentOnTheHost` pins the local-raise half of `Forward` (a guest requester, the host as the owner) | code fact; the *feel* is the user's run |
| 3 | Nested container (trash bag) insert: R4/R13/W4 coalesce `UnloadItem` + `LoadItem` on one container into one `MoveIntoContainer`; the owner's own `Container.LoadItem` re-reads weight, tags and the 10-unit distance. | `RemoteDragIntentCaptureTests`, `RemoteIntentRequestTests` (`ContainerMove_IsForwardedToTheOwner`) | code fact; user's run |
| 4 | Take back out: W1 is `TakeOutOfContainer`; stage 2's vanish regression pins the reported shape — after a container take-out and re-insert for a guest owner both items are still TOP-LEVEL in the host's copy of that inventory, which a mirror edit nesting one under the other would have broken. It asserts their presence (not the whole tree: count, slots and contents are not compared), so the claim is the vanish shape, not "nothing else changed". | `RemoteIntentRequestTests` (`TakeOutOfContainer_ForAnItemInsideAContainer_IsForwardedToTheOwner`, `ContainerTakeOutAndBack_ForAGuestOwner_LeavesTheHostsCopyOfTheInventoryAlone`) | code fact; user's run |
| 5 | Drop / edge drop: the world fallbacks run natively and become `DropItem` (W2), `DropWearable` (W3) or the take-out (W1); the owner's `Body.DropItem`/`DropWearable` decide the position, the sound and the guard. | `RemoteDragIntentCaptureTests`, `RemoteIntentRequestTests` (`WornDrop_ForAnItemInsideAContainer_IsForwardedToTheOwner`) | code fact; user's run |
| 6 | Slot move / swap / switch hands: R8 is `SwapSlots`, R9 is `PickUpToSlot` preceded by `RemoteIntentSlotRelease.Plan`'s native order (drop the held item, drop the occupying slot item, then pick up — never `force`). | `RemoteIntentSlotReleaseTests`, `RemoteIntentRequestTests` (`SlotSwap_IntoAHandSlot_IsAdmitted`, `SlotIntent_OutsideTheOwnersInventory_IsRefused`) | code fact; user's run |
| 7 | Use / wear / combine / battery load-unload / favourite: R10's two independent `if`s produce `WearItem` then `UseItem` in native order, R6/R3/R2 carry the second item operand, and the while-dragging frame's `favourited` store is observed across the frame bracket. | `RemoteItemInteractionCaptureTests`, `RemoteItemInteractionOperandsTests`, `RemoteInventoryIntentWireTests`, `RemoteIntentRequestTests` (`TwoItemIntent_*`, `TraderHandIn_*`) | code fact; the item's own sound is a limit (§4) |
| 8 | Held remote item, backpack closed, used from the medical panel: `ApplyToLimb` keeps the landed host-authoritative `PlayerItemUseService.HandleRemoteHeldItemUse`; the host refuses a worn item by name, which is the scope that flow had before this rework. | `RemoteUseOnSelfTests` (`UseOnSelf_WornRemoteItem_IsRefused`, plus the four end-to-end `ApplyToLimb` cases around it); the landed medical-operation suites | code fact; user's run |
| 9 | A gesture the native rules refuse: the native guard runs on the owner and each refusal has a named line — `ApplyUseItem`, `ApplyWearItem`, `ResolveCarried`, `ApplyLoadBattery`, `ApplyUnloadBattery`, `ApplyGiveToTrader`, `ApplyMoveContainerChildren`. A release that produced no intent is classified (R1/R7/R14 in the drag patch, R10's no-op by the radial probe) or logged as an unclassified gesture — never a silent no-op. | `RemoteItemInteractionCaptureTests` (the radial no-op and the frame's refusal to claim a release no-op), `RemoteDragIntentCaptureTests` — these pin the WINDOW's classification half; the owner-side refusal lines live in the scene-bound `RemoteIntentApplier`, which no test drives by design | code fact |
| 10 | A third peer watches the result. No per-viewer branch exists in this family: the owner's immediate re-report goes through the shared authoritative path — `CharacterDataSync.ReportCharacterData` broadcasts on the host and reports to the host from a guest, and the viewer's clone is rebuilt from that fact. | the landed relay pattern for other domains (`CharacterSoundSyncTests.GuestReport_*`, `CharacterRagdollSyncTests.GuestReport_*`, `EnemyBiteSyncTests.GuestReport_HostApplies_OtherGuestProjects_SourceGuestSkips`); this family's own runtime tests drive host + owner | code fact for the *path*; the third client's screen is the user's run, and no test in this repository drives three clients through this family |
| 11 | Solo / no session: the window only opens for a `RemoteCloneRender` proxy — a local item returns from the prefix before the native body runs — and `SendRemoteInventoryIntent` refuses when there is no live session world. The radial probe only records a classification. | none: both guards are source facts (`PlayerCameraDragUsePatch`'s proxy gate, `SendRemoteInventoryIntent`'s dead-session guard) that no test drives; `RemoteBackpackViewCloseTests` is the unrelated local-Tab instant-close regression, so this row has no pinning test and says so | code fact |
| 12 | The operator's own screen: the native branch runs on the operator's client, so its own feedback (the ring, the cursor and drag image, the slot scale changes, `DoAlert`, `PlayBackpackSound`, the container-window state) is the game's, not a CUO replica. Feedback produced *inside* the replayed mutation (the item's own animation and sound) happens where the mutation happens. | design §3.2.3/§3.4; the capture tests above | user's run (the branch-level feedback is a code fact; animation and sound are not) |
| 13 | Host holds metal scrap at 75% condition, guest opens the host's backpack: the viewer's clone receives `Condition`, `Favourited` and `Liquids` through `RemoteItemPresentation.ApplySourceValues`, and a later change rides the item events or the periodic character snapshot (`CloneFactTable`: condition is deliberately not a divergence signal because decay moves it on its own). | `RemoteItemPresentationTests`, the landed projection suites | code fact |
| 14 | A water bottle, dog food, a lantern and metal scrap into the remote trash bag: the viewer's own `CanHoldItem` on the clones decides only whether the native loop runs, and the OWNER's `Container.CanHoldItem` decides per child which items actually move — the native partial outcome. | `RemoteDragIntentCaptureTests`, `RemoteIntentRequestTests` (`ContainerChildBatch_IsForwardedToTheOwner`, `ContainerChildBatch_IntoItself_IsRefused`) | code fact; user's run |
| 15 | Craft screen from a remote item, and opening a remote container's window: audited this stage — see §3.1 and §3.2. | new evidence below | code fact |

## 3. What this audit added

### 3.1 The craft screen cannot reach a display proxy (matrix row 15, first half)

The stage 3 audit recorded the craft screen's own consumption path as "local UI on the viewer" and
left it there. This stage checked whether a recipe opened from a REMOTE item (R14 drags a proxy onto
the craft button) can consume that proxy, and it cannot, for three independent reasons:

1. The material search reads the LOCAL body: `Recipe.GetItemsForRecipe` starts from
   `PlayerCamera.main.body.GetAllItemsThorough()` (`Recipe.cs:111`), and a display proxy is never
   parented to the local body — `CloneInventoryRenderer` parents it to the focused clone and marks it
   `RemoteCloneRender`.
2. The world half of the same search requires an enabled collider:
   `Physics2D.OverlapCircleAll(PlayerCamera.main.body.transform.position, 10f, LayerMask.GetMask("Item"))`
   (`Recipe.cs:112`) is a physics query, and every clone render disables its collider —
   `CloneInventoryRenderer` ("never pickable/blocking") and `RemoteBodyFactory` ("Disable ALL
   colliders on the proxy") for the top-level renders and the body clone, and
   `RestoreRemoteContents` → `MarkRemoteCloneTree` for the nested container children, whose loop
   disables the collider of every descendant.
3. Even if a proxy were handed to the craft path, it carries no authoritative id to consume:
   `RemoteBodyFactory` destroys the clone's `ItemInstanceId` components and
   `CloneInventoryRenderer.SetRemoteInventoryItemId` stamps the display marker
   (`RemoteInventoryItemId`) instead, so `ItemIdAllocator.EnsureId` would have to allocate a fresh id
   — the "extra item" family the design's §6 rules out.

The remaining behaviour is the native one, and it is recorded rather than changed: the recipe list is
filtered by the remote item, while the craft consumes the OPERATOR's own matching materials (local
body plus nearby world items). No remote-inventory intent is produced, no host round trip happens FOR
THE REMOTE ITEM and no proxy is mutated, which is the row's expected outcome — with one qualification
the row's original wording needed: the craft itself still rides the landed craft report
(`CraftingSync`'s one-craft-one-report path), because what it consumes is the operator's own local
materials. The row is about the owner's item, and the owner's item is untouched.

### 3.2 The container window re-binds by authoritative instance id (matrix row 15, second half)

`RemoteBackpackView.TrackOpenRemoteContainer` records the container's `RemoteInventoryItemId`, and
`UpdatePendingContainerRefresh` re-binds `PlayerCamera.main.currentContainer` to the clone's current
container proxy by that id before calling `RepopulateContainer()` — the re-bind exists because a
periodic render may destroy and recreate the whole container proxy while the window is open. The
window's rows are proxies like every other clone render (same marker, same disabled collider), so a
gesture made from that window enters the same release window as any other drag.

### 3.3 Worn items resolve on the owner

`CarriedItemLocator.FindById` searches the local body's whole carried subtree
(`body.GetComponentsInChildren<Item>(true)`) and skips anything under `RemoteCloneRender`, so a WORN
item is found by its authoritative id like a carried one; `ApplyWearItem` and `DropWearable` are the
two native calls that act on it. The stage 4 concern that a worn item might be invisible to the
applier is therefore closed by the locator's own implementation. The traversal reaches worn items
because the game parents a worn item to its limb transform (`Body.WearWearable` →
`item.transform.SetParent(limb.transform)`), and production code elsewhere reads the same traversal as
reaching "a worn limb". No test here opens the shipped prefab hierarchy to confirm that a limb is a
descendant of the body GameObject, so the acceptance checklist asks for that one runtime confirmation
in its row 7.

### 3.4 Both directions share one validation path

The host-as-requester direction does not have a second implementation: the host's own capture calls
`HandleRemoteInventoryIntentRequest` with `_session.LocalSteamId`, and `Forward` either raises the
intent locally (owner is the host) or sends `NetMsg.RemoteInventoryIntent` to the guest owner. Rows 1
and 2 therefore differ only in which hop carries the payload, and the host's refusals (permission,
membership, the ownership fact, operands, arbitration) are the same code for both.

## 4. The limits that go to the acceptance run

These are the three gaps the earlier stages recorded. None of them is fixed here, and none of them can
be fixed without a design decision this ticket deliberately does not take; each one is an explicit
question for the user's run, which is why the acceptance checklist repeats them as judgement items.

| Limit | Evidence | Why it is a limit | What the run must judge |
|---|---|---|---|
| The item's own sound plays only where the mutation happens | design §3.4's audit: `combine` and `waterpour` (`Body.CombineItems`/`CombineLiquids`), `batteryinsert` (`BatteryItem.LoadBattery`/`UnloadBattery`) and the `useAction` eating/drinking clips are `Sound.Play` calls INSIDE the mutation, and no existing path carries an item's own sound; re-sending them would change local-versus-remote for every player and needs a channel that does not exist | the design's own rule — no second feedback path — is what the rework was built on | whether the operator can accept hearing the combine / battery / eating sound on the owner's client instead of their own |
| The radial weight readout prints the operator's own encumbrance | `PlayerCamera.cs:1901` reads `this.body.totalEncumberance` / `this.body.maxEncumberance` — the LOCAL body — while the ring renders the focused clone; the clone bodies receive no encumbrance projection (the only writer is `ModStatusVanillaProjection`, on the local/mod-status path) | a correct readout would need the owner's encumbrance projected onto the clone plus a new readout patch: a display feature the design's §6 explicitly deferred to this audit | whether the readout must show the owner's weight, or whether the operator's own weight while browsing is acceptable |
| `TransferToBody` and `ApplyToLimb` keep their landed host-authoritative paths, and the owner's body runs no release animation | design §6: the custody transfer rides the snapshot edit plus the transfer event, and the cross-player use rides `PlayerItemUseService`; the native world has no cross-player release call to replay | the owner-side native release the design's principle 1 would want does not exist as a native call — the transfer is a CUO-side custody move | whether the missing owner-side animation is noticeable in the row 2 / double-Tab flow |

## 5. The two projection tickets, re-evaluated

| Ticket | Verdict | Why |
|---|---|---|
| `docs/backlog/review/unified-remote-display-projection-rework.md` | **stays landed, no reopening** | its seam is the display half this rework relies on: matrix rows 3, 4, 13 and 14 are read through `RemoteItemPresentation.ApplySourceValues` and `CloneInventoryRenderer`, and this audit found the interaction rework adds no display requirement the seam does not already carry. Its "remaining boundary" paragraph named the interactive remote-backpack rows as living in this ticket; those rows are now audited here, so the boundary closes without reopening the projection work. |
| `docs/backlog/review/global-projection-framework.md` | **stays landed, no reopening** | this rework changes interaction, not the projection contract; the audit found no new projection domain, no new rebuild path and no change to the boundary the ticket records (the remote-character-presentation store is still not the adapter's sole display source). |

## 6. What only the real machine can settle

The acceptance checklist `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`
carries the rows of §2 that need a session, the reproduction steps the ticket was opened with, the
three limits of §4 as judgement items, and the build identity to verify before starting. Nothing in
this audit substitutes for that run: this repository has no in-game probe, and no test here renders a
frame, plays a clip or reads the operator's input.

## 7. Observations recorded, not acted on

Two pre-existing items surfaced while auditing this family. Neither is part of this change, neither
affects a matrix row, and both are recorded here rather than fixed silently inside an audit cycle:

- `CloneInventoryContentSanitizer` documents a live seam ("the renderer's restore input is stripped
  here so the display restore never stamps a domain id onto a proxy") but no production code calls it.
  The craft proof of §3.1 does not rest on it: it rests on `CloneInventoryRenderer` never adding an
  `ItemInstanceId` and on `RemoteBodyFactory` destroying the clone's ids.
- `RemoteIntentRequestTests.WornDrop_ForAnItemInsideAContainer_IsForwardedToTheOwner` is named for a
  container case while its body drops a plain worn item. The body does pin the behaviour this audit
  cites it for (the `DropWearable` forward); the name is the defect.
