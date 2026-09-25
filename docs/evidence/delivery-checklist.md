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
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified — evidence: remote-inventory-native-intent-stage4-audit §2, the matrix's 15 rows, each carrying its implementation anchor
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all 15 matrix rows audited in one pass across both directions, worn items, containers, the craft screen and the container window (audit §2-§3)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: remote-inventory-native-intent-stage4-audit (matrix 15 rows, findings §3, limits §4, projection verdicts §5)
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: a code-fact anchor per row plus the acceptance checklist's session rows, 3 judgement items and failure-capture steps; no test here renders a frame
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the handoff names stage 4 of the rework ticket as the next work item; design §4's stage table froze it (decision 217)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: 3906/3906 with build; gates 149/149 and 148/148 filtered, post-fix (%TEMP%\cuo-stage4-full-final.txt); doc-only cycle, format skipped
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: no src/ or tests/ file changed; stage 3's measured aggregates stand (window 511 lines, RemoteContainerMoveCapture 176)
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
