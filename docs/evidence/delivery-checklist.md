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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: acceptance cycle `20261005-b` touched no mechanism; every claim the record makes is read from the deployed artifact's own logs — `net-receive-blackout` parking the receiver's inbound dispatch, `Sent kernel checkpoint at revision N` (60 s phase), `kept N unacknowledged item report(s) across the world baseline restored at revision N`, `[LayerReset] dropped the previous layer's world-rooted items`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all nine matrix rows are judged on `+53d577e0` in one batch; rows 1, 3, 5, 8 and 9 were re-run in this batch's second session instead of being carried over from `20261005-a`, and row 4 moved from `unproven` to `pass`
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the record's nine-row table, one verdict and one artifact pointer per row (`docs/evidence/acceptance/guest-command-loss-reconciliation-20261005-b.md`)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: each row names its injection (host-parked inbound for rows 1-4, 8, 9; guest-parked for row 5), the checkpoint phase is read from the host's log before the drop, row 4's empty-table window is measured before the destroy, and each verdict pairs a probe JSON with the log line that carries it
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff the user passed names batch `20261005-b` (row 2's machine re-run) as the next step; no work-item choice re-asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: the run executed the frozen artifact `0.1.0+53d577e0` (`tools/verify-deploy.ps1` exit 0, 35 files, 34 matched); this documentation-only cycle changes no C#, so `dotnet format` is skipped per the rule above, and the focused normative-gate run follows this checklist
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no `src/`, `tests/` or `tools/` file changed this cycle; the moved ticket and record are flat markdown, the index row is one pointer line, and every pointer-shaped reference to the moved slug was re-pointed by hand (matrix rows and gap list, two review tickets, three done tickets)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
