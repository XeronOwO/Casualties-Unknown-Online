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
      file:line or runtime log) or is explicitly marked unverified — evidence: selfcheck stage 3 tables, 8 mechanism rows plus the port/adapter rows, each cell naming a file or a test
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all nine named types accounted for (eight moved, three stay-behinds named with blockers); every consumer of each port is wired in KernelReplicationComposition; the sync-coverage quotes re-pointed (gate-enforced)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: selfcheck page's mechanism x change table; each of the 8 stage-3 rows names a production type and the test that pins it (round-2 review M2 also removed two dead port members)
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: port delegation read against the pre-move call sites + KernelReplicationLayerBoundaryTests + the production-composition kernel families (82 focused cases); real-client behaviour explicitly not claimed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: owner instruction "finish every remaining todo, then come back" (ticket Gate line, decision 207) and the handoff's stage-3 direction ("ports first, then the move")
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings/0 errors; format exit 0 (read-only --verify-no-changes --include over the changed files also exit 0); tests 3773 passed (build included), gates 93/93 with this checklist filled
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: CuoBootstrap 586 lines (was 600, cap 600); moved/added types 9-555 lines; no new state bool; the two uncalled port members deleted after the round-2 review; no dangling old-path citations
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
