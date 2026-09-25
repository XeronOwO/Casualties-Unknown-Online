# Remote inventory operations: run the native path end to end

- Status: Todo
- Priority: Critical
- Category: Remote inventory / native interaction parity / architecture rework
- Source: User acceptance findings (2026-09-21) plus the same day's ruling: operating another player's items must feel exactly like operating one's own — the same functions, the same item animations, the same UI feedback and the same sounds. The current implementation is rejected as a whole and is to be replaced, not patched again.
- Related: `resolved/remote-backpack-native-interaction-parity.md` and `resolved/remote-backpack-item-projection-acceptance-issues.md` (the rejected deliveries this ticket replaces, absorbed here in stage 0), `review/unified-remote-display-projection-rework.md`, `review/global-projection-framework.md`, `review/tab-backpack-open-close-immediately.md`, `review/guest-container-contents-ghost-drops-on-host.md`
- Design: `docs/architecture/remote-inventory-native-parity.md` (stage 0 record, decision 217)

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

## Staged plan

- **Stage 0 — design.** Done, see above.
- **Stage 1 — the operation path.** Owner-side execution plus host validation for the inventory and
  slot family (move/swap, transfer to the requester, drop), replacing the clone-edit path.
- **Stage 2 — containers.** Move into and out of a container, nested containers, the trash bag,
  pour, and the open-container-window gesture; the vanish case is a regression test here.
- **Stage 3 — item interactions.** Use/wear/combine/battery/favourite and the held-remote-item
  chains (close the backpack, then use the held item from the medical panel).
- **Stage 4 — family audit and acceptance.** Both directions, a third peer, worn items, containers
  and the craft screen against the matrix below; the projection tickets re-evaluated.

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
| 15 | Craft screen from a remote item, and opening a remote container's window | Native local UI on the viewer: no intent, no host round trip, and no proxy mutation |

The two absorbed tickets' remaining rows are covered by rows 3-9 above: the trash-bag take-out and
re-insert vanish (rows 3-4), pour and edge drop (row 5), main-hand and other slot placement (row 6),
double-Tab transfer to the requester (row 2 with `TransferToBody`), and the held-remote-item medical
chain (row 8). Their original text stays in git and in the two `resolved/` records.

## Verification

The implementation cycle adds tests for the intent -> native-call mapping and keeps the item/kernel
suites green; the deployed-artifact identity is checked. The operating feel on the requester's
screen and the two-client behaviour are the user's release-cycle acceptance. Rows 1-4 of the
reported behaviour must be re-run on the deployed build until they no longer reproduce.

## Non-goals

- Not adding gameplay permissions beyond the existing host rules and the native inventory
  vocabulary.
- No per-frame item state stream and no client-side prediction of the owner's body.
- Not keeping any part of the clone-edit path alive "for compatibility": the pre-release policy
  applies, and a wire change bumps the protocol in the same change.
