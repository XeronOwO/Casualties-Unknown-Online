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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, eight rows — the batch's reading, the per-frame retry caller, both failure branches, the UNATTRIBUTED cause, the family's window mechanism, the census gap, why the drain needs a wind-down, and the six-file pump sweep
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 row 8 and §2 — BOTH failure branches of the clone factory bounded (not just the measured one), the sweep reproducible from the tree (files declaring a per-frame pump that also call `LogWarning`/`LogError`: six files, seventeen sites, each attributed to its enclosing method: two click-driven view opens, one policy-gated probe, one Unity-log sink, the rest one-shot), the family's other four producers re-read and left as pinned
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3, six rows — the guard's 19 cases (3 new), the gate's 25 cases (2 new facts, the ask and drain containment matchers, five census floors, five matcher theories), the pre-fix RED, the four mutations with hashes, the build/format/gate run, and the runtime row the next batch owns
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the runtime half is the next batch's re-drive of `layer-change-member-dropout`'s fixture (both members' inbound parked inside ONE command for ~9 s) with each client's growth read as a SIZE by `.acceptance/tools/log-census.ps1` since a mark: at most three lines per (member, failure) subject plus at most one summary per ended run, against 58,148 lines (ticket, *Required work* 4)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff instruction named this ticket as the next development work item ("do the development-side remote-clone-warning-storm ticket first"), the ticket itself carries the required work and the accept-by standard, and no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; evidence run with the delivery gate excluded while this box was open = gates 449/449, behaviour 4665/4665; the closing full run with the checklist complete = **gates 450/450, behaviour 4665/4665, 0 failures** (the delivery gate is the 450th case)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: touched files 11-407 lines (`RemotePlayerRenderer` 407, from 338; `RemoteBodyFactory` 179; gate 349; guard tests 288; guard 133; key type 11); no new bool or state flag (the renderer gains one `LogRepetitionGuard` field); the two direct `log.LogWarning` calls the window replaces are DELETED, not kept beside it, and an unused `FailureReasons` array written on the way was deleted in the same round (the independent review's F2)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
