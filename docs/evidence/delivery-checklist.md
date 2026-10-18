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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no CUO gesture and no player-visible surface: it bounds an existing member report and the new answer is a refusal plus the reporter's own local destroy; the items it judges are the native building-death drops the reporter already rolls (`BuildingEntity.Update`'s death branch, named in `done/trap-destruction-drop-quantity-desync.md`); the handoff ordered this ticket and decision 265 already rules the bound's shape
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the trap entity's own table read (the applier's position-keyed identity rule), the host's explosion-diff capture, the trap entity's single count (excluded from the scope's second read), the set matcher (`DropBoundPolicy`), the answer's two reason mappings and the reporter's destroy arm are cited with paths and quoted text in the ticket's `NetMsg.EntityEvent` section; the capture itself is named as Unity-typed and unverified until the acceptance batch, and the host's own-blast roll ownership is split into its own ticket and named unverified there
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the drops family was walked one by one — the block-break carrier (`BlockDropBoundJudge`, already bounded), the building-death carrier on a break (`BoundBuildingDrops`, already bounded), the destructive entity-event carrier (this change), and the creation channel `ItemSpawn` (still unbounded and named in the ticket); the audit found the host's own blast rolling a second set for the same death BEFORE the commit, and because the direct-blast suppression breaks on chain explosions and host-only deaths the ownership question was NOT half-fixed — it is filed with its evidence as `docs/backlog/todo/host-blast-rolls-a-second-drop-set.md` and named as the row's open limit
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `NetMsg.EntityEvent` section carries the mechanism × change × evidence table: six rows covering the report's set bound, the answer, the reason's two mappings, a kind outside the building-death carrier, the trap table's single count and the empty-row set
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `TrapDropBoundJudgeTests` five cases over the real runtime (host judge → wire → the guest's `ItemRejected` surfacing), with three mutation REDs recorded (the partition, the kind arm and the reason arm); the capture and the caller's use of the accepted list are Unity-typed and named for the acceptance batch (`NetMsg.EntityEvent` drop-refusal row in the ticket)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the session handoff ordered this ticket's remaining rows and decision 265 (the user's 2026-10-17 data-only validation ruling) is the standing approval for the bound's SHAPE; the change adds no user-visible surface, only a refusal answered to a reporter whose drops lie outside what this host's own application can account for
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: after `dotnet format`, `EntityEventSync` 223 lines, `TrapDropBoundJudge` 104, `TrapBuildingHealthScope` 85, `ItemApplication` 315 and `WorldBuildingEntitySync` 300 — all under the 600 gate; no new state bools (the capture rides the existing scope and the trap-entity identity is a constructor value); the two enum additions are additive (`RejectionReason.TrapDropUnaccounted`, `ItemRejectMsg.Reason.TrapDropUnaccounted`)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` exit 0 (0 warnings), `dotnet format CasualtiesUnknownOnline.slnx` exit 0, and the final unfiltered `dotnet test CasualtiesUnknownOnline.slnx` run after this tree's last edit is 741/741 normative gates and 5976/5976 behaviour tests green (`.agent-local/trap-drop-full2.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
