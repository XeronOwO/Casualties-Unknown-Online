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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no CUO gesture; the model inputs it feeds are the native stage reads `Body.cs:669` (in-water breath), `:2994` (the moving/exercising/climbing stamina branch), `:3088` (in-water compute) and `:3086` (the climb lookup), each quoted in the ticket's unit-2 table; the one user-visible consequence is a refusal on the stream ingress (a bound on an existing claim, not a new gesture); the session handoff ordered the unit
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the four inputs and every native read are cited in the ticket; the wire bits, mapper and publish path are covered by the roundtrip table and source pins; the ingress identity bound is covered by three `StateStreamTests` rows (red observed with the guards removed); the live effect of the inputs on the model's numbers and the patch installation stay explicitly unverified (two-client acceptance owed)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the input family is all four model reads (in-water, moving, exercising, climbing) and the stream's every receive direction was checked (a member's row from another sender, a guest's own row, a third peer); the presentation restore covers all four fields; the review's mirror holes are closed in the same commit
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `### Independent adversarial review (2026-10-18, frozen tree) — unit 2` section lists M1, M2 and every minor/nit with the code it cites and the fix each received, plus what stays unverified
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: pure rules (`HostVitalsInputsTests`), the wire roundtrip table (`EntityStateRoundtripTests`), source pins with executable negative samples (`HostVitalsSuppressionPinTests`: `PassRunsInsideTheInputScope`, `ScopeShapeHolds`) and the fake-network ingress rows (`StateStreamTests`); the live proof is the ticket's two-client acceptance rows (owed)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-18 ruling froze the host-computes direction, and the session handoff ordered Stage 2 item 2 as this cycle's first work item
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `HostVitalsInputs`/`HostVitalsInputScope` are two files (one top-level type each, 80/81 lines); `RunCoordinator` (611) is recorded in `docs/architecture-debt.json` with the planned split; `HostVitalsBodies` is 294 lines; no type exceeds 5 boolean state fields (`HostVitalsDriver` is at 5)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` exit 0 (0 warnings), `dotnet format CasualtiesUnknownOnline.slnx` exit 0, and the final unfiltered `dotnet test CasualtiesUnknownOnline.slnx` is 5947/5947 behaviour tests and 741/741 normative gates green (`.agent-local/test-full-item2c.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
