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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `container-move-event-carrier-selfcheck.md` §1 cites the owner's container event (`ContainerItemSync.OnLoadedIntoContainer`), the replayed native pair (`RemoteIntentApplier.ApplyMoveIntoContainer`), the silencing guards in all three carriers, and the restore gate that explains them (`SourceShapeGateTests.CharacterRestore_MaterializesItemsInsideARemoteApplyScope`); `reversing/Assembly-CSharp/Container.cs:154` read for `UnloadItem`'s parent-null write
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all three item-fact carriers converted (`ContainerItemSync` three hooks, `PickupSync.OnPickedUp`, `ItemWorldSync` four hooks), not only the container wordings the batch saw; the guards that legitimately mean a replay (`HeaterCookSync`, the world replays, `ItemApplication`, `CharacterRestoreApplier`) are named as deliberately left on the bare query in `container-move-event-carrier-selfcheck.md` §1
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/container-move-event-carrier-selfcheck.md` §3 — four rows, each naming the mechanism, the change and the test that pins it (composition, replay-still-silent, the carrier gate's red-then-green, the divergence pair)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: source-shape gate `ItemFactReportScopeGateTests` (red recorded: 3 of 17 failed before the change), behavioural composition `CallContextCompositionTests.TheReplayQuery_ExcludesAPeerIntentExecution`, divergence pair `CloneFactTableDivergenceMonitorTests` (the batch's two exact wordings pinned as still-warning), and the runtime rows named for the acceptance run in the ticket's `What remains` (zero-warning monitor reading on the operator and the third peer, dropped item visible on the peers)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff the user passed names `container-move-snapshot-only-sync` as one of the two tickets the 2026-10-06 ruling raised, and the work-item choice is the agent's per `AGENTS.md` rule 9; no user question was asked this cycle
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` succeeded; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after every edit; `dotnet test CasualtiesUnknownOnline.slnx` ran 4620/4620 in `CasualtiesUnknownOnline.Tests` and 345/346 in `CasualtiesUnknownOnline.NormativeGates.Tests` — the single red is this gate itself (`DeliveryChecklist_NoIncompleteRequiredBoxes`, the mid-cycle state this box closes), and the focused gate re-run after checking it is green
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: touched files measured after the change — `RemoteIntentApplier` 512, `ItemWorldSync` 471, `ContainerItemSync` 309, `CallContext` 244, `PickupSync` 208, all under the 600-line gate; the rule added is a computed property, no new state bool; the three per-file `IsRemoteApply` helper predicates were deleted with their replacement, no dead mechanism left
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
