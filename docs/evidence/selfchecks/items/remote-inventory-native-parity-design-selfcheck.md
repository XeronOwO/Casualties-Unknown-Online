# Remote inventory native parity — stage 0 design self-check (2026-09-21)

Ticket: `docs/backlog/todo/remote-inventory-native-parity-rework.md` (stage 0, decision 217). Cycle
scope: freeze the design that replaces the clone-edit remote-inventory path — the native gesture
inventory, the wire intent vocabulary, the ticket adjudication. **No behaviour changed in this
stage**: no `src/` or `tests/` file was touched.

Design record: `docs/architecture/remote-inventory-native-parity.md`.

## Mechanism inventory (static evidence)

Every row was read from the decompiled game; the anchors are `reversing/` paths plus line numbers
(that tree is never edited, so they are stable).

| # | Mechanism | Evidence (game) | Design consequence |
|---|---|---|---|
| 1 | The drag has three phases, driven by the `iteminteract` bind | `PlayerCamera.cs:1353` (`HandleDragging`), `:1367` (start), `:1456` (release), `:1713` (while dragging) | the capture seam must cover the while-dragging phase too, not only the release |
| 2 | The release is a hit-test dispatch that stops at the first consumed raycast cast, then falls back to world actions | `PlayerCamera.cs:1500` (`TryPerformUIActions`), `:1686` (`TryPerformWorldActions`) | CUO must not own an ordering table: the native dispatch is the classification |
| 3 | Two of the operations CUO classifies at release are native while-dragging actions: the drain tick and the favourite-toggle key | `PlayerCamera.cs:1729` (drain per frame), `:1745` (favourite on the hovered item) | "pour" and "favourite" are captured while dragging; a release-only design loses both |
| 4 | The battery branch is selected by the *target's* battery component and the dragged item's tags, not by a CUO enum | `PlayerCamera.cs:1543` (unload), `:1548` (load) | one intent per native call, direction and operand come from the call |
| 5 | The container move is a native pair (`UnloadItem` then `LoadItem`), and the same pair is used by four different branches | `PlayerCamera.cs:1567`, `:1589`, `:1668`, `:1707`; the lone `UnloadItem` take is `:1691` | the release window buffers and coalesces the pair into one `MoveIntoContainer` intent |
| 6 | Slot placement is `SwapSlots` when both sides are held, otherwise `DropItem` + `PickUpItem(item, slot, false)` | `PlayerCamera.cs:1616`, `:1623`, `:1629` | `force: false` is part of the native call; the current owner-side apply uses `force: true` and so bypasses native guards |
| 7 | The native mutation guards read the local scene: container weight/tag/stacking and a 10-unit distance, slot rules, held-parent shortcut | `Container.cs:110`, `:116`, `:154`; `Body.cs:1356`, `:1388`, `:1413` | the guard must run where the objects are: the owner's client executes, the host never models contents |
| 8 | The current implementation executes one operation in two worlds | `Runtime/Session/PlayerInteraction/PlayerRemoteInventoryService.cs` (mirror edit), `GameAdapter/RemoteInventoryOperationApply.cs` (native apply for seven kinds), `GameAdapter/Patches/PlayerCameraDragUsePatch.cs` (CUO ordering) | the mirror edit is the root cause of the reported vanish and the asymmetry; it is deleted, not patched |

## Delta the design closes

| Reported / current shape | Native shape (evidence) | Where the design answers it |
|---|---|---|
| A gesture the CUO ordering does not name does nothing and reports nothing | every consumed release either mutates or is an explicit native no-op (`PlayerCamera.cs:1536`, `:1609`) | §3.2 fail-closed window + unclassified-release log; §3.3 vocabulary covers every mutating branch, and R1/R7 are the two classified no-ops |
| Mirror edit is overwritten by the next authoritative report | the game's own call is the only writer of the item tree | §3.1 owner executes; §3.6 clone-edit path deleted |
| Two directions behave asymmetrically | the native pipeline is direction-agnostic | one vocabulary, one executor, one authority rule |
| Host models the owner's inventory contents | the host only validates and arbitrates | §3.1 host row; §3.3 host records the resulting fact |

## Decisions recorded in this stage

| Decision | Statement | Where it lands |
|---|---|---|
| Capture seam | the viewer runs the native gesture path; CUO intercepts the mutation calls inside a release window opened by the drag patch | design §3.2 |
| Vocabulary | the wire carries the native call identity plus instance-id operands, never a CUO taxonomy and never a mirrored result | design §3.3 |
| Ownership | owner executes with its own guards; host validates, arbitrates first-writer-wins and records; viewer projects | design §3.1, §3.4 |
| Protocol | the vocabulary replaces the operation enum -> the wire changes -> the protocol is bumped in the same change (stage 1) | design §3.5 |
| Ticket adjudication | the two projection tickets stay landed; the rejected parity ticket and the projection acceptance ticket are absorbed into the rework ticket | design §5 |

## Verification (static, this stage)

| Claim | How it was checked | Result |
|---|---|---|
| Every release branch has an anchor | read of `PlayerCamera.cs:1320-1808` plus the callee bodies (`Container.cs:105-194`, `Body.cs:1356-1425`, `PlayerCamera.cs:42-180`, `:736-763`, `:2765-2844`) | 14 UI branches + 4 world branches, each with the call it makes |
| The CUO operation enum covers neither the native branch set nor the native directions | read of `RemoteInventoryOperationKind.cs` (12 kinds) against the branch table | 3 divergences named in §2.2 and the delta table |
| The owner-side apply bypasses native guards | read of `RemoteInventoryOperationApply.cs` (`PickUpItem(source, targetSlot, force: true)`) against `PlayerCamera.cs:1629` (`force: false`) | recorded in the delta table |
| No behaviour change in this stage | the cycle touched documentation only | `git status` shows no `src/` modification |

## Adversarial review round

An independent reviewer with a fresh context and shell access checked the design record against the
decompiled tree (full report: `%TEMP%/cuo-review-remote-inventory-design.md`). It verified 61 anchors:
60 matched exactly (line, method, condition and callee), and it confirmed the branch inventory is
neither missing a mutation nor duplicating one, the ownership model agrees with
`review/remote-interaction-local-gating.md`, and the four ticket verdicts are supported.

The findings were real and are fixed in the design record:

1. The battery branch was described as target-selected; the direction is chosen by the DRAGGED item's
   tags while the target only has to carry a battery component (§1).
2. The design depends on the native branch running, but three shipped patches hold it shut —
   `PlayerCameraDragUsePatch` (the ordering table), `PlayerCameraHandleWhileDraggingPatch` (the whole
   while-dragging body, hence the missing native drain and favourite) and
   `PlayerCameraTryPerformRadialActionPatch` (R10), plus `RemoteProxyDragPolicy`. §3.6 now names them
   and assigns each removal to a stage.
3. The owner-side apply path (`RemoteInventoryOperationApply`, `RemoteInventoryApplyMsg`) was missing
   from the deletion list even though §5 calls the ticket that produced it absorbed; §3.6 names it.
4. The window is now stated as a call bracket (one invocation), so a projection rebuild — which itself
   calls `Container.LoadItem`/`UnloadItem` on clones — cannot be captured by construction.
5. The coalescing rule is now explicit that it merges only same-container pairs, because the native
   world fallbacks run as independent `if`s and a take-out followed by a move into another container
   must stay two intents (otherwise the take-out would become conditional on the second container
   accepting the item, which the native code never does).
6. `TransferToBody` now carries R9's occupying-slot step, without which the destination's
   `Body.PickUpItem` refuses whenever the requester is holding something — the usual case.
7. Smaller corrections: `MoveContainerChildren`'s source operand is the dragged item's own container
   component, the host is named as the role that refuses a non-owner, non-requester destination, and
   the matrix gained the craft-screen/container-window row.

## Limits

- This stage produces no runtime evidence and fixes nothing: the reported reproductions (guest opens
  the host backpack and cannot operate items; the trash-bag item vanishes) are still live until
  stages 1-2 land and are re-tested on the deployed build.
- The mechanism inventory covers the `PlayerCamera` drag pipeline. The medical-panel and
  context-menu families reach the same native mutation calls through their own UI paths and are
  audited in stage 3 against the same seam, not assumed covered.
- The native-feel half (animation, sound and timing on the operator's screen) is the user's
  release-cycle acceptance; the automated suite can prove the intent mapping, the ownership rule and
  the refusal observability, not the feel.
