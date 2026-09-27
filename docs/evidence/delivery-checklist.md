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
      file:line or runtime log) or is explicitly marked unverified — evidence: selfcheck §1 (4 rows) + §2: the pin reference, the drift arithmetic, the ordering and the window quoted from the changed sources; the deployed artifact `0.1.0+c8e97c1d…` and commit `0e7693f4` read from `%TEMP%/cuo-s6-verify.txt` and `git log`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both carry views store the reference after their own ride pose wrote the root, the release drops the reference but never the window's reading, an unavailable anchor keeps the reference, and the carried local rider's own client is named as out of this reading's reach (selfcheck §2/§5)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: selfcheck §1 (the readings and what each one answers), §2 (what landed), §3 (the verification table, 13 rows), §4 (the run recipe), §5 (limits)
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: the acceptance run's eye-free evidence is the two printed readings plus the anomaly warnings at the default level; mechanical proof is 9 real-source mutations run by this cycle, all red (`%TEMP%/cuo-carry-mut/`, `%TEMP%/cuo-carry-mut2/`, sources md5-identical after restore), and the review's own 5 surviving mutations are red too; the picture stays the user's run (selfcheck §4/§5)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the handoff instruction continues this Critical ticket; decision 222 forbids building a placement change on its unverified mechanism, so the cycle ships readings; no work-choice question asked (AGENTS.md rule 9)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors (`%TEMP%/cuo-carry-build3.txt`); focus 57/57 (`cuo-carry-focus3.txt`); format exit 0 (`cuo-carry-format2.txt`); gates 287/287 with the checklist gate excluded (`cuo-carry-gates4.txt`), then the full run green
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: largest touched type 587/162/164/122/33 lines (all under the 600 aggregate ceiling; `RemotePlayerRenderer` crossed it once and was trimmed back by moving the reading's reach into the Runtime rule); one top-level type per file (new `CarryAnomalies` split out, gate green); no `_`-bool added; nothing superseded left behind
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
