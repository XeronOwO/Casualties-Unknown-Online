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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `container-move-kernel-fact-selfcheck.md` §1 names the owner's report (`ContainerItemSync.OnLoadedIntoContainer`), the batch rebuild (`KernelBatchItemProjection.BuildContents`), the receiver's own prune (`CloneFactTable.ApplyCarriedSync`) and the wire path that already worked (`KernelWireMapper` → `ItemDomainModule.DecideSyncContainer`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the container job had TWO implementations and the wrong one is deleted rather than fixed twice — `ItemContainerSyncWriter`/`SyncContainerContents` was dead in production and relocation-blind; the other three `SendItemCarriedSync` carriers were read and pass full recursive captures
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/container-move-kernel-fact-selfcheck.md` §3 — four rows, each naming the mechanism, the change and the test that pins it
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the host-report row `ContainerSyncProtocolTests.HostCarriedContainerReport_ProjectsTheMovedChildInsideTheContainer` recorded RED first (`Expected: Contained, Actual: Carried`), the kernel/command rows in `ItemContainerSyncTests`, and row A1 named in the ticket for the next three-client batch
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff the user passed names the container-move accounting fix as the next work item, and the work-item choice is the agent's per `AGENTS.md` rule 9; no user question was asked this cycle
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` succeeded; `dotnet format` exit 0; full suite 4621/4621 and the gate project 346/346 — the single mid-cycle red was this checklist gate itself, closed by this box, with the focused gate re-run green afterwards
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `ItemService` 599 → 536 with `ItemKernelProjectionWiring` 157, `ItemKernelAuthority` 577 → 449 with `KernelDomainCommands` 195 (the two seams the architecture watchlist named; the fix alone had carried the authority to 616 and the gate refused it); no new state bool; `ItemContainerSyncWriter` deleted
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
