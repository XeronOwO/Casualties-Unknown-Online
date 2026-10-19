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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no CUO gesture: it floors the physical building health a member's destructive-trap report already carries, whose native write is the game's own building-death branch (`reversing/Assembly-CSharp/Assembly-CSharp/BuildingEntity.cs:56`: `this.health < 0.5f`); the report's drop bound and its acceptance row were decided on 2026-10-19 and the user's handoff ordered this fix (red-first → code → gates → commit → deploy); no fallback, no new entry, no player-visible surface
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the one command every reporter builds (`RecordBuildingEntityHealthCommand`) and its production constructors (`TrapStateRegistry.ReportBatch`, `KernelDomainCommands.TryRecordBuildingEntityHealth`, and the HostOnly wire refusal) are traced in the ticket; the floor and the fact invariant live in `WorldEntityDomainModule.DecideRecordHealth`/`AssertInvariants`; the composite's atomicity is `GameStateKernel.ExecuteComposite` (reduce → assert → store swap); the adapter capture sites (`EntityEventSync.ReadDestroyedTrapHealth`, `TrapBuildingHealthScope.TryAdd`, `WorldBuildingEntitySync`'s damage reports) are Unity-typed and remain machine-read only
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every building-health writer was walked — the trap composite's destroyed-trap reading, the blast scope's entries, the explosion's direct damage report and the opened report all reach the one command; the guest/wire path is HostOnly and refused at admission; `GameStateKernel.Restore` is named in the ticket as the separate host-trusted writer outside the command floor; the non-finite family was closed in the same change (the invariant now refuses ±∞, not only NaN)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `NetMsg.EntityEvent` fix paragraph carries the four-row table (the one floor; non-finite facts; the clamp's observability; the whole family), each row naming its test, log or writer trace
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: red-first on three defects — the trap composite and the direct damage report (`.acceptance/20261019-b/red-negative-health.log`) and the +∞ invariant hole (`red-nonfinite-health.log`); green in `green-negative-health.log` and `focused-family-after-review.log` (35/35); the deployed two-client rerun of the ordinary-event drop row is batch `20261019-b`'s own evidence after the commit
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-19 handoff names this fix as the next work item and orders the exact sequence (red test → code → gates → commit → deploy), and the ticket's row already froze the shape: one floor where a physical health becomes a kernel fact, for the trap batch AND the damage report
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the edits, `WorldEntityDomainModule` 203 lines, `TrapStateRegistry` 188, `BuildingEntityHealthRegistry` 52, and the three test files 527/104/323 — all under the 600 gate; no new bool state (the only new member is the Debug line at the two report seams, and the kernel stays logger-free); the first draft's blanket "non-finite" claim was replaced by the finite invariant it always meant, so no dead claim is left standing
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0, `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings, focused family 35/35 (`.acceptance/20261019-b/focused-family-after-review.log`), and the final unfiltered `dotnet test CasualtiesUnknownOnline.slnx` is 741/741 normative gates and 6019/6019 behaviour tests green (`.acceptance/20261019-b/full.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
