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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new entry or control scheme: this change is inside the give gesture's existing release (`PlayerCamera.HandleReleaseDragging` → `PlayerCameraDragUsePatch` → `RemoteDragIntentDispatcher.ReportLocalReleaseOntoProxy`), whose input sequence the ticket's Design (2026-10-22) records and batch `20261022-f` read with the real pointer; the placement question happens AFTER the release and asks the receiving client's own rules — no button, window or fallback is added, and the answer it can give is the destination those rules resolved
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every added member has its reader in this change: `NetMsg.PlayerPlacementQuery` → `PlayerPlacementQueryHandler` → `IPlayerInteractionControl.HandlePlacementQuery` → `RemotePlacementService.HandleQuery` → `IRemotePlacementResolver.Resolve` (the adapter's `RemotePlacementResolver`, asking `LocalBodyPlacement.WouldAccept` / `Container.CanHoldItem`); `NetMsg.PlayerPlacementAnswer` → `PlayerPlacementAnswerHandler` → `HandlePlacementAnswer` → `Commit` → `PlayerInventoryTakeService.HandleRemoteGiveToBody(..., exactDestination: true)`; `NetMsg.PlayerPlacementFailed` → `PlayerPlacementFailedHandler` → `HandlePlacementFailed` → `GivenItemReturnService.Return` (the reverse snapshots + the returned transfer); `PlayerInteractionApply`'s exact branch → `CanTakeExactDestination` (the same resolver port, so the answer and the check cannot drift) → `SendPlacementFailed`; `RemotePlacementService.ResetSessionState` → the `ISessionReset` subscription in `PlayerInteractionService`; the pending and committed tables are dropped at the session end, and the test-only `PermissivePlacementResolver` names itself as a stand-in; the decompiled bases are `Body.cs:1388-1410` (the pickup gate the answer runs) and `Container.CanHoldItem` (already cited by `ItemContainerFit`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the give family walked in one pass: the intent validation (unchanged, still first), the placement question and answer, the exact-destination commit and its refusal, the receiver's resolution sharing `LocalBodyPlacement.WouldAccept` with the placement loop (so the answer and the placement cannot drift), the refusal riding the existing `InventoryIntentRefused` wire, the kernel transfer, and the design's residual — the receiving client's exact branch reporting instead of substituting, the failure frame, and `GivenItemReturnService` moving the row back to the giver; no arm is left on the old substitution path except the take direction's landed rule
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `PlacementAnswerTests` (6 cases: the answered slot recorded, a receiving-rules refusal leaving the item with the giver, a destination that changed being refused before custody, no resolver refusing rather than falling back, the residual's return to the giver, and a failure report from the wrong peer or for no committed give changing nothing) plus the 35 existing intent tests through the harness's stand-in (`PermissivePlacementResolver`); the adapter's exact branch pinned by shape in `GiveGestureGateTests` (`msg.ExactDestination`, `CanTakeExactDestination`, `SendPlacementFailed`); `SyncCoverageGateTests`' row P14 with its 6 anchors; the census rows for `PlayerPlacementAnswer`/`PlayerPlacementFailed`; the wire direction classifications in the three direction-test lists
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green above (6256 behaviour cases, 764 gate cases, `PlacementAnswerTests` included); L1 is the machine rows this change owes and does NOT claim here: the give's unit-2 rows 3/4/6 and `placement-outcome-never-returns` rows 1-6 need the deployed artifact and two clients, and row 6 now has its code (the return) but no machine reading yet — the ticket stays in `todo/` until that batch is judged
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-22 ruling is the design this implements ("the receiving client answers before custody; the item stays with the giver when it cannot be placed"), recorded in both tickets, and no new user-visible surface was invented (the gesture and label work is the previous cycle's and unchanged)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `PlayerInventoryTakeService` 623, recorded in `docs/architecture-debt.json` with the split it owes (the forward custody alone; the return was moved out); `RemotePlacementService` 342, `GivenItemReturnService` 219, `PlayerInteractionService` 240, `PlayerInteractionApply` (the exact branch), `LocalBodyPlacement` 129, `RemotePlacementResolver` 93; one top-level type per file; no bool state flags; the session-scoped pending and committed tables implement `ISessionReset` and are dropped at the session end
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour suite 6256/6256 and normative gates 764/764 green (`unit2b-test-full.log` in the local artifact area), `PlacementAnswerTests` (6) and the sync-coverage / member-report / direction-classification gates included
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
