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

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [x] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified — evidence: this cycle touches no runtime mechanism; the moved ticket's claims were re-verified instead — 82/82 acceptance test anchors re-resolved in the test tree (54 `Type.Method` + 28 shorthand `.Method`, 0 missing), 376/376 focused save/restore + world-entity cases, 32/32 normative gates
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family was every reference to the two moved tickets — 33 references across 18 files (one by hand, 32 by one literal sweep rule), with a repo-wide search confirming 0 residual hits for the two moved ticket paths
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the *S3.5 closure* section of `review/save-mid-run-consistent-cut.md` carries the scope table (9 scopes x state x where each landed), the exactly-once claim split into machine-proven vs the user's in-game half, and the re-anchoring numbers
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: no runtime behaviour changes, so the proof is a one-off anchor re-resolution pass (82/82, script not landed), the focused suites, and the normative gates; the moved ticket's in-game rows stay named as the user's pass and are NOT claimed as observed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the S3 design was frozen with the user on 2026-09-10 (decisions 162-166) and the handoff instruction named this ticket as the next work item; the S3.5 closure is verification/documentation only, so it introduces no design that needs approval
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; full suite 3 202/3 202 + 31/32 normative gates, the only red being THIS checklist before its own boxes were checked; re-run after checking them is 32/32
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: no class is touched — the two source edits are comment-only lines repointing a moved ticket path (`WorldFactRestore.cs`, `WorldTransientPolicyTests.cs`), with no state bool and no dead mechanism involved; the rest of the change is documentation
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
