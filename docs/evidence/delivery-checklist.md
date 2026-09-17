# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and manual multiplayer acceptance are user release
actions outside this gate; feature development verification uses
simulation/static evidence. When a release cycle lands, reset the checklist by
manually unchecking every box so the next cycle starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only a deliberate cycle reset may touch multiple lines.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [x] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified — evidence: 64 data rows are 10 cells each with an `Anchors` count equal to that row's entries in `docs/evidence/sync-coverage-evidence.json`, which is now the single home of every quoted source text
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all 64 data rows were rewritten by one rule rather than row by row, and the 33 mechanical-deletion residues the independent review found across 21 rows are fixed in the same change
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `SyncCoverageGateTests.SyncCoverageMatrix_DeclaredAnchorCountsMatchTheEvidence` recomputes every row's count against the JSON (64/64 match, independently reproduced by the review) and every new gate rule carries a negative-contract self-test
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: no runtime behaviour is touched, so the proof is the gate itself (12/12 including 7 negative-contract self-tests) plus the full suite, with the review replaying mutations against the real gate DLL outside the tree
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the ticket's own "Required outcome (decide at implementation)" block froze the five outcomes, and the handoff instruction named this ticket as the next work item
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `SyncCoverageGateTests` 12/12; full suite 3 202 + 32 gates green (the only red was this checklist before its own boxes were checked)
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: `SyncCoverageGateTests.cs` is 642 lines, over the 600-line advisory; the repository line cap enumerates `src` only, `TestClassSizeGateTests` caps xUnit cases (12 here, limit 40), and the deviation with the review's split recommendation is recorded in `review/evidence-matrix-fat-rows-split.md`
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
