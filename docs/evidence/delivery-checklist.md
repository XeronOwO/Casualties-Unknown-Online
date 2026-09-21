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
      file:line or runtime log) or is explicitly marked unverified — evidence: application-layer-first-slice-selfcheck.md mechanism table, 9 rows, every cell names a file or a test
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every wire path from a frame to the kernel inventoried (the heals outside the seam named); no stale `CanDestroy` citation left
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: selfcheck page's mechanism x change table; each of the 9 rows names a production type and the test that pins it
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: gateway unit tests + production-composition integration tests + the gate's control run; real-client behaviour explicitly not claimed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: owner instruction "finish every remaining todo, then come back" (ticket Gate line, decision 207) froze the order; the stages froze the design
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings/0 errors; format exit 0; tests 3762 passed (build included), gates 93/93
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: new types 8-152 lines; Application has no state bools; CuoBootstrap exactly 600 (cap 600, split ticket queued); no dangling `CanDestroy`
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
