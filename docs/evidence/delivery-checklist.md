# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

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
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture: the change reconciles enemy COPIES, and the native chain it mirrors is the game's own generation/creation/death path (`WorldGeneration.DistributeEntities`/`GenerateEntityAtPos`, `BuildingEntity.Start` for the runtime classification, `RuntimeEntityFactory` → `Utils.Create` for materialization, `BuildingEntity.Update` for death/drop ownership); no identity fallback is added — the position pairing and its whole-set refusal are deleted — and the four user-visible choices are the user's 2026-10-21 decision (host roster, per-copy converge, one identity, per-row report)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the selfcheck's nine-row inventory (`docs/evidence/selfchecks/enemies/enemy-roster-adoption-selfcheck.md` §1) names the kernel enemy facts, the generation-stamped snapshot, the host capture, the pure adoption, the binder, the binding table, the empty/stale gates, the layer reset and the deleted family; the runtime half is the batch, not claimed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both channels (generated + runtime-spawned), every entry path (entry, late join, 60 s repair, layer descent), the empty table and the stale generation were aligned in the same change; the trap-layout/runtime-entity absolute-table pattern was the precedent, the old pairings and the empty-snapshot no-op are deleted, and the source-shape gate pins the absence of the old matching names
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/enemies/enemy-roster-adoption-selfcheck.md` — §3's eight mechanism × change × evidence rows, with the batch rows marked NOT RUN and the review dispositions appended
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the pure `EnemyRosterAdoptionTests` matrix (reuse/replace/key/tie/empty/unresolved) plus the snapshot/service cases (empty, stale, reset, hint survival) plus the source-shape gate; L1 is the ticket's three-client rows (entry, mid-run join, drift, runtime spawn, layer descent) on the deployed artifact
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user chose all four rebuild options on 2026-10-21 (host roster / per-copy converge / one identity / per-row report), after the handoff named this ticket the round's first work item; no user-visible choice was re-asked
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `EnemySyncCoordinator` 413 + `EnemyBindingTable` 96 + `EnemyRosterBinder` 350 (the rebuild reached 730 aggregate before the split; HEAD was 587), `EnemyRosterAdoption` 268, `EnemyRosterPublisher` 128, `EnemySyncService` 558, `GameAdapterBridge` 600; two boolean fields added, each with its reader (`EnemySyncCoordinator._adoptionPending` for the deferred snapshot pump, `EnemyRosterPublisher._rosterReady` for the host's snapshot gate) and both within the per-class budget; no new debt entry; the two old arbitration files, the empty-snapshot no-op and the scan-only state list are deleted
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; behaviour suite 6133/6133; normative gates 751/751 in the evidence run (this box's own gate excluded), then 752/752 after it
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
