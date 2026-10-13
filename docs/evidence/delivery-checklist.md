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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable, and the reason is checkable — nothing here crosses players or mirrors a native call site: `git diff --stat -- src/` answers nothing, and the change set is a gate test plus its records
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: two mechanisms, each with its consumer named — (1) the checklist's declared boxes, read by `DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` and by the incomplete-box check beside it; (2) the event-replay matrix's kind set, read by `RepositoryGateTests.EventReplayMatrix_Completeness` against `EntityEventKind.cs`, the source of truth both it and the dispatch census parse
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a gate whose subject is a record the repository rewrites, read with no census of it", and it was swept rather than fixed where the ticket pointed: the sweep walked all 18 gate files that name a `docs/` path, separated the ones that read a record as their subject from the ones that merely cite one, and found exactly two carrying no census — the delivery checklist and the event-replay matrix, both in `RepositoryGateTests` — and both are closed in this change, with each sibling's census form checked one by one (`AcceptanceLessonsGateTests`' `EntryFloor`, `SelfcheckManifestGateTests`' `IndexFloor`, `DocumentCensusGateTests`' `CensusFloor`, `SyncCoverageGateTests`' `MinimumMatrixRows` + `MinimumEvidenceEntries`, and the source-derivation `EntityEventDispatch` already used); the sweep and its result are in the ticket
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: three observations, each with the run that produced it: (1) every box line deleted from the checklist → the census fact names all 10 lost steps (the pre-fix gate passed the same file 1/1); (2) the matrix reduced to its header → `EventReplayMatrix_Completeness` names the 33 lost kinds (the pre-fix gate passed it 1/1); (3) both records emptied, both families in one filtered pass → 6 cases, 2 failed / 4 passed, the reds being exactly the two census facts while the incomplete-box check stayed green on the emptied checklist. Each observation is pinned synthetically by its own sample, and the real file's mid-cycle run is red on the incomplete-box check alone
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: no runtime is involved and that is stated rather than assumed — the subject is a process record, so its proof is a gate plus a re-runnable probe: three samples feed synthetic text to the same pure functions the facts use, and the two mutation probes run against the real files (every box line deleted; the matrix reduced to its header), each with its own focused run and case count
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the previous cycle's handoff, which named this ticket as the next item; its three open questions (what the floor counts, a count versus the required items, where the number lives) are the gate's own scan-surface choice, settled by the rule rather than asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet test CasualtiesUnknownOnline.slnx` green at **5072/5072** behaviour cases (net48) and **657/658** gates, the single red being this checklist's own two open boxes — named line by line, which is the gate working rather than a defect; the focused re-run with every box checked is green, 5/5
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `RepositoryGateTests.cs` 418 lines (415 at HEAD plus the event-replay census) and the new `DeliveryChecklistGateTests.cs` 223 — both inside the 600-line discipline, which is why the family moved: this cycle's first pass had pushed the shared file to 609. No new state bool and no new state field anywhere (the only type added is the nested `ChecklistBox` record, whose `Checked` is a value, not a flag to maintain), and the change DELETED two mechanisms rather than adding: the whole-line `Contains("FORBIDDEN")` exemption, which let an evidence suffix exempt a box, and the dead `var root = RepositoryPaths.Root;` local in the dispatch fact. The file census is 16 changed paths, all inside the ticket's scope
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
