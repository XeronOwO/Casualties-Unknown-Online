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
      file:line or runtime log) or is explicitly marked unverified — evidence: guest break = BlockPlaced + a drops-carrying BlockDamaged one frame later (BlockBreakSync.FlushPendingBlockBreak); the host registers a guest's drops ONLY from that message (BlockDropSync.FireBlockDropsReceived); the duplicate guard is the drop item id (SpawnWorldItem, RegisterWorldItemIfAbsent)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all three swallowed-report shapes closed (both messages lost / air write only / drops report only) plus the refusal path; the 60 s fallback family gained its third channel (GuestReportFallbacks); W1 matrix row extended with the drop half
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the 7-row acceptance matrix maps to named tests (GuestBreakDropRecoveryTests 12); the verdict machine (Fresh / Repeat / Refused) and both purge windows unit-tested (BlockBreakArbitrationTests 16); item-id idempotency verified at both guards
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: the Runtime half is proven over the real wire (ItemSimWorld + injected link faults + the 60 s fallback pump); the adapter half is enumerated as Unity-bound in the ticket's limitation paragraph and rests on the dual-client pass
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the handoff named this ticket as the next work item and the ticket's own `## Design direction` had frozen the approach (record the drops, re-report them, make the host verdict idempotent)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors; dotnet format clean (scoped --include); gates 56/56; full suite 3253 + 56; the unfiltered gate run follows this checklist
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: one top-level type per file (Verdict and PendingBreakDrops split out); the WorldService 600-line aggregate gate forced the WorldRunProjection extraction; no dead code left
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
