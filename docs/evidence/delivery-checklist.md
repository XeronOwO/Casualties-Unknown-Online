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

**Documentation-only cycles (added 2026-09-17)**: a cycle that changes no runtime or test behaviour
(backlog moves, evidence/citation updates, workflow documentation) still fills EVERY box and still
leaves item 8 and FORBIDDEN unchecked, but may write its boxes in one pass. That is the deliberate
multi-line exception the paragraph above already allows for a cycle reset, applied to a cycle with no
implementation sequence to record; the BULK-checking prohibition keeps governing every cycle that
touches `src/`, `tests/` or `tools/`. `dotnet format` may be skipped (it only rewrites C#, and such a
cycle has none). The evidence run may use
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — but the focused
normative-gate run afterwards is NOT optional: it is the only thing that proves this checklist complete
before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [x] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified — evidence: seams read: WorldEntryFanout's two sends, TrapLayoutScanner's edge scan, TrapLayoutRegistry Replace/Reset, both entry handlers
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both group sends + all four production send points moved together; W6 row re-pointed, residual closed, anchors 21 to 27
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's acceptance matrix carries a result per row (5 new cases + 1 reconnect case), each with its named test
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: red recorded on the pre-fix tree (5F/4P, refreshes=0); green 38/38 focused, 3454 full suite; adapter leg named as dual-client
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: work-item choice is the agent's per AGENTS.md rule 9; the handoff's suggested order and the ticket's own notes were followed
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: dotnet build 0 warnings/0 errors; dotnet format exit 0; full suite with build 3454 + gates 69/69
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: largest touched class WorldEventSync 524 (shrank), WorldEntryFanout 151, LiveTrapLayoutSource 74; state = one int frame guard; RefreshLayout + its wiring deleted
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
