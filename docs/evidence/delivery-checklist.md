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
      file:line or runtime log) or is explicitly marked unverified — evidence: selfcheck §1 (12 cited rows): the surface and its canvas rect, the template reader, the shared row machinery, both panel models, the one action table, the single member-card builder, the four pointer polls, the parameterless census, the retirement's consumers, the untouched gesture paths, what only a game run shows
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: selfcheck §3 (9 rows): the theme's dead styles, the fade census, the window/surface pin anchors, the blocking pins, the port and guard contract censuses, the untouched colour/console pins, the untouched wire/session/saves, and the deleted rectangle test
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/ui/online-ui-panels-selfcheck.md` §1 (12 mechanism rows with quoted evidence), §2 (what landed), §3 (9 audit rows), §4 (the verification ladder with its artifacts), §5 (limits)
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: the red recorded before the change (39 failed / 44 passed / 83 total, `%TEMP%/cuo-s5-red.txt`), the placement rule by 7 pure facts, the panel surface pins by 10 pins + 20 real-source mutation rows and the blocking pins by 9 pins + 16 rows (36 in total), re-run on the frozen tree; the panels' look, the click landing, the canvas rect and the canvas scale need one game run (selfcheck §1 row 12, §5)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the ticket's Decision (2026-09-26, user ruling) froze the uGUI destination and the staged plan, and the handoff named S5 with its retirement scope; no work-choice question asked (AGENTS.md rule 9)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors (cuo-s5-build5.txt); focus 341/341 (cuo-s5-focus5.txt); format exit 0 (cuo-s5-format2.txt); gates 288/288 (cuo-s5-gates4.txt); full WITH build see cuo-s5-full2.txt
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: largest touched 585/498/479/476 lines (< 600); one type per file after the gate caught `OnlineUiPanelPlacement`/`OnlineUiPanelCorner` sharing a file; the scoped-block mechanism, the IMGUI member card and the theme's panel frame deleted in the same round
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
