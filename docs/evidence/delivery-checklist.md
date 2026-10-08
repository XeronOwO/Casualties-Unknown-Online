# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and multiplayer acceptance are the agent's actions,
run after the commit per `docs/acceptance/` and outside this gate; feature
development verification uses simulation/static evidence. When a release cycle
lands, reset the checklist by manually unchecking every box so the next cycle
starts clean.

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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: no `src/` file changed this cycle; the two code files are `tools/acceptance/recipes/body-place.cs` (the fractional-argument cast the run's own finding required) and one doc-comment path in `StandingItemGateTests.cs` (the moved ticket's own reference); every reading cites the product path it was read from (the materializer's own `[StandingItem] … carried row(s) … standing object(s)` line, `ItemUseSync`'s standing branch, `CrossPlayerDragUse`'s pointer read, `Body.Eat` / `ItemInfo.useAction`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the standing-object family re-read through its own census across two worlds (8/5 and 2/1 objects): the condition path in both directions, the four retire edges ("the data no longer carries the row", "the owner left the world", "the row left the inventory into the world", session end) and every presentation switch, one by one
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the batch record carries one verdict and one evidence pointer for each of the fifteen rows the scope page declared before the session; an independent reviewer re-read every cited artifact and its findings are folded into that record (row 3's citation, row 5's audio flag, the release count, rows 7/8/13's measurements and counts)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `docs/evidence/acceptance/20261008-a-scope.md` declares the probes, the two-client setup, the declared substitutions (body placement, forced writes, the production request entry) and every row's class before the session
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff fixed the acceptance batch as this cycle's work and the ticket's design was frozen by the user 2026-10-08; no work-item choice was re-asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings/0 errors; behaviour 4805/4805; gate project 548/548 including the checklist gate after the boxes were filled; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (run after the review's two code findings, the recipe cast and the moved ticket's doc-comment path)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `git status --short -- src` is empty and no class behaviour changed anywhere; the cycle's files are the acceptance record, the scope page, the index/ticket moves, the lessons entries, this checklist, one cast in `tools/acceptance/recipes/body-place.cs` and one doc-comment path in `StandingItemGateTests.cs`
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
