# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only a deliberate cycle reset may touch multiple lines.

**Documentation-only cycles (added 2026-09-17)**: a cycle that changes no runtime or test behaviour
(backlog moves, evidence/citation updates, workflow documentation) still fills EVERY box and still
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
multi-line exception the paragraph above already allows for a cycle reset, applied to a cycle with no
implementation sequence to record; the BULK-checking prohibition keeps governing every cycle that
touches `src/`, `tests/` or `tools/`. `dotnet format` may be skipped (it only rewrites C#, and such a
cycle has none). The evidence run may use
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — but the focused
normative-gate run afterwards is NOT optional: it is the only thing that proves this checklist complete
before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new entry or control scheme: the change rides the give gesture's existing release (`PlayerCamera.HandleReleaseDragging` → `PlayerCameraDragUsePatch` → `RemoteDragIntentDispatcher.ReportLocalReleaseOntoProxy`), whose input sequence the ticket's Design (2026-10-22) records and batch `20261022-f` read with the real pointer; the outcome report adds no button, window or fallback — it is a message the same release already causes
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every added member has its reader in this change: `NetMsg.PlayerPlacementOutcome` → `PlayerPlacementOutcomeHandler` → `IPlayerInteractionControl.HandlePlacementOutcome` → `RemotePlacementService.HandleOutcome` (the `CommittedPlacement` comparison) → `GivenItemReturnService.Return`; the reporter side is `PlayerInteractionApply` → `SendPlacementOutcome` with the place `PlayerInventoryPlacement`'s `LocalPlacement` answered; `UnplacedGiveItemOrigin` → `ItemPatches.ItemOnDestroyPatch` (a destroy that is not the item's end, read next to `LimbAppliedItemOrigin` and attached only on the give's arm); `ClampReason` runs on the host's receipt path as well as the sender's; the committed record is taken by the one outcome that matches it, so a repeat can neither return twice nor match later; the decompiled bases are the ones the placement already cites (`Body.cs:1388-1410` the pickup gate, `Container.cs:110-130` `CanHoldItem`/`LoadItem`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the give family walked in one pass: the intent validation, the placement question and answer, the exact-destination commit, the failure CLAIM replaced by the outcome report (one message carrying both arms), the return consumer reading the same committed record, the census row SPLIT (applied arm bound by the committed place, not-applied arm named `unbounded` with this ticket), the sync-coverage row P14 (three messages, 7 anchors) and the guest→host direction list; the sibling `PlayerItemActionOutcome` is the pattern and still holds (one grant consumed by its report, refused by name, unanswered when no grant matches); the unplaceable handed-over item's END report was removed with a marker on the give's arm only, while a returned item that finds no place keeps its end report; and the host-side clamp of a member-authored reason was added where the deleted handler had it
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `PlacementAnswerTests` 12 cases (the answered slot recorded, the receiving-rules refusal, a destination that changed before custody, no resolver, a MATCH settling silently, a container give matching its container, a placement that landed elsewhere refused and returned, the not-applied return, an applied-but-place-less report refused and returned, an outcome from the wrong peer or for no committed give moving nothing, the host-side clamp of a control-character reason, and `TheTransferFlags_SurviveTheKernelProjection`); `GiveGestureGateTests`' shape pin (`LocalPlacement.NotPlaced`, `outcome = placed`, `PlaceCarriedItem(..., msg.ExactDestination)`, the `UnplacedGiveItemOrigin` READER in `ItemPatches`, `SendPlacementOutcome`, the injected resolver, the codec flags); `SourceShapeGateTests`' container-handover pin re-pointed at the `LocalPlacement` signatures and the suppressor flag; `SyncCoverageGateTests`' row P14 with its 7 anchors; the census row and the direction list
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green (6262 behaviour cases, 764 gate cases, `PlacementAnswerTests` included); L1 is OUTSTANDING and scheduled for batch `20261023-d`, immediately after this commit and its deploy: a real give's outcome reaching the host and matching the row it wrote (no deployed artifact could carry the message before this commit). Acceptance rows 2-3 are judged by the behaviour tests, because the 60 ms answer→apply window cannot be produced by hand (batches `20261022-i`/`-j`, the user's 2026-10-23 ruling)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket as the next work item and the ticket itself states the anchor the report needed; the user authorised taking over pointer and keyboard for the acceptance batch (2026-10-23: "在这期间你可以接管鼠标键盘"), and no NEW user-visible surface was added — the give gesture is unchanged, and the race arm now returns instead of substituting, which is the user's 2026-10-22 ruling already in force
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `PlayerInteractionApply` 571, `RemotePlacementService` 470, `PlayerInventoryPlacement` 253, `GivenItemReturnService` 118, `ItemPatches` 99, `LocalPlacement` 25, `UnplacedGiveItemOrigin` 20, all under 600; one top-level type per file (both new types are their own files); no state bool added; "one outcome per give" is held by consuming the committed record, not by a flag; the unplaceable destroy keeps ONE suppressor marker instead of a second report path, and the marker's READER is the hook that already reads its sibling — the first draft attached it with no consumer and the independent review caught it, which is why the reader is pinned by `GiveGestureGateTests` now; the existing debt entries (`GameAdapter` 631, `PlayerInventoryTakeService` 626) are untouched
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour suite 6262/6262 and normative gates 764/764 green (`full.log` in the local artifact area), `PlacementAnswerTests` (12) and the sync-coverage / census / direction / shape gates included
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
