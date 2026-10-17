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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: (reset for the container-sync cycle) the change adds no gesture and bounds a report whose native call site is `Container.LoadItem` (`Container.cs:116`) → `ContainerItemSync.OnLoadedIntoContainer` → `IItemControl.SendItemContainerContent`; no user-visible behaviour changes beyond a forged report being refused
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: (reset for the container-sync cycle) the three record arms, the statement fact, the kernel's container decision, the seam's two eligibility rules and the client settlement each carry a code citation and a named test in the ticket's self-check table
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: (reset for the container-sync cycle) the family is every write this wire kind can make — the parent row, each child's destination, a known child's current container, the omitted-descendant destroy arm, the missing-parent heal and the creation claim — and each is bounded or named in the ticket row; the spawn report stays the filed `ItemSpawn` class
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (reset for the container-sync cycle) the ticket's container-sync row carries the table: six mechanisms × change × the named case, each test name matching a `[Fact]` in `CreationBeforeOperationTests`
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: (reset for the container-sync cycle) twelve cases in `CreationBeforeOperationTests`: ten observed RED (four in the first run, six by mutation control) and two guards; the two-client acceptance row is owed and named in the ticket
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: (reset for the container-sync cycle) the row and its anchor come from the ticket this file records as the S3a follow-up, the order from the user's handoff, and decision 262 (data validation only) is what every arm obeys; no new design question was opened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: (reset for the container-sync cycle) measured after the change: `ItemService` 595, `KernelProtocolCommandHandler` 565, `KernelCommandGateway` 414, `ItemSimWorld` 374, `CreationBeforeOperationTests` 584 — all under the 600 gate, and `ItemService` owes the settlement-policy split before it grows again; no new state flag; the independent review's round one (1 blocker + 5 majors + 3 minors) and round two (1 blocker + 2 majors + 4 minors) are all fixed in this commit
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: (reset for the container-sync cycle) `dotnet build` 0 warnings 0 errors, `dotnet format` exit 0, `dotnet test` green at 668/668 gates and 5586/5586 behaviour cases
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
