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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new player gesture and no new entry path: the change reorders the reports of three native calls the adapter already mirrors — `Body.WearWearable` (`Body.cs:1480-1518`), `Body.SwapSlots` and `Body.SwitchHands` (`Body.cs:1113-1133`) — and carries nothing the gesture cannot (the item and the two slots are the operands; no limb, target or identity is guessed), so no fallback exists; the only user-visible effect is the clone fact table no longer warning on a normal wear/swap, which is this ticket's own acceptance requirement, named as the round's first work item by the handoff
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## Mechanism` and `## The fix (this cycle)` plus the selfcheck's eight-row table read each: the three postfixes and their native calls (`Body.WearWearable`, `Body.SwapSlots`, `Body.SwitchHands`), `OnInventoryChanged`'s caller-order contract and its pending-drop hold, `CloneFactTable`'s quiet/warn halves, the reliable-ordered send chain on the host and the guest→host→third-guest path, the six `OnInventoryChanged` callers, and the craft sibling the review found (filed, not merged); the session half is the acceptance row and is not claimed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every `OnInventoryChanged` caller was walked (six: the three changed postfixes, `DropWearablePatch`'s pending-drop case, `RecipeTryMakePatch` / `BodyCombinePatch` committing their craft report first); the `ReportInventoryChanged` receiver-side sites were read and send no item fact of their own, the host/kernel commit being the fact; the craft-scope wear the walk turned up is filed as `todo/craft-carried-fact-retirement.md` with its code path and acceptance row instead of being merged or recorded as a limit
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/item-report-order-selfcheck.md` — eight mechanism rows, the change, the review's major finding and six minor/nit dispositions, and what the cycle did not verify, each naming its file, test or batch
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is `CloneFactTableDivergenceMonitorTests` (event-before-snapshot quiet; snapshot-without-event warns) plus the comment-aware source-shape pin `InventoryChangeReportOrderPinTests`, whose red was observed on the pre-fix tree (`WearWearablePatch: … reported after the snapshot`) and whose matcher carries positive, inverted and comment-only samples; L1 is the ticket's three-client row (the batch after the commit): the member wears a bag child, then the hand swap and the `SwapSlots` swap over occupied slots, with every viewer at zero divergence and each moved item's `[CarriedSync] applied` line in the window
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff's work order names this ticket as the round's first work item and its stated fix shape (the item fact before the snapshot, a source-shape pin, the three-client row); no new user-visible choice was invented — the report order is the ticket's own acceptance requirement, not a new surface
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `GameAdapterBridge` 598, `IPatchBridge` 599, `BodyPatches` 510, `BodyItemPatches` 346, `InventoryChangeReportOrderPinTests` 282 (all <= 600); no state boolean was added, no dead mechanism was found to delete, and every added mechanism names its reader (the pin ← the three postfixes; the filed craft ticket ← the review finding; the selfcheck ← the checklist)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build` 0 warnings 0 errors; normative gates 751/751; behaviour suite 6117/6117 — rerun after the independent review's major finding and the six minor/nit fixes
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
