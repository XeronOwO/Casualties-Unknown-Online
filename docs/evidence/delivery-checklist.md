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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: acceptance record 20261005-a row 2 (`ResetPending` log line at revision 950); `GuestCommandReconciliation.OnCheckpointRestored`; `KernelProtocolService.HandleCommand` epoch gate; `GuestCheckpointReceiver` identity rules; independent review
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every `CheckpointRestored` subscriber read (projections only; no sibling queue is dropped there); the four sibling guest-report families reset at `WorldParamsService.Apply`/`WorldService.ResetWorldLayerTables`; the item family's difference declared in matrix row I5
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: ticket row-2 section — the four restore outcomes (same run kept, other run dropped, session ended, budget spent) each with its test or its declared limit
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: simulated red before the fix (pending 1 → 0), mutation control on the other-run drop, the window's keep/drop warning lines, then the machine batch that injects the real swallow
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff the user passed names this ticket as the next step and the ticket's own "Fix row 2 first" line froze the direction; no work-item choice re-asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: format exit 0; evidence full run (delivery gate excluded) 4617/4617 net48 + focused class 16/16; gate project 329/329 after this checklist was completed
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `GuestCommandReconciliation.cs` 548 lines (<= 600); no new state bool; the wholesale queue clear is replaced rather than shadowed; the world/layer replay gap is declared in the class doc, matrix row I5 and the ticket, not silent
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
