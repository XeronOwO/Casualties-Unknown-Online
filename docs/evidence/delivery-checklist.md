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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture, no new wire member; the change re-points the TWO existing item entries at their native call sites and separates them by entry. Inventory use = `PlayerCamera.TryPerformRadialAction` → `Body.UseItem` (`PlayerCamera.cs:1636-1651`, `Body.cs:2475-2481`; `Body.WearWearable` beside it for a wearable); wound view = `TryPerformSpecialUIAction`'s `WoundViewLimb` branch → `ApplyWoundItem` → `useLimbAction`/`ApplyToLimb` (`PlayerCamera.cs:1654-1660`, `739-763`). The limb the inventory gesture cannot carry stays refused (its entry asks no limb rule; a limb family with no limb is refused), and the user-visible change (the world drag drinks a dual-class container) is the ticket's own acceptance row under the user's handoff; the wound-view half the ticket claimed was disproved against the native call site and filed as `topical-limb-action-shape.md`, not recorded as a limit
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `What landed` and the selfcheck's mechanism table read each of the eight: the two entries' call sites (above); the container both entries admit (`saline` `usable`+`usableOnLimb`, `useAction` = `Drink(body, 100f, "drink")`, `Item.cs:1844-1874`, refillable through `LiquidTransfer.Finish` → `Body.CombineLiquids`); the classifier over the four content seams; the host's dispatch and commit (`PlayerItemUseExecution`, `PlayerItemUseCommit`); the operator's gate/measurement (`LocalUseItemEligibility.FamilyOf` with the entry); the auto-select (`CarriedItemUseTree`); the wire (unchanged, entry derived from the limb selection); the affected side (unchanged, dispatches on the result's dose/key fields). What a source cannot settle — the live drag, the wound-view release — is the acceptance rows plus the two filed shapes
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every one-shot routing site walked — the host's dispatch and auto-select, the world-drag gate and its cue (`DragUseCueTracker` asks the same `IsUseItem`), the medical view's two dose measurements, and the three predicates the medical gate keeps (`IsInjectableRemoteItem`/`IsTopicalRemoteItem`/`IsDrinkRemoteItem`, deliberately kept for the unmigrated chains). The independent review found the limb-tool gate had lost its adapter-side scan (restored in this commit) and two false claims (the wound-view topical reachability, "a blood bag's action is never measured"); both claims are corrected here and the shapes filed as `topical-limb-action-shape.md` and `drink-measurement-destructive-use-action.md`
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/players/topical-live-stack-family-order-selfcheck.md` — eight mechanisms audited and seven self-check rows, each naming a test or gate (the red log, the 11 classifier cases, the auto-select case, the four chain gates, the full-suite run), with the two filed shapes and the auto-select consequence in its `Limits`
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the red→green pair for the world-drag case (`.acceptance/red-t1.log`: 1 failed / 1 passed on the pre-fix tree; `.acceptance/focused-t4.log`: 38/38 after) plus the 11-case classifier table and the 26 chain gates, with the full behaviour suite 6069/6069 and the 751 normative gates. At runtime the acceptance batch drives the production release (`remote-gesture mode=release` with the peer under the pointer) and reads the host/affected logs, the item's state and the target's body; the wound-view row drives `remote-medical-release` and reads that no drink is committed. What the batch cannot produce is named in the ticket's `Limits`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's handoff (2026-10-19/20, this session's prompt) names this ticket as the round's first work item; decision 246 is the settled rule the ticket executes and the ticket's acceptance row is the requirement. The correction of the ticket's wound-view sentence is a factual correction against the native call site (it could not happen), not a new design: the two filed shapes are the design questions that remain
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the final edit — `PlayerItemUseService` 241 (was 632 and over the line gate), `PlayerItemUseExecution` 406, `ItemUseFamilyClassifier` 113, `ItemUseEntry` 32, `ItemUseFamily` 61, `LocalUseItemEligibility` 223, `CarriedItemUseTree` 185, `CrossPlayerDragUse` 143, `ItemUseTests` 662, the classifier tests 179, `TopicalChainGateTests` 133. No new boolean state; the adapter's `Family`/`FamilyOf` order and the host's second order are DELETED (one classifier), and every mechanism added has its reader named (the four routing sites; the auto-select's exclusion has its own case)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/format-t1.log`); `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/build-final1.log`); the normative gate project 751/751 (`.acceptance/gates-final2.log`, run AFTER the doc-census/index edits, so it includes this checklist gate and the documentation gates); the full behaviour suite 6069/6069 (`.acceptance/full-final1.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
