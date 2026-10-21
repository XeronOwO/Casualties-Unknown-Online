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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture or UI: the change repairs two existing session facts (a member's start-gate release, a host's run phase) and one enemy identity input; the native surfaces it touches are the game's own loading screen (`LoadingScreenKeeper` over `WorldGeneration`'s load object) and the enemy creation/roster path, and no action is re-routed between entries; each fix has exactly one correct behaviour, so no user-visible design choice was introduced
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## Root cause and fix` and the selfcheck §4 name each touched mechanism with its reader: `TryGetEnemyFacts` → `TryAdoptNewRows` → `FindLiveCopy`; `WorldStartGate.LateJoinerPasses` → `NotifyMemberInWorld`/`AnswerRepeatInWorld`; the `SetHostRunPending` per-run reset; `StartGateCoordinator.ResumeHostPlayingIfTheSessionRoseAroundTheRun` → `MarkPlayingForHost` → `LoadingScreenKeeper.ShouldKeep`; the Unity half is the batch, not claimed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every start-gate verdict path (arrival, repeat, arm, release, per-run reset, session end) and both live-pass identity inputs were walked; the independent review's six questions enumerated fresh entry/layer, mid-run activation, reconnect, host-left-world, lobby switch and a first report while armed; the one family member left untouched is the guest buffer's now-unused `RuntimeSpawned` (kept: it mirrors the row and the host publishes from it)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/enemies/enemy-roster-adoption-selfcheck.md` §4 (both behaviour halves, the review's major/minor and its RED/GREEN) and §5 (limits, each with its reader)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is `WorldStartGateTests` (4 session cases: the release, its swallowed repeat, the armed guard, the per-run reset), `EnemySyncServiceTests.KernelFact_SuppliesBothLiveRowIdentityFields`, the source-shape pins (the live-pass hand-off, the host-resume pump) and the architecture gate; L1 is the batch rerun: session B reads the host-resume line and the keeper's absence, no `WorldReady never arrived` valve, and `live-adopted` within a stream pass; session A reads the entry rows
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket's row-4 repair as the round's first work item and orders the 60 s pump question answered; both findings are defects with one correct behaviour (no design trade-off), so no user-visible choice was introduced, and the 2026-10-21 rebuild decisions still stand
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `StartGateCoordinator` 344 (the pump, ~25 lines), `WorldStartGate` 212, `EnemyRosterBinder` 355, `EnemySyncService` 565, `RunCoordinator` 611 (unchanged: the pump was moved to the gate presentation precisely to stay at its recorded debt line); no new state field (`LateJoinerPasses` is a computed property); nothing dead kept
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; behaviour suite 6138/6138; normative gates 752/752 in the evidence run (this box's own gate excluded), then 753/753 after it
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
