# Remote inventory native parity — stage 2 container family self-check (2026-09-25)

Ticket: `docs/backlog/todo/remote-inventory-native-parity-rework.md` (stage 2). Design record:
`docs/architecture/remote-inventory-native-parity.md` (amended by stage 2). Decisions: 217 (stage 0),
218 (the window's shape, stage 1), 219 (the container family and the while-dragging body). Cycle
scope: the container-expansion gesture (R5), the nested-container and trash-bag paths, the restored
while-dragging body with its per-frame liquid drain tick, the open-container-window path and the
reported vanish case — with `ProtocolVersion.Current` bumped in the same change (36 → 37, the
handshake unchanged as the compatibility boundary).

## What landed (anchors are our own paths plus quoted text)

| # | Mechanism | Where it lives | What it does |
|---|---|---|---|
| 1 | Expansion batch | `Runtime/Session/PlayerInteraction/RemoteDragIntentCapture.cs`, "The native drag-window state machine, in two kinds" | an unload out of the dragged item's OWN container plus the load of that child into the hit container is ONE `MoveContainerChildren` intent; a bare child unload is refused |
| 2 | While-dragging bracket | `GameAdapter/Patches/PlayerCameraHandleWhileDraggingPatch.cs`, "The while-dragging frame of the remote backpack view" | the native body runs again per frame, the window brackets that frame, the radial menu stays anchored to the focused clone |
| 3 | Drain seam | `GameAdapter/Patches/RemoteDragMutationPatches.cs`, `RemoteDragLiquidDrainPatch` | the frame's `WaterContainerItem.Drain` is captured as the AMOUNT it removed and the local proxy call is skipped; a proxy no bracket took is refused with one line per dragged proxy |
| 4 | Drain tick intent | `RemoteDragIntentCapture.CaptureDrain` | one intent per native call, `amount` carried as the operand, non-finite or negative amounts refused |
| 5 | Owner replay | `GameAdapter/RemoteIntentApplier.cs`, "Owner side of the native inventory intents" | `MoveContainerChildren` enumerates the real children and runs the native per-child guard loop; `Drain` re-derives the distribution from the owner's own stack |
| 6 | Continuous reporting rule | `Runtime/Protocol/Messages/RemoteInventoryIntentFrequency.cs`, "Trigger frequency of the native intent vocabulary" | a continuous kind skips the per-intent immediate character re-report and logs at Debug; both are the same rule in one place for the viewer, the host and the owner |
| 7 | Host validation | `Runtime/Session/PlayerInteraction/PlayerRemoteInventoryIntentService.cs`, "The operands each intent kind needs" | the batch needs a resolvable target container that is not the source; the drain needs a finite, non-negative amount |
| 8 | Outcome kind | `Runtime/Session/PlayerInteraction/RemoteDragOutcome.cs` + `RemoteDragWindowKind.cs` | the outcome carries which bracket produced it, so an idle while-dragging frame is never reported as an unclassified gesture |
| 9 | Pickup-gate answer | `Runtime/Session/PlayerInteraction/RemoteDragIntentCapture.cs`, "AnswersPickupCheckFor" and "CapturesContinuousCallsFor" | one pure predicate answers the drag pipeline's own feasibility gate for the dragged proxy in BOTH bracket kinds, and only a capturing frame takes the continuous call |

## Findings that amended the frozen design (recorded, not silently absorbed)

| # | Finding | Evidence | Amendment |
|---|---|---|---|
| 1 | The drain tick's gate reads the LOCAL body (`this.body.DoPickupCheck(this.dragItem, true)`) and the LOCAL drag item, so restoring the while-dragging body needs the same display-body answer a release bracket has | `PlayerCamera.cs:1729` (the tick's four-part gate), `Body.cs:1356` (the distance/line-of-sight check) | design §3.2.7: the while-dragging frame is its own bracket kind and the pickup check is answered for the dragged proxy |
| 2 | The favourite toggle is a FIELD store on the HOVERED item, not a call | `PlayerCamera.cs:1745-1747` (`invButton.GetItem().favourited = !…`), `Item.cs` (`favourited` is a public field, no property) | design §3.2.7 and §6: the frame that would write it is skipped with one line, and the store-level seam arrives with the intent |
| 3 | The native tick hands over a per-stack LIST computed from the viewer's proxy stack, which is not the owner's stack | `WaterContainerItem.cs:176` (`Drain(List<float>)`), `:144` (`CalculateDrain` distributes by the caller's own stack) | design §3.3: the intent carries the amount, and the owner recomputes `CalculateDrain(amount)` on its own stack |
| 4 | The per-intent immediate character re-report is a full snapshot, so a per-frame intent would broadcast sixty of them a second | `GameAdapter/Character/CharacterDataSync.cs`, "An inventory-internal move finished … re-report right away" plus `ReportCharacterData`; the periodic path already carries un-evented item state (`GameAdapter/Items/ItemReconcile.cs`, "the top-level item state that is not covered by a dedicated event") | decision 219 and design §6: a continuous kind does not trigger it, and its happy path logs at Debug |
| 5 | The outcome's unclassified-gesture rule is a RELEASE rule; a while-dragging frame that produced nothing is just an idle frame | `RemoteDragOutcome.IsUnclassified` (release-only meaning) versus one bracket per frame | the outcome carries its bracket kind, so the rule cannot fire per frame |
| 6 | R5's target and source are one native loop over the dragged item's own container, so the child identity is only needed to pair the calls, not on the wire | `PlayerCamera.cs:1585-1593` (`this.dragItem.container.UnloadItem(item3, null)` then `container.LoadItem(item3)`), `Item.cs:60` (`container` is the item's OWN container component) | design §3.2.4 and §3.3: one intent, children enumerated on the owner |

## Decisions taken in this stage

| Decision | Statement | Where it lands |
|---|---|---|
| Batch granularity | R5 is one intent; the owner's `CanHoldItem` decides per child, so the native partial outcome is reproduced | design §3.2.4, decision 219 |
| Drain granularity | one intent per native call (one per frame), carrying the amount — no cross-frame merge, which would move the native timing | design §3.2.7, decision 219 |
| Drain reporting | the drained state rides the periodic character snapshot; no per-frame full re-report, Debug level on the happy path | design §6, decision 219, `sync-coverage-matrix.md` row P10 |
| Favourite | refused with one line per dragged proxy; the store-level seam and the intent belong to the item-interaction stage | design §3.3/§3.6/§6, ticket stage 3 |
| Container window | re-checked against the restored branch: `PlayerCamera.OpenContainer` tracking and the rebuild-time re-bind are keyed by the authoritative instance id, so they stay valid; the window's contents ride the projection | design §6, ticket stage 2 |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- Test-expectation red observed before the fix was accepted: with the batch and the drain capture
  reverted to the stage 1 shape (a refusal, and a store-level "not carried"), the focused run failed
  **5/61** — `ContainerChildBatch_IsOneMoveContainerChildrenIntentForTheWholeGesture`,
  `TheDrainTick_IsOneIntentCarryingThatFramesAmount`,
  `AWhileDraggingFrameKeepsItsDrainWhenAReleaseCallSlipsIn`,
  `ADrainOutsideAWhileDraggingFrame_IsRefused` and
  `DrainTick_IsForwardedToTheOwnerWithItsAmount` — and the probe was removed again before the run
  below (output: `%TEMP%\cuo-red-stage2.txt`; the probe was a working-tree-only edit, absent from the
  commit).
- Focused after the review round: `RemoteDragIntentCaptureTests` (33 cases: the coalescing rules, the
  two-intent take-out-then-move order, R9 absorption, the one-intent expansion batch and its three
  refusal shapes, the pickup-gate answer in both bracket kinds, the continuous-call predicate, the
  frame's drain amount, the unusable amounts, the release-only call inside a frame, the slot-0/limb-0
  operands, the classified no-ops, the redirect decision, the reset between brackets),
  `RemoteInventoryIntentWireTests` (12: the two 0-legal operands, the amount round-trip including
  zero, the batch operands), `RemoteIntentRequestTests` (19: host-owner delivery, guest-owner forward,
  the batch forward and its self-target refusal, the drain forward, the unusable amounts, the
  container take-out-and-back leaving the host's copy alone, plus the stage 1 rows) — 64/64 in one run.
- Normative gates: 148/149 while the checklist was open (the only failure is
  `DeliveryChecklist_NoIncompleteRequiredBoxes`), and 149/149 on the final focused run after the boxes
  were closed; two gate baselines were updated by this change because the shape they pin genuinely
  moved: the `IRemoteBackpackPatchBridge` seam census in `PatchBridgePortShapeGateTests`, and the two
  `ProtocolVersion.cs` quotes in `docs/evidence/sync-coverage-evidence.json` (rows W1 and I6) that the
  version bump invalidated.
- Full suite **with build** after the review fixes: 3877/3877 (`dotnet test CasualtiesUnknownOnline.slnx
  --filter "FullyQualifiedName!~DeliveryChecklist"`), 148/148 for the gate project in the same run.
- Touched classes stay well inside the 600-line structure gate (largest:
  `RemoteDragIntentCapture.cs` 423 lines; `RemoteDragMutationPatches.cs` 332;
  `PlayerRemoteInventoryIntentService.cs` 308; `RemoteIntentApplier.cs` 300), and this cycle deleted a
  mechanism rather than adding a parallel one: the stage 1 batch refusal and its `_containerBatchRefused`
  latch are gone, replaced by the batch intent.
- The expansion batch, the drain and the container-window path have no unit-testable seam beyond the
  window state machine and the host contract: the owner-side replay and the patch layer need a Unity
  scene, so they are covered by the pure state machine, the host contract tests and the code facts
  above.

## Independent adversarial review of this stage (same commit)

A fresh-context reviewer audited the frozen working tree against the design record, the game's own code
and the suites, and independently reproduced the build and the focused run. Report:
`%TEMP%\cuo-review-remote-inventory-stage2.md`. First-pass verdict: **1 blocker / 2 majors / 8 minors**,
with the R5 replay, the drain arithmetic, the report-hook guards and the wire handling of `Amount`
confirmed sound. What came back, and what happened to each:

| Finding | Outcome |
|---|---|
| B1 — the drain tick's `DoPickupCheck` gate "can never pass", because the patch's guard is "always true" inside a while-dragging bracket | **Refuted, and the rule is now code that a test pins.** The guard is `!IsOpen \|\| !IsProxy(item) \|\| InstanceId(item) != DraggedItemId`; for the dragged proxy inside an open bracket all three clauses are false, so the patch sets `__result = true` and skips the native distance test — in the frame bracket exactly as in the release bracket (an inverted reading of that polarity is what produced the finding). The decision moved out of the seam into the pure `RemoteDragIntentCapture.AnswersPickupCheckFor`, covered by `ThePickupGate_IsAnsweredForTheDraggedProxyInBothBracketKinds`, so nobody has to re-derive it |
| M1 — a proxy with no authoritative identity ran the native `WaterContainerItem.Drain` on the display proxy (the release path fails closed for that exact shape) | Fixed: the seam now asks `CapturesContinuousCallsFor`, and a proxy no while-dragging bracket took has its drain call refused with one line per dragged proxy while the native body keeps running for its own feedback, so no path mutates a proxy (`OnlyAWhileDraggingFrame_CapturesTheContinuousCalls`) |
| M2 — the favourite refusal skips the frame, so that frame's drain tick is lost silently | Recorded rather than engineered around: it is one frame per key press, the refusal line now names the cost, and design §6 records it as the deliberate price of stopping a field store Harmony cannot intercept (the store-level seam arrives with the intent) |
| m1 — a two-target batch still emitted its intent while logging a refusal | The batch is refused whole (`_batchTargetConflict`); `ContainerChildBatchReachingTwoTargets_IsRefusedWhole` asserts no intent leaves |
| m2 — the batch applier counted `moved` without verifying the write, and `Container.LoadItem` refuses silently | The count is taken from the scene (`child.transform.parent == target.transform`), so the line reports verified moves and the rest as refused — the "report only verified writes" rule |
| m3 — the checklist's mechanism-inventory evidence declared "8 rows" and enumerated a mismatched anchor list | The evidence suffix now names the self-check's table instead of a carried-over count |
| m4 — two checklist boxes were still unchecked at review time | They are the cycle's own record and are closed with their evidence before the commit |
| m5 — a competing continuous intent logged at Information while the drain held the lease | The refusal follows the frequency rule (Debug for a continuous kind), and the lease consequence is recorded in design §6 |
| m6 — the operator's projection can lag the drain by up to 1 s | Already recorded in design §6 and matrix row P10; the reviewer confirmed it as a declared trade-off, not an overclaim |
| m7 — `CaptureDrain` cited `:1732` for the tick | Anchors made precise: `:1729` is the gate, `:1731` the call |

## Limits

- No runtime evidence: no two-client session, no frame-level verification of animation, sound or
  input interception. The drain's visible cadence on the operator's screen (`1 Hz` projection steps
  against the owner's continuous drain), the expansion gesture's feel and the container window's
  behaviour are the user's release-cycle acceptance.
- Stage 2 refuses, observably, the gestures whose intents arrive in stages 3-4 (use/wear on the radial
  centre, combine, battery, the favourite toggle, the trader hand-in).
- The radial weight readout still prints the local body's encumbrance while the remote ring is open
  (design §6): a display detail left for the stage 4 family audit.
