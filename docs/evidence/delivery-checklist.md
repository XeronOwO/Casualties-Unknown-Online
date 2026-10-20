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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new player gesture and no new entry path: the report is the arrival half of two native calls the adapter already mirrors — `Body.PickUpItem`'s internal `container.UnloadItem(item, null)` (`Body.cs:1393-1401`) and `Body.WearWearable`'s (`Body.cs:1500-1508`) — and it carries nothing the gesture cannot (the item's own identity is the operand; no limb, target or identity is guessed), so no fallback exists; the one user-visible change — an item taken out of a bag is no longer killed by the bag's next report — is this ticket's own acceptance requirement, named as the round's first work item by the handoff
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## The mechanism` and the selfcheck's seven-row table read each: the unload/pickup bracket (`Body.cs:1393-1401`, `PickupSync.OnPickedUp`), the wear's same cancellation (`Body.cs:1500-1508`, `ItemSlotSync.ReportCarried`), the data-only slot report, the container rows (`DecideSyncContainer`, `AdoptCarriedContents`), the destructive stale walk, the kernel relocation (`TryPickup`), and the changed-hands owner field (`HolderOf`, `done/member-report-bounds.md`); the session half is the acceptance row and is not claimed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every canceller of a departure was walked (`PickupSync.OnPickedUp`, `ItemSlotSync.ReportCarried`, `ItemWorldSync.OnItemDestroyed`, `ContainerItemSync`'s replacement cancel); both container homes got their own report (`ContainerArrivalPolicy`: body-held → take-out, world → pickup) and both adapter seams send it (the seam pin); both pickup-shaped kinds share one eligibility rule (the alias hole the review found); the applied family's wound-view gesture is named as its own ticket instead of merged
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/container-take-out-selfcheck.md` — seven mechanism rows, the change, the review's findings and dispositions, and what the cycle did not verify, each naming its file, test or batch
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the three-node wire simulation (`ContainerTakeOutTests`: the unjudged answer, the relocation, the bag's next report, the changed-hands bag, the alias guard, the world-container shape) plus the pure policy table and the adapter seam pin, and the two pre-fix reds observed (the alias hole on the unmodified gateway; the changed-hands failure under a child-owner rule); L1 is the ticket's acceptance row, driven in-process by a `body-call`-shaped recipe reading the host record, the `[ContainerSync] … no longer names` warning and a third client's clone
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff's work order names this ticket as the round's first work item, and the fix is the ticket's own stated shape (a kernel relocation entry for the arrival, the 2026-10-18 section); no new user-visible choice was invented — the radial-wear half is the same fact at the same gesture family, not a new surface
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `ContainerArrivalPolicy` 53, `PickupSync` 212, `ItemSlotSync` 119, `KernelCommandGateway` 497, `KernelProtocolCommandHandler` 538, `ItemDomainModule` 491, `ItemCommandRejectionRouter` 161, `ItemMessageFlowService` 373, `ItemLocationChain` 179 (all <= 600); the policy is one pure function plus its enum (the decision extracted from the adapter because the adapter has no harness), the superseded `ContainerTakeOutPolicy`/its test and the mis-scoped wear ticket were deleted in this round, no state boolean was added, and every added mechanism names its reader (the policy ← the two adapter seams; the new kind ← the handler's own arm and the rejection router)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build` 0 warnings 0 errors; normative gates 751/751; behaviour suite 6113/6113 — rerun after the independent review's two blockers and five majors were fixed
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
