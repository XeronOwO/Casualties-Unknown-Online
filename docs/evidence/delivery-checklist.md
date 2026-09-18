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
      file:line or runtime log) or is explicitly marked unverified — evidence: the bite is SpiderHandler.cs:180-210 (collider contact + minVectorDotToBite facing gate) and the lunge is CrystalEnemy.cs:133-165 (first body wins, ground stops it), with every CUO site they replaced traced (TryOrderSpiderBite, OnCrystalLungeBegin, ApplyHostSpiderBite, ApplyHostCrystalLunge)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both attack kinds converted (bite and lunge), the host's own body left on the native collision path, the proximity-effect family confirmed already local (EnemySyncService.SendEnemyEffect) and the item-hit fallback confirmed host-side and untouched
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: review/enemy-hit-determination-local.md carries the design direction, the landed mechanism list and the acceptance-matrix coverage table (every row mapped to a test, to an unchanged path, or to the user's dual-client pass)
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: L0 rule tests (EnemyAttackJudgmentTests, EnemyAttackLedgerTests), wire simulation over ItemSimWorld (EnemyAttackSyncTests: broadcast to every in-world guest + per-enemy identity), and the frame-level dual-client pass that remains the user's
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the user confirmed this cycle's plan in-session, on top of the 2026-09-18 ruling that decision 184 records and the five directions it fixed
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors, dotnet format exit 0, whole-solution evidence run green 3265 + 55 with the checklist gate filtered out, and the unfiltered building run proves 3265 + 56 (dotnet test CasualtiesUnknownOnline.slnx)
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: largest touched class 512 lines (EnemySyncService), EnemyCombatDirector 355, EnemyCombatReplay 317, the new EnemyAttackLocalProbe 139 / EnemyAttackJudgment 141 / EnemyAttackLedger 45 / EnemyBiteAnnouncementState 21; no new bool state; the dead host-side verdict machinery deleted (SelectLungeVictim, DecideSpiderBite + DecideCrystalLunge + ApplyPath.RemoteOrder, SelectLimbIndex + BodyLimbIndex, CrystalRayLength + CrystalRayTolerance, FirstGroundDistance)
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
