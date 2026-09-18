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
      file:line or runtime log) or is explicitly marked unverified — evidence: three mechanisms, each measured on this tree: the index (136 rows before and after; blob sizes 53 340 → 20 011 B, longest row 1 560 → 150, 106 → 0 rows over budget), the two new gate rules plus their negative self-test (`BacklogIntegrityGateTests` 12/12), and the two `:\s` regex patterns that the tracked-file absolute-path scan reads as drive-letter paths (rewritten, `NoAbsolutePaths` 1/1 green)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a C# regex escape that reads as a drive-letter path"; the scan covers every tracked file and is green on this tree, and the two sibling candidates the independent review named (`SourceShapeGateTests`'s `@":\s*GameCommand…"` and the new note in `normative-gates.md`) were re-measured with a `[regex]::IsMatch` probe → False, so the family is exactly the two shipped patterns
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: index → pointer rows (136/136 rows, longest 150 of the 160 budget) · gate → budget + priority agreement + negative self-test (class 12/12) · path scan → `[ \t]*` head plus trailing `\s*`, CRLF-safe, drive-letter scan green · ticket → landing record moved to `review/` with folder, `- Status:` and index section agreeing
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: no runtime path is touched, so the proof is the gate suite against the real tree: the new rule measured RED on the pre-change index (1 failed / 11 passed, 106 over-budget rows) and green after the rewrite, then the frozen tree ran 43/43 normative gates plus 3 202/3 202 tests; the independent review reproduced the structural claims and each of its three findings was re-measured before this line
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the handoff instruction named this work item as the next cycle's first task (the backlog index becomes pointer rows with a length gate); no open design question (documentation structure, not gameplay or UI)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 with no file rewritten; `dotnet test CasualtiesUnknownOnline.slnx` → 3 202/3 202 main plus 43/43 normative gates on the filtered evidence run, and this cycle's last step is the unfiltered gate run with every box here checked
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: the touched gate class is 12 cases (limit 40) and 511 lines (limit 600); no production class is touched; no state bool; the one temporary artifact (`docs/backlog/README.md.new`) was consumed by the swap and leaves no orphan
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
