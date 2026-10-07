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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, nine rows — the batch's red reading, the call site's two different fields, `EnemyEntity.SpawnPosition`'s own key contract, the anchor's provenance at the first bind, the member's freeze and candidate filter, the 60 s repair cadence, the shared `Random.state` baseline the key rests on, why no pure test could see it, and the runtime-spawn family's deliberate key
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 row 9 with §2 — the runtime-spawn channel's LIVE-pose key is deliberately different (its copy is born where the creation report placed the animal, so the host's anchor sits the report's latency away) and that reason is now written into `EnemyRuntimeSpawnArbitration`'s own doc so the two channels are not aligned by analogy; the snapshot's positional pass, its creation-key pass and the candidate filter were re-read and left as pinned, with the reason recorded
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/enemies/enemy-generation-pairing-late-join-selfcheck.md` §3, seven rows — the host-side key (the defect), the ordering of both sides, the refusal's reading, the success's reading, the untouched candidate filter and baseline latch, the Unity wiring the batch judges, and the N1 evidence anchor the gate caught
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §3 — the RED is batch `20261006-g`'s 5/5 failing cycles on the deployed artifact plus mutation A (the pre-fix key inside the new entry point, 4 of 25 cases red, file restored byte-identically); the runtime half is ticket rows 1-5 on three clients with a mid-run join, reading `mapping=True` with the same `N generated bound` and the worst key distance of the paired set, and zero `generation spawn pairing failed` lines
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this cycle's handoff names `enemy-generation-pairing-late-join` as the development-side first work item (the High ticket that needs no machine), the order is the agent's per `AGENTS.md` rule 9, the ticket's own items 1-4 left the fix's shape to this cycle, and no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx --no-incremental` clean (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` = gate project 449/449 and behaviour 4663/4663, 0 failures (this checklist's own gate is the one filtered out while these two boxes were open and is proven by the focused gate run that follows this edit)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: physical lines — `EnemySpawnArbitration` 186, `EnemyRuntimeSpawnArbitration` 239 (doc only), `EnemySyncCoordinator` 383, its `RuntimeSpawns` partial 204, test files 198 and 302 — every file under the 600-line gate; no new state and no new bool (the added members are the pure entry point, the nested outcome/divergence value types and the tests' own helper); the dead mechanisms were DELETED in the same round — `TryPair` (its only production caller was the line this cycle replaced) and `Order` (no production caller) — not kept beside the new entry point; the independent adversarial review's 11 findings (0 blocker, 0 major, 4 minor, 7 nit) were all fixed in this round, including the two mechanism-level ones (the family doc's unverifiable reason, the anchor-field wording) and the sentinel that made `IsCountMismatch` true on a successful pair
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
