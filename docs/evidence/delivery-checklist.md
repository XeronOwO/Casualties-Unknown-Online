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
      file:line or runtime log) or is explicitly marked unverified — evidence: three mechanisms read on this tree — the new wire message + handler (NetMsg.BlockDamageReport = 136, NetMessageDirection.GuestToHost, payload BlockDamageSnapshotMsg, ProtocolVersion.Current 22), the guest pending table + 60 s re-send (PendingBlockDamageTable → GuestBlockDamageReportBookkeeping → GuestReportRecovery.ResendDamages, driven by WorldReportFallbackPump) and the host merge + answer (INativeWorldFacts.MergeBlockDamages → GameBlockDamageTable.DecideMerge/Merge → the BlockDamageSnapshot broadcast); the adapter's record point (BlockBreakSync.OnBlockDamaged reading the cell's accumulated BlockDamage.damage) is code-reviewed only, and the ticket names it as such
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a guest's unacknowledged report": W1 (block state) and W2 (partial damage) now share one collaborator (GuestReportRecovery) and one pump while keeping separate tables, both reset at the same world/layer boundary (WorldParamsService.Apply calls both resets) and both cleared by the air-write path (BlockBreakSync.OnBlockAirWrite); GameBlockDamageTable.Apply keeps its semantics and the new Merge/DecideMerge reuse the same air/range/128-cap rules; every ProtocolVersion reference was re-pointed (ProtocolVersion.cs, decision 137, docs/api/mod-api.md) and the four docs left dangling by the ticket move were fixed (two of them, including the W6 ticket, were pre-existing rot); the W1 neighbour suite still passes 6/6
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: wire → new id + one-way direction → GuestToHostDirectionTests.GuestToHost_AllowedOnHost_RejectedOnGuest + DirectionClassificationTests · guest record → absolute upsert, cap and remove → PendingBlockDamageTableTests (6) · re-send → the 60 s window re-reports the outstanding set → SwallowedGuestPartialDamageReport_ConvergesOnTheFallbackCycle + HostsAnswer_ClearsThePendingReport · host merge → never-lower plus air/range/128-cap → GameBlockDamageTableTests (6), incl. DecideMerge_NeverLowersTheHostsOwnDamageAndKeepsTheSameBoundary · answer → clears the pending entry and the local crack → RefusedReport_IsAnsweredWithZeroAndClearsThePendingReport · third party → the broadcast reaches G2 → ThirdParty_ReceivesTheHostsAuthoritativeValue · boundaries → air write and world reset → AirWrite_ForgetsThePendingCell, WorldReset_DropsThePendingReports · no port → no invented answer → NoNativeReader_LeavesTheReportOutstandingInsteadOfInventingAnAnswer · roles → HostRole_NeverRecordsAPendingReport, GuestRole_NeverMergesOrAnswersAReport
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: the Runtime half is proved by the real composition root in the test host — ItemSimWorld + the faked INativeWorldFacts port → GuestBlockDamageReportRecoveryTests (9) with the port's own call log as arrival evidence; the Unity half (the record-before-send order, the merge's world reads and crack-sprite refresh, the zero-row BlockDamageCleaner.ClearForAirWrite path, the live DamageBlock hook) cannot execute in the test host and is declared code-review + unified dual-client acceptance in both the ticket and matrix row W2; the on-machine trace is `[BlockSync] merged {Peer}'s partial-damage report …`, `[BlockDamageReport] merged {Count} reported cell(s) …`, the refusal warnings and `Block-damage snapshot applied (… {Cleared} cleared)`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the handoff instruction named the Medium family as this cycle's work and put this ticket first among them, and the ticket's own Design direction #1 (absolute re-report + per-cell merge) is the frozen direction; no gameplay/UI/architecture question was open here — the review's max-vs-sum finding is an implementation decision taken per the repo rules (record it honestly in the ticket and matrix, and open todo/partial-damage-delta-report-overlap.md for the fix), not a user-owned choice
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors on the final tree; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; focused four classes 27/27; full suite 3 225 passed (43/43 gates on the filtered evidence run, `--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"`), and this cycle's last step is the unfiltered normative-gate run with every box here checked
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: touched classes measured on this tree — WorldStateMessageService 579, WorldService 582, GuestReportRecovery 209, GameBlockDamageTable 299, BlockBreakSync 417, IWorldControl 505, PendingBlockDamageTable 78, GuestBlockDamageReportBookkeeping 118 (limit 600); test classes 9 / 6 / 6 cases (limit 40); the pre-split 713 was re-stated as an in-session intermediate reading with the two checkable states (575 at HEAD, 579 final) named; no new state bool (the pending tables keep their state in the bookkeeping collaborators; PendingReportFallback's armed flag is pre-existing); dead surface deleted in the same round — the test-only BlockDamageReportReceived event and its three forwards, the now-unused `using System`, and the WorldStateMessageService._nativeWorldFacts field the extraction absorbed
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
