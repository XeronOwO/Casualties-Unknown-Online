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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: (reset for the S3b cycle) the change adds no gesture: it makes the use CLAIM a wire kind of its own (`WireCommandKind.ItemUse`) for the two native `Body.UseItem`/`UseItemInHand` call sites, and gives the gun chain's persistent transitions (`GunStateSync`, no use) their own state-report sender — no user-visible behaviour changes beyond a forged use report being refused by name and settled
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: (reset for the S3b cycle) the game's `usable` guard (`Body.cs:2452`, `:2477`), the registry read (`Item.GlobalItems`), the seam's rule order, the handler's creation heal for the new kind, the settlement and the gun chain's state route each carry a code citation and a named test in the ticket's S3b self-check table
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: (reset for the S3b cycle) the family is every report that claims a native action: the two guarded uses (`ItemUse`, bounded by `usable`), the gun chain's transitions (`ItemUpdateState`, moved off the use kind by the review), the console verb (the host's rule), and the limb families (their record-based bounds) — each bounded or named, and the state NUMBERS named as the anchor-less holder claim rather than given a false bound
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (reset for the S3b cycle) the ticket's S3b table carries seven rows (the use kind, the state route, the data anchor, the refusal's name/answer, the way back, what did not change, not covered), each naming its case; every named test is a `[Fact]` under `tests/` (the backlog anchor gate reads them)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: (reset for the S3b cycle) nine new cases: five in `KernelItemReportEligibilityTests`, three in `CommandAdmissionIntegrationTests`, one in `ItemActionSyncTests`; three mutation controls observed RED (2 / 1 / 1 against the 35-case two-suite filter); the machine row (acceptance row 7) is owed and named in the ticket
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: (reset for the S3b cycle) the handoff ordered this ticket's S3 second half; the ticket's own S3 paragraph fixes the direction (one data anchor per family, read through an adapter port) and decision 262 (data validation only) is what every rule obeys; no new design question was opened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: (reset for the S3b cycle) measured after the change: `ItemService` 546, `KernelCommandGateway` 464, `KernelProtocolCommandHandler` 566, `ItemCommandRejectionRouter` 125, `ItemStateSettlement` 115 — all under the 600 gate; the refusal routing was split out of `ItemService` at the gate rather than recorded as debt; no new state flag; the independent review's round one (1 major + 2 minors) is fixed in this commit, and the delta round started on the fixed tree did not return a report (recorded in the ticket, with what was re-verified instead)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: (reset for the S3b cycle) `dotnet build` 0 warnings 0 errors, `dotnet format` exit 0, `dotnet test` green at 693/693 gates and 5599/5599 behaviour cases
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
