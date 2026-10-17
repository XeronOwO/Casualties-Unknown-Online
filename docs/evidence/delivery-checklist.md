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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no entry and changes no player-visible behaviour (it restores the registered seam of the verb whose entry mapping and refusals are the ticket's own: `review/give-item-to-a-player-command.md`, *The startup cycle the batch found*)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: three mechanisms, each cited in the ticket's *The startup cycle* and in the sources — the registration (`GameAdapterComposition`'s `AddSingleton<ItemIdAllocator>`, standalone `AddSingleton<ItemGrantSource>` + `Replace`), the one id counter (`ItemIdAllocator`'s class doc), and the aggregate's sixteen-port composition (`IGameAdapter`'s list); the runtime evidence is the client's own log line quoted there
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every seam consumed inside the adapter's own constructor graph was checked against the aggregate registrations — the give source, the visibility oracle and the local-body capture are standalone for the cycle, the local item access for needing nothing the graph owns; no remaining port on the singleton is consumed under the adapter's constructor, and the adapter's own dead visibility forwarder was deleted with the give one
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the regression pin `AdapterCapabilityPortShapeTests.GiveCreationSeam_IsStandaloneOverTheOneAllocator` (observed red before the fix: expected 0 aggregate registrations, actual 1 — `.acceptance/20261017-q/red-portpin.log`; green after — its assertions now also require the `Replace` that wires the port and ignore commented-out lines), plus the sixteen-port/26-member pins it sits beside (23/23 in that class)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the runtime is the judge of a startup cycle, so the proof is the deployed client's own log (`CUO composition root validation failed` absent, the adapter's capability report present) plus the driver's `ping` answering `plugin: true` on the fresh artifact; the batch's prelude gates on exactly that before its first row, and the re-run against the fix's commit carries it
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the design was frozen by the previous cycle (the ticket's *The design, as landed* and the user's combined 2026-10-18 ruling); this cycle is the defect path the same workflow prescribes (a failed acceptance row is fixed through the normal cycle), with no user-visible trade-off to decide
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `GameAdapter` 615 (debt entry lowered 626 -> 615 in `docs/architecture-debt.json`), `GameAdapterDomains` 405, `GameAdapterComposition` 267, `ItemGrantSource` 73, `ItemIdAllocator` 110; the dead forwarding member on the aggregate AND the adapter's now-dead `IPlayerInteractionVisibility` forwarder were deleted; the allocator's widened type visibility is stated and limited in its own class doc (every game-facing member stays internal)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0; `dotnet format` exit 0; the full suite in `.acceptance/20261017-q/fix-full-suite-3.log` records 693/693 normative gates and 5697/5697 behavioural cases, 0 failed
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
