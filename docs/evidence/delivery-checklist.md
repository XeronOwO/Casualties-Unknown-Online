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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the 2026-10-22 give gesture's input sequence (own backpack → right-drag → the target's range opens their backpack → release in range = USE / on the displayed backpack = GIVE / elsewhere = drop) walked through `PlayerCamera.HandleDragging`'s start→release→while-dragging order and `InvButtonBodyPatch`'s ring swap; the one give resolution is `DragGiveTargetResolver`; the independent review's A0 confirmed the sequence performable; the user's 2026-10-22 ruling is the design and unit 2's no-room rule stays recorded in the ticket
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: each added reader is named in the ticket's "The independent review's findings": `DragTargetSelection` → `CrossPlayerDragUse.Observe` (release and cue), `DragGiveTargetResolver` → the release patch and `DragUseCueTracker`, `AutoOpenRemoteBackpackForDrag` → `PlayerCameraDragGiveOpenPatch`, `RemoteBackpackView.AutoOpened` → the release finalizer, the seam's `bool` answer → `__state.Give`, the open's anchor/`UpdateWearables` calls → the ring and wear strip the native activation path would otherwise build; the decompiled bases are `PlayerCamera.cs:1353-1364`/`:2778` (the drag order and `UpdateWearables`) and `InvButton.cs:159-165` (the per-frame ring re-read); the cue fix's readers are `IDragUseCueState` → `Plugin.DrawDragUseCue` (the caller batch `20261022-f`'s row 9 found missing), `DragUseCueTracker.Clear` → `GameAdapterBridge.OnSceneLoadBegin`/`GameAdapterSessionBinding.OnSessionEnded`, `IsModalSurfaceOpen` and `SessionActive`/`LocalInWorld` → the same draw guard, and the game's own `iteminteract` bind → `DragUseCuePolicy`'s held-release fact
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the drag family walked in the independent review — the give, the in-range use, the native drop, the panel entry, the cue, the automatic open, the close rules, the view-only/deny arms and the proxy take — and its findings fixed in this commit: M1 (the cue could promise a use where the release gives), M2 (a refused give kept the drag-opened backpack), m1 (a spent open attempt), m2 (the wear strip on an opened view), m3 (the close flag outliving its drag), all recorded in the ticket; the machine batch then found the cue was decided but never drawn, and two follow-up review rounds aligned the whole label family in the same commit — the drawer, the name resolution, the localization, the stale-state teardown, the missed release, the two-axis clamp and the gate — with the two limits it cannot close recorded on the cue ticket (duplicate display names, the modal-stuck drag)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the independent review's twelve findings (4 major, 8 minor) are recorded one by one in the ticket's "The independent review's findings" with the code each named and its disposition, and the follow-up review's two rounds (10 + 6 findings) are fixed or recorded with their evidence on the cue ticket; what a source or pure test can hold is pinned: `GiveGestureGateTests` (4 cases: give-before-use, the seam's answer, one cue/release resolution, the matcher contract), `DragUseCueGateTests` (4 cases: the draw chain, its matcher contract over the four refused shapes, the teardown/bind shapes, and the comment/literal scan surface), `DragUseCuePolicyTests` (7 cases, including the released bind), `DragTargetSelectionTests` (9 cases), `RemoteBackpackContractTests.PatchBridge_ExposesTheReleaseWindowSurface`; the one thing neither project can reach — the Plugin-only display-name rule — is recorded as the cue ticket's limit with the Runtime seam that would make it testable; the runtime rows are the ticket's 1/2/5/7/8/9/10
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green above (6247 behaviour cases, 763 gate cases, including `GiveGestureGateTests` and the new `DragUseCueGateTests`); L1 is the ticket's acceptance rows 1, 2, 5, 7, 8, 9 and 10 read on the deployed artifact with two clients and a real right-button drag (batch `20261022-f`): rows 1, 2, 5, 7 and 8 passed, row 9 failed because the label was never drawn, and row 10 was unproven behind it; this commit is row 9's fix, so rows 9 and 10 are re-read on the deployed artifact after it (a short two-client batch), and rows 3, 4 and 6 and the ledger rows are unit 2 and stay owed in `todo/placement-outcome-never-returns.md`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-22 rulings answered the gesture (including the "the item stays with the giver" rule) after the previous options were rejected, and the handoff names this ticket as the round's work item; the user's design text is recorded verbatim in the ticket's "Design (2026-10-22)" section
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `GameAdapterBridge` 622 (the scene-load clear; recorded in `docs/architecture-debt.json` with the reason), `OnlineUiHost` 552, `RemoteBackpackView` 329, `Plugin` 327, `CrossPlayerDragUse` 264, `RemoteDragIntentDispatcher` 240, `GameAdapterSessionBinding` 194, `PlayerCameraDragUsePatch` 184, `DragTargetSelection` 157, `DragGiveTargetResolver` 82, `DragUseCueTracker` 70, `DragUseCueOverlay` 54, the two small patches 30/27, `DragGiveTarget` 13; no class over 600 unrecorded; one state flag (`RemoteBackpackView._autoOpened`, cleared when its drag ends) plus the gesture's `_dragActive`/`_openedFor`, and the fix's plugin fields are optional-port references and services, not state; the review's dead-surface findings (unused position fields, an over-wide `EndDrag`, a one-value setter) are deleted in this commit
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; behaviour suite 6247/6247 and normative gates 763/763 green (`test-full-cue3.log` in the local artifact area), `GiveGestureGateTests` (4) and `DragUseCueGateTests` (4) included, with the new gate's red on the pre-fix sources recorded in `test-cue-gate2-red.log`
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
