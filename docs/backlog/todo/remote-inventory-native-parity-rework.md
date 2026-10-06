# Remote inventory operations: run the native path end to end

- Status: Todo — Rejected (batch `20261006-g`, 2026-10-06: matrix row 6's OCCUPIED-destination half — the
  swap the fix cycle below targets — now **passes** on every client of the deployed artifact, and its
  EMPTY-destination control still passes with zero divergence on either viewer. Rows 7 (battery-load,
  combine, favourite halves) and 8 (held remote item from the medical panel) stay `unproven`: this session
  staged no battery receiver, no combineable pair, no favourite key and no treatable limb, so the ticket
  stays open on them alone. Record:
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`; the swap's own monitor
  reading is filed on `todo/container-content-event-gap-on-repick.md`. Previous rejection, batch
  `20261006-f`, 2026-10-06, drove matrix row 6's two halves on three clients:
  the EMPTY-destination half passes (the item lands in the owner's slot, no divergence), and the
  OCCUPIED-destination half — the swap on the owner's body — could not run at all. The operator's release aborted
  inside the game's own release body with `UnityException … Transform child out of bounds`:
  `Transform.GetChild` ← `Body.GetItem` ← `Body.SlotOf(item)` ← `PlayerCamera.TryPerformInventoryAction` ←
  `TryPerformUIActions` ← `HandleReleaseDragging`. The cause is a seam asymmetry — the predicate patches
  answer `HoldingItem(int)` from the displayed clone while `Body.GetItem`'s own body still indexes the LOCAL
  body's slot transform — so an empty local slot at an index the clone has occupied throws before any CUO
  seam can absorb the call, the release produces no intent and no refusal line, and the drag is never
  cleared (`dragAfter` still set after the abort; the ring had closed and the batch's probe cleared it). Record:
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-f.md`. Rows 7 (battery-load,
  combine, favourite halves) and 8 (held remote item from the medical panel) stay `unproven`, and the
  container-child route's carried facts carry a monitor gap filed as
  `todo/container-content-event-gap-on-repick.md`; the swap half's seam asymmetry is fixed in the cycle
  below, and its re-read is the next batch's row)
- Priority: Critical
- Category: Remote inventory / native interaction parity / architecture rework
- Source: User acceptance findings (2026-09-21) plus the same day's ruling: operating another player's items must feel exactly like operating one's own — the same functions, the same item animations, the same UI feedback and the same sounds. The current implementation is rejected as a whole and is to be replaced, not patched again.
- Related: `resolved/remote-backpack-native-interaction-parity.md` and `resolved/remote-backpack-item-projection-acceptance-issues.md` (the rejected deliveries this ticket replaces, absorbed here in stage 0), `review/unified-remote-display-projection-rework.md`, `review/global-projection-framework.md`, `review/tab-backpack-open-close-immediately.md`, `done/guest-container-contents-ghost-drops-on-host.md`
- Design: `docs/architecture/remote-inventory-native-parity.md` (stage 0 record, decision 217)
- Acceptance record: `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md` (row 6's
  occupied-destination half and its empty-destination control both `pass`, matrix row 2's transfer half
  `pass`; rows 7 and 8 `unproven`),
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-f.md` (row 6's
  empty-destination half `pass`, its occupied-destination half `fail`; rows 7 and 8 `unproven`),
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261005-d.md`
  (rows 3, 7, 9, 13, 14; rows 4 and 8 stay `unproven`); batch `20261006-a` planned two further halves of
  this ticket and staged NEITHER: row 8's battery-LOAD half (that run's fixture set carried no
  installed-battery receiver) and row 14's multi-item insert series (its own plan said "driven only if the
  fixtures exist, else named as this run's limit"). No row of this ticket is judged by that batch, and
  both halves stay `unproven`.

## Reported behaviour (2026-09-21)

1. Guest opens the host's backpack — nothing inside can be operated at all.
2. Host opens the guest's backpack — items can be moved into the trash bag, but an item taken back out
   of the trash bag disappears a short time later, and the owner's items cannot be dropped.
3. Target ruling: a remote item operation must be indistinguishable from the same operation on the
   player's own inventory — function, animation, UI and sound. Player experience is the governor.

## Why patching again cannot reach parity (root cause)

An operation is currently executed in TWO worlds that only meet at the 1 Hz character snapshot and
the item event stream:

- The host re-implements the inventory semantics on a serialized clone of the owner's character
  data: `PlayerRemoteInventoryService.HandleDrop`, `HandleMoveToContainer` and `HandlePour` each
  clone the owner's `CharacterDataMsg` via `PlayerCharacterAccess.CloneCharacter`, edit the item
  tree inside that clone, issue a kernel item command built from the clone and save the clone back
  as the authoritative character data.
- The owner's client executes the operation natively for a DIFFERENT subset of kinds only — the
  `RemoteInventoryApplyMsg` family in `RemoteInventoryOperationApply`.

A mirror edit therefore loses to the next authoritative report from the real body, which is the
reported "taken out of the trash bag, then it disappears". The two halves also carry different
rules, which is why the two directions behave asymmetrically.

The gesture side has the same shape: `PlayerCameraDragUsePatch` and the router in
`RemoteBackpackOperationHandler` classify a native release through a hand-written ordering
(UI-only windows -> container move -> radial centre use/wear -> inventory-button battery/combine/
slot -> pour -> edge drop -> take fallback) into a CUO operation enum. A native gesture that
ordering does not classify does nothing and reports nothing — the reported "cannot operate the
items at all". Every native rule the operation should inherit (stacking, container guards, slot
rules, item animations, item sounds) is either re-implemented or missing, so parity is unreachable
by construction rather than by omission.

## Goal

One mutation site and one truth: the OWNER's client runs the operation through the game's own code
path, the host decides only whether a peer may do it, and the viewer's backpack is a projection of
the owner's facts. The operator's screen shows the native result — the native animation, UI
feedback and sounds.

## Design principles (the implementation cycle turns these into the design record)

1. One mutation site: only the owner's client mutates items, by running the same native calls the
   local gesture would have run, so the game's inventory/container semantics stay the single
   implementation.
2. The wire carries a native-shaped INTENT (which item, which target slot/limb/container, which
   second item), never a re-implemented operation taxonomy; the classification comes from the
   native gesture pipeline's own resolution, not from a CUO routing table.
3. The host validates permission, membership, visibility and ownership, arbitrates conflicts
   first-writer-wins and records the resulting fact; it never models inventory contents.
4. The viewer's backpack renders the owner's facts and never runs a native mutation on a proxy;
   its gestures produce intents.
5. The operator's own client shows the native feedback immediately, the owner's screen shows the
   result as its own body performs it, and latency is never a judgment input.
6. Every refused or unrepresentable gesture is answered and observable; a silent no-op is a defect.

## Stage 0 — design (landed 2026-09-21)

Design record: `docs/architecture/remote-inventory-native-parity.md`. This cycle's evidence:
`docs/evidence/selfchecks/items/remote-inventory-native-parity-design-selfcheck.md`. Ruling:
decision 217. Stage 0 changed no behaviour.

What it settled, one line each:

1. **The native pipeline is inventoried branch by branch.** All fourteen UI release branches and all
   four world fallbacks of `PlayerCamera.HandleReleaseDragging` are mapped to the exact native call
   each one makes, with `reversing/` anchors, and so are the two *while-dragging* actions (the liquid
   drain tick and the favourite toggle) that the rejected implementation treated as release branches
   and therefore never produced.
2. **The capture seam is the native call, not a CUO ordering.** The viewer keeps running the game's
   own gesture path and CUO intercepts only the mutation call, inside a release window opened by the
   drag patch; the classification is therefore the game's own. The native local feedback of the
   gesture (sounds, alerts, UI state) still happens because the rest of the branch still runs. Three
   shipped patches hold that path shut today (`PlayerCameraDragUsePatch`,
   `PlayerCameraHandleWhileDraggingPatch`, `PlayerCameraTryPerformRadialActionPatch`, plus
   `RemoteProxyDragPolicy`); the design names each one and the stage that removes it, because the
   design does not work while the native branch is unreachable.
3. **The intent vocabulary is the native call plus its operands** — one member per native call, keyed
   by instance ids; the operation enum with its CUO semantics (`BatteryLoad`, `TransferToRequester`,
   the release-time `Pour` and `FavoriteToggle`) is replaced.
4. **The one intent with no single native call behind it is named as such**: `TransferToBody` is the
   two-sided transfer whose halves are the owner's native release and the destination body's native
   pickup.
5. **The host stops modelling inventory contents**: it validates session permission, membership and the
   ownership fact, arbitrates the item id first-writer-wins and records the fact the owner reports.
   One correction to principle 3 above: the reach/sight verdict is the ACTOR's own client's, per the
   landed rule in `review/remote-interaction-local-gating.md`, so the host never judges it.
6. **The protocol is bumped with stage 1** — the vocabulary replaces the wire enum, so the wire
   changes; the handshake check is the compatibility boundary and no dual shape is kept.

## Stage 1 — the native intent path (landed)

The wire, the capture seam, the owner-side replay and the host half replaced the clone-edit path in
one change, with `ProtocolVersion.Current` bumped (35 → 36, the handshake stays the compatibility
boundary) and decision 218 for the seam's shape.

What it settled, one line each:

1. **The release window is a call bracket.** `PlayerCameraDragUsePatch` opens it for a display proxy
   and its finalizer closes it, so the only calls inside it are the ones the game's own release
   branch makes — a projection rebuild (`CloneInventoryRenderer` loads and unloads its clones) or any
   other Unity callback cannot be captured by construction.
2. **That branch's guards are answered by the body the ring shows.** The native branch reads
   `PlayerCamera.body` — always the local body — while the inventory ring and the dragged proxy
   belong to the displayed clone, so `RemoteDragPredicatePatches` answers `HoldingItem`, `GetItem`,
   `GetWearable` and `DoPickupCheck` from that body, and `DropItem(int)` (R9's occupying-slot step)
   is absorbed instead of dropping an item of the requester's own body.
3. **The window coalesces the native pairs and names the rest**: `UnloadItem` + `LoadItem` on one
   container is one `MoveIntoContainer`, R9's own drops are absorbed by its slot pick-up, and the
   vocabulary carries `DropItem`, `DropWearable`, `TakeOutOfContainer`, `SwapSlots`, `PickUpToSlot`
   and `TransferToBody` (the ring back on the requester's body).
4. **A gesture whose intent a later stage carries is refused, logged and never run on a proxy**
   (combine, battery, favourite, the container-expansion batch, the trader hand-in), and a release
   that produced nothing is logged as an unclassified native gesture — never a silent no-op.
5. **The owner replays the native call** (`RemoteIntentApplier`) on the real items resolved by
   instance id, with R9's own sequence and no `force: true`; a guard refusal is the native refusal
   and is logged with the intent, the item and the reason.
6. **The host validates and forwards; it never models inventory contents.** Session permission,
   membership, the ownership fact, the operands and the destination body; the item is arbitrated
   first-writer-wins (`RemoteIntentArbitration`). `TransferToBody` and `ApplyToLimb` keep the landed
   take and cross-player use paths.
7. **Deleted with the clone-edit path:** the operation enum with its request/apply pair,
   `PlayerRemoteInventoryService`'s mirror-edit halves, `RemoteBackpackOperationHandler`,
   `RemoteInventoryOperationApply` and `RemoteProxyDragPolicy`.

## Stage 2 — containers and the while-dragging body (landed)

The container family and the restored while-dragging body ride the stage 1 seam, with
`ProtocolVersion.Current` bumped in the same change (36 → 37, the handshake stays the compatibility
boundary) and decision 219 for the two rules the implementation settled.

What it settled, one line each:

1. **The container-expansion gesture (R5) is ONE intent.** `MoveContainerChildren` names the dragged
   container and the target container; the owner enumerates the dragged item's OWN container
   (`dragItem.container`) and its own `Container.CanHoldItem` decides per child which items move, so
   the native partial outcome is reproduced where the items are. One intent per child would make the
   owner run the loop once per child, on a container the first run has already emptied.
2. **The while-dragging body runs again, in its own bracket kind.** One bracket per
   `HandleWhileDragging` frame, the drain tick's `Body.DoPickupCheck` gate answered for the dragged
   proxy, and the radial menu still anchored to the focused clone instead of the local body.
3. **The drain tick is one intent per frame, carrying the amount.** The owner replays
   `WaterContainerItem.Drain(CalculateDrain(amount))` on its own stack — never the per-stack list the
   viewer computed from its proxy — so the distribution is derived where the item really is.
4. **A continuous kind does not re-report per frame.** The immediate authoritative character
   re-report stays with the discrete kinds; the drained state rides the periodic character snapshot
   the game's own un-evented item state (decay, battery charge, liquids) already uses, and the
   continuous happy path logs at Debug while a refusal keeps its Information/Warning line.
5. **The favourite toggle stays refused, observably.** It is a direct `favourited` field write on the
   hovered item, so there is no call to intercept: the frame that would write it is skipped with one
   line per dragged proxy, and the intent plus its store-level seam arrive with stage 3.
6. **The vanish case is pinned by a regression.** A container take-out and re-insert for a guest owner
   must leave the host's own copy of that inventory untouched, so nothing is left for the owner's next
   report to overwrite — the deleted mirror edit was the cause, and its absence is now asserted.
7. **The container-window path was re-checked against the restored branch.** `OpenContainer` tracking
   and the rebuild-time re-bind are keyed by the authoritative instance id and stay valid; the window's
   contents ride the projection, and the row stays a real-machine acceptance item.

## Stage 3 — item interactions (landed)

The item-interaction family rides the stage 1 seam, with `ProtocolVersion.Current` bumped in the same
change (37 → 38, the handshake stays the compatibility boundary) and decision 220 for the two rules
the implementation settled. Cycle evidence:
`docs/evidence/selfchecks/items/remote-inventory-native-intent-stage3-selfcheck.md`. What it settled,
one line each:

1. **The favourite toggle is observed across the frame bracket, not intercepted.** It is a direct
   `favourited` field store on the HOVERED item with no call behind it, so the while-dragging patch
   compares the field of the frame's own candidate buttons before and after the native body: the
   native condition still decides whether a store happens, the store it observes becomes
   `ToggleFavourite`, and the proxy's field goes back to the value the frame started with. The stage-2
   price for this gesture — skipping the frame and losing its drain tick — is gone.
2. **R10 runs again.** The suppression patch is deleted and `Body.UseItem` / `Body.WearWearable` are
   captured like every other mutation; the branch's two independent `if`s can produce `WearItem` then
   `UseItem` from one release, and the one native outcome that consumes a release and runs nothing (an
   item that is neither wearable nor usable inside the circle) is recorded as a classified no-op from
   the native method's own answer.
3. **A two-item call names both operands.** `CombineItems` carries the hit item — the receiver the
   native call takes first — and `LoadBattery` carries the receiving item, so the message gains
   `TargetItemInstanceId`; `UnloadBattery` names the HIT item alone because that is all the native call
   reads.
4. **The frozen vocabulary's `GiveToTrader` row is corrected.** Its native call reads
   `PlayerCamera.currentTrader`, which the owner's client does not have, so the trader itself is an
   operand carried as its world position — the identity the trade domain already keys its messages by —
   and the trader's own credit total is the verified write.
5. **The report-hook rule was applied to the family, not just to the new seams.** `OnItemUsed` and the
   wearable "re-report right away" now skip a display proxy, and the trader's `GiveItem` report fires
   only when the credit really moved — without which the requester's captured call would have credited
   the trader for an item nobody gave.
6. **Two audits concluded with evidence rather than assumption.** The item sounds played inside a
   replayed call (`combine`, `waterpour`, `batteryinsert`, the `useAction` clips) are carried by no
   existing path and stay on the client that runs the mutation: recorded as a limit, because the design
   adds no second feedback path. Every mutation call of the family has exactly one other caller outside
   the drag pipeline (the touch auto-wear, which cannot reach a proxy; the craft screen's own
   consumption and battery calls, which are local UI), the medical panel reaches them through the
   captured `ApplyWoundItem` and the landed medical-operation session, and the design's context-menu
   note has no counterpart — `UIUtil.IsPointerOverContextMenu` has no caller in the build.
7. **The held-remote-item medical chain keeps its landed path.** Matrix row 8's semantics (the owner's
   item state is consumed, the requester's body takes the effect) are `PlayerItemUseService`'s
   host-authoritative flow, which the `ApplyToLimb` intent already routes to; this stage audited that
   route instead of re-implementing it, and row 8's real-machine behaviour stays the user's run.

## Stage 4 — family audit and acceptance preparation (landed)

No runtime behaviour changed in this stage. The acceptance matrix was audited row by row against the
mechanism that implements it, the test that pins it and the part only a session can settle; every
verdict and its anchors are in the cycle evidence
(`docs/evidence/selfchecks/items/remote-inventory-native-intent-stage4-audit.md`), together with
decision 221. What it settled, one line each:

1. **The craft screen cannot consume a remote item (row 15).** The recipe's material search reads the
   LOCAL body and a world overlap that needs an enabled collider, and every clone render disables its
   collider and carries no authoritative instance id — so the native local UI runs, nothing about the
   OWNER's item travels and no proxy is mutated. The craft consumes the operator's own matching
   materials, which is the native behaviour, and it still rides the landed craft report for those
   materials; it is recorded rather than changed.
2. **The container window re-binds by authoritative instance id** (`RemoteBackpackView`), and the
   window's rows are proxies like every other clone render, so a gesture made from it enters the same
   release window.
3. **Worn items resolve on the owner**: `CarriedItemLocator` searches the local body's whole carried
   subtree and skips display proxies, which is what `ApplyWearItem` and `DropWearable` need.
4. **Both directions share one validation path**: the host's own gesture enters
   `HandleRemoteInventoryIntentRequest` with the local SteamId, and the same forwarding rule carries it
   to a guest owner, so rows 1 and 2 differ only in which hop carries the payload.
5. **The third peer's view is the landed authoritative path**, not a per-viewer branch: the owner's
   immediate re-report is the character-data path every other remote fact uses. No test in this
   repository drives three clients through this family, so the row stays the acceptance run's.
6. **The two adjudicated projection tickets stay landed** and are not reopened: the interaction rework
   adds no display requirement their seam does not carry, and the interactive rows their boundary
   deferred are the ones audited here.
7. **Three limits are the acceptance run's judgement items**, each with its evidence and its reason in
   the audit's §4: the item's own sound plays where the mutation runs, the radial weight readout shows
   the operator's own encumbrance, and the custody transfer keeps its host-authoritative path with no
   owner-side release animation. The checklist carries them as explicit questions, because none of them
   can be settled without a decision this ticket does not take.

The acceptance checklist
(`docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`) carries the
reported behaviour, the rows that need a session, the three judgement items and the build-identity
step. With this stage the ticket's development work is complete; the run itself is the user's
release-cycle action.

## Staged plan

- **Stage 0 — design.** Done, see above.
- **Stage 1 — the native intent path.** Done, see above: the window, the capture seam, the owner-side
  replay, the host half and the deletion of the clone-edit path, protocol bumped in the same change.
- **Stage 2 — containers.** Done, see above: R5 as one owner-evaluated `MoveContainerChildren`
  intent, the restored while-dragging body with the per-frame drain tick, and the vanish case as a
  host-copy regression.
- **Stage 3 — item interactions.** Done, see above: the seven item-interaction kinds with the second
  item operand and the trader operand, the favourite store observed across the frame bracket, R10's
  use/wear branch restored with its no-op classified, and the sound / medical-path audits concluded.
- **Stage 4 — family audit and acceptance.** Done, see above: the matrix was audited row by row, the
  two projection tickets were re-evaluated, and the limits the earlier stages recorded are the
  acceptance run's judgement items; the run itself stays the user's release-cycle action.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest opens the host's backpack and operates an item | Every native gesture works exactly as on the guest's own backpack |
| 2 | Host opens the guest's backpack and operates an item | Same, reverse direction |
| 3 | Drag an item into a nested container (trash bag) | Enters immediately, keeps weight and condition, survives the next snapshot and a re-open |
| 4 | Take that item back out | Comes out immediately and stays out |
| 5 | Drop / edge-drop an owned item from the remote view | Native drop at the native position with the native sound |
| 6 | Slot move / swap / switch hands | Native result on the owner's body |
| 7 | Use / wear / combine / battery load-unload / favourite | Native result, native animation and native sound |
| 8 | Held remote item, backpack closed, used from the medical panel | Works; the owner's item state and the requester's body effect are both correct |
| 9 | A gesture the native rules refuse (full container, blocked slot) | Refused with native feedback and an observable log line, never a silent no-op |
| 10 | A third peer watches the result | The same item state everywhere |
| 11 | Solo / no session | Unchanged local behaviour |
| 12 | The operator's own screen during any row above | The same animation, UI feedback and sounds as operating their own inventory |
| 13 | Host holds metal scrap at 75% condition and the guest opens the host's backpack | The guest sees 75%, and the value follows the owner's later changes (absorbed from `resolved/remote-backpack-item-projection-acceptance-issues.md`) |
| 14 | Move a water bottle, dog food, a lantern and metal scrap into the remote trash bag | Every item the native weight and tag rules allow enters immediately and stays (the absorbed selective-insertion finding) |
| 15 | Craft screen from a remote item, and opening a remote container's window | Native local UI on the viewer: no native inventory intent and no host round trip for the remote item (the craft consumes the operator's own materials and so still rides the landed craft report), and no proxy mutation |

The two absorbed tickets' remaining rows are covered by rows 3-9 above: the trash-bag take-out and
re-insert vanish (rows 3-4), pour and edge drop (row 5), main-hand and other slot placement (row 6),
double-Tab transfer to the requester (row 2 with `TransferToBody`), and the held-remote-item medical
chain (row 8). Their original text stays in git and in the two `resolved/` records.

## Verification

The implementation cycle adds tests for the intent -> native-call mapping and keeps the item/kernel
suites green; the deployed-artifact identity is checked. The operating feel on the requester's
screen and the two-client behaviour are the user's release-cycle acceptance. Rows 1-4 of the
reported behaviour must be re-run on the deployed build until they no longer reproduce. Stage 4
consolidates that run — its steps, its expected outcomes, its three judgement items and the
build-identity check — in
`docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`.

## Acceptance readings (batch `20261005-d`, 2026-10-05)

Rows 7, 9 and 13 gained their verdicts here (all `pass`), and rows 3 and 14 were re-read beside the
carrier fix that batch also judged:

1. **Row 7's use and wear halves pass**, driving the radial CENTRE action the previous batch named as its
   gap: the committed recipe stages the drag (`mode=hover`) and casts the centre target
   (`mode=release cast=-2`), so the game's own `TryPerformRadialAction` runs and the `Body.UseItem` /
   `Body.WearWearable` calls it makes are captured. Read: `UseItem captured` → the owner's
   `[ItemUsed] bread … — host fact broadcast.` + `replayed native UseItem`; `WearItem captured` → the
   owner's `[SlotMoved] autopump … → slot -3 (Wear) — host fact broadcast.` + `replayed native WearItem`.
   The `combine` and `battery load/unload` halves were not driven, and `favourite` reads a key-down this
   driver may not produce.
2. **Row 9 passes on the container-guard shape**: `move refused by the native container guard: item
   lantern did not enter container … (weight, tag restriction or distance)` — a named refusal, not a
   silent no-op. Observed beside it and recorded as an observation, not judged: the radial no-op shape
   logs nothing at all, because `RemoteDragIntentDispatcher.Emit` logs only `IsUnclassified`.
3. **Row 13 passes**: the owner's soup written to `condition = 0.75` renders as `0.75` on the viewer's own
   proxy one snapshot cycle later.
4. Rows 4 and 8 stay `unproven`: the take-out needs the container window's own drag-out, and the medical
   row needs a treatable limb fixture plus the wound-view staging; neither was built this session.

## Acceptance readings (batch `20261005-e`, 2026-10-05)

The same three-client session that re-read the container-move fix judged three more of these rows:

1. **Row 4 passes, in both owner directions.** The take-out's missing capability was the container's own
   contents window: the contained child's proxy only exists in the operator's scene once
   `PlayerCamera.OpenContainer(proxy)` has run, which `container-panel mode=remote` stages
   (`contentsCount: 1`). Releasing that child **into the world** is what the row's
   `TakeOutOfContainer` classification needs — the owner logs
   `[ItemTrace] … origin=OnItemUnloadedFromContainer result=Committed(1) events=[Unload]` +
   `replayed native TakeOutOfContainer` — and the child then reads in the world on the third peer's own
   view. The same gesture replayed on the guest's bag by the host (guest as owner) behaves identically.
   Note for the remaining readings: releasing the contained child onto an empty **ring slot** is
   classified `PickUpToSlot` instead (the owner's replay still unloads the child through
   `Body.PickUpItem`, so it leaves the container — but the intent the row names is the world release).
2. **Row 7's battery half passes for `UnloadBattery`.** Guest `UnloadBattery captured` → owner
   `[PickUpResult] mediumbattery → slot (slot 1)` + `the native unload ejected item aed's battery onto
   the owner's body.` + `replayed native UnloadBattery`, and both viewers see the ejected battery arrive
   as its own carried fact with no divergence line. The fixture is narrower than it looks: world
   items carry an installed battery (`battery.hasBattery`), `Utils.Create`d copies carry the component
   without one and the native guard refuses them, so the world item has to be brought to the owner's
   body before the game's own pickup admits it.
3. `LoadBattery` from the remote view was not driven (it needs a battery item in the ring and a receiver
   with a battery slot), and the `combine` half and the `favourite` sub-kind keep the limits batch
   `20261005-d` recorded; row 8 still needs its medical fixture.
4. Row 14's monitor half was re-read for one item only (the dog food insert, silent on both viewers);
   the four-item series stands from batch `20261005-d`.

## Acceptance readings (batch `20261006-f`, 2026-10-06)

The batch drove matrix row 6's two halves, one release each, on three clients (operator = physical-machine
host, owner = sandbox guest, third peer = the alternate sandbox):

1. **Row 6's swap half cannot run on this build.** Releasing the displayed clone's own item onto an
   OCCUPIED slot of that clone reaches the native R8 branch — the run confirms it, because the throw happens
   inside that branch's own argument list — and aborts there: `Body.SlotOf(item)` walks `Body.GetItem(i)`,
   whose `HoldingItem(i)` the predicate patch answers from the displayed clone while the method body still
   indexes the LOCAL body's slot transform, so an empty local slot at an index the clone has occupied makes
   `Transform.GetChild(0)` throw. The whole release is lost with it: no intent, no refusal line, no swap on
   the owner's body, and `HandleReleaseDragging` never reaches its own `dragItem = null` — the drag was never
   cleared (the ring closed after the abort with `camera.dragItem` still set, and the batch's probe cleared
   it). The asymmetry is the thing to fix — the redirect answers a PREDICATE but not
   the method body that reads the same index — and it also decides whether `SwapSlots` can ever be captured
   on that path.
2. **Row 6's slot-move half keeps working**: a released proxy onto an EMPTY slot classifies `PickUpToSlot`,
   the owner's replay lands it (`[PickUpResult] bandage → slot (slot 2)`, `[SlotMoved] … (Drag) reported.`),
   its own drop-then-re-pick pair stays internal (`Cancelled events=[RePick]`), and neither viewer warns.
3. **The same proxy released over the world instead of over a ring button still works** — that release
   classified as `DropItem` and the owner replayed it to the world (`[ItemDropped] bandage …`,
   `op=43 … Flush result=Committed(1)`) — so the capture seam and the owner-side replay are intact; the
   failure is specific to the ring-button path.
4. **A container child moved into a ring slot lands, but the peers' fact tables warn twice each** about the
   container's contents change and the child's new carried slot; filed as
   `todo/container-content-event-gap-on-repick.md`. Row 4's take-out reads unaffected (it reports the drop),
   and the slot-item control is clean.
5. **Rows 7 and 8 stay undriven**: this session staged no battery receiver, no combineable pair, no favourite
   key and no treatable limb fixture, so the battery-load half, the combine half, the favourite sub-kind and
   the medical row carry the same `unproven` verdict as the earlier batches.

## The swap half's fix (landed 2026-10-06)

The failure above is one seam asymmetry, and it is closed: while the release bracket is open the redirect
answers the branch's queries about the displayed body, and it now answers each of them by SKIPPING the game's
own body instead of overwriting its result. The four `Body` queries of `RemoteDragPredicatePatches`
(`HoldingItem(Item)`, `HoldingItem(int)`, `GetItem(int)`, `GetWearable(string)`) are prefixes that assign the
displayed body's own answer and return `false`, so `Body.GetItem`'s index is never reached with a redirected
guard behind it; a slot index the displayed body does not have keeps the native answer, which is sound because
both bodies are instantiated from the same "Experiment" template and carry its serialized slot array
(`Character/RemoteBodyFactory.cs`, `Body.cs:3977`). The rule is decision 239 and `RemoteDragQuerySeamGateTests`
pins it with a census floor and matcher samples — read RED on the pre-fix tree
(`4 of 5 Body query seam(s) do not answer by skipping the native body`) and green after. The file's
`PlayerCamera.OpenContainer` seam keeps its postfix: it patches an action whose native body must run.

What this means for the rows, stated as verdicts rather than expectations:

- **Row 6's swap half `pass`es as of batch `20261006-g`** (the reading is in the section below). With the
  walk intact, the release reaches
  `this.body.SwapSlots(invButton.slot, this.body.SlotOf(dragItem))` with the proxy's own slot, the call is
  captured as `SwapSlots`, and the owner replays the native R8 sequence on its own body
  (`RemoteIntentApplier.ApplySwapSlots`, unchanged by this cycle). The batch re-drove `20261006-f`'s own
  recipe and read the operator's capture line and the owner's replay instead of the exception. It also
  produced the swap's monitor half, which is a finding of its own: the viewers converge, but through the
  periodic snapshot rather than an event (`todo/container-content-event-gap-on-repick.md`).
- **The R8 route's reachability is settled for `todo/drop-pending-single-slot-overwrite.md`.** Its owner-side replay is
  `Body.SwapSlots`, i.e. two `Body.DropItem` and — when both slots held an item — two `Body.PickUpItem` inside
  one frame (`Body.cs:1413-1428`) — but the whole call runs inside `SwapSlotsPatch`'s `InternalReorder` scope,
  so the two `Body.DropItem` calls report nothing, and the swapped items are direct slot occupants so
  `Body.PickUpItem`'s own `container.UnloadItem` is never reached either: the route registers NO departure at
  all (that ticket's point 1). It is this route's own row, not that ticket's producer.
- **Rows 7 and 8 are untouched**: the battery-load, combine and favourite halves and the medical row keep
  their `unproven` verdict and still need their fixtures.
- **Two adjacent shapes this cycle's independent review found are folded into the same change**: the
  wearable-drop report hook (`BodyItemPatches.DropWearablePatch`) now carries the display-proxy guard its two
  siblings always had — inside a bracket its own `GetWearable` read is answered by the displayed body, and a
  display proxy is never a local drop — and `PlayerCamera.UpdateWearables`' temporary body swap is restored by
  a finalizer as well as by its postfix, so a throw inside that call (it runs inside every release bracket)
  cannot leave `camera.body` on a clone and silently disarm the redirect this fix is built on. Both are
  recorded with their limits in the self-check.

Evidence: `docs/evidence/selfchecks/items/display-body-query-seam-selfcheck.md` (mechanism inventory, the
change, the verification table and its limits).

## Acceptance readings (batch `20261006-g`, 2026-10-06)

The batch re-drove `20261006-f`'s own recipe against the fix on the deployed artifact and added the control
rows this matrix was missing. Operator = physical-machine host, owner = sandbox guest, third peer = the
alternate sandbox:

1. **Row 6's swap half passes.** Releasing the owner's `bandage` proxy (id `9492956821`) onto the OCCUPIED
   slot 3 of its clone ends the release with `calls: "none"` and `dragAfter: "none"` (batch `20261006-f` read
   an exception and a drag that was never cleared); the operator logs
   `[RemoteIntent] SwapSlots captured for item 9492956821 … (container 0, slot 3 …)` and forwards it; the
   owner replays it — `[PickUpResult] emergencylight → slot (slot 0)`, `[PickUpResult] bandage → slot
   (slot 3)`, two `[SlotMoved] … (Swap) reported.` lines and `replayed native SwapSlots on item 9492956821`.
   The owner's own tree reads the two items exchanged. Re-driven once more with the third peer in the world,
   with the same five lines.
2. **The EMPTY-destination control still passes** (row 6a): the same proxy onto an empty slot classifies
   `PickUpToSlot`, the owner lands it (`[SlotMoved] bandage → slot 2 (Drag) reported.`, `[PickUpResult]
   bandage → slot (slot 2)`, `replayed native PickUpToSlot`), and no client warns.
3. **The swap converges on the viewers, but only through the periodic snapshot**: the operator and the third
   peer each log two `[CharSync] divergence … a carried move without an event sync` lines. The same two lines
   appear when the OWNER performs the same swap purely locally, so the intent replay is not their cause —
   filed on `todo/container-content-event-gap-on-repick.md` with the reading.
4. **Matrix row 2's transfer half (`TransferToBody`, the double-Tab take) was driven for the first time and
   passes**: with the operator's own ring open and the remote backpack view closed, one call drags the
   owner's proxy and releases it onto the operator — `TransferToBody captured … (container 0, slot 2, body
   76561198281246659 …)`, forwarded, and the custody fact moves on both sides (the operator's tree carries
   the item in slot 2, the owner's no longer carries it at all).
5. Rows 7 and 8 stay `unproven` because this session staged no fixture for them, as the status line records.

## Non-goals

- Not adding gameplay permissions beyond the existing host rules and the native inventory
  vocabulary.
- No per-frame item state stream and no client-side prediction of the owner's body.
- Not keeping any part of the clone-edit path alive "for compatibility": the pre-release policy
  applies, and a wire change bumps the protocol in the same change.
