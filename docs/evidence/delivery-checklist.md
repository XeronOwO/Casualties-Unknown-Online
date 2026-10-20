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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture: the change completes the craft entry the adapter already mirrors (`Recipe.TryMake` → `RecipeItem.UseItem` → `RecipeResult.SpawnResult`, `Body.WearWearable`) by retiring the row its own report names; the operands (owner id, instance id, disposition) all ride the report, no limb/identity fallback is added, and the review re-read the native call sites line by line (`Body.cs:494-529`, `:1301-1311`, `:1441-1452`, `:1480-1518`); the one user-visible effect — no spurious clone-fact warning and no lost clone row — is the ticket's own acceptance requirement
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## Mechanism` and the selfcheck's seven-row table read each: the native consume/spawn/wear chain (`Recipe.cs:172-193`, `RecipeItem.cs:125-186`, `RecipeResult.cs:9-77`, `Body.cs:494-529/1441-1452/1480-1518`), the craft scope's destroy claim, `CloneFactTable.WarnOnDivergence`'s missing-entry loop, the host/relay apply split and the host-own branch, the reliable single-channel ordering chain; the runtime half (acceptance batch `20261025-d`) is the ticket's row and is not claimed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every in-craft snapshot caller was walked (only `WearWearablePatch`; `DropWearable`/`SwapSlots`/`SwitchHands` are not entered from `Recipe.TryMake`/`CombineItems`), every craft-report apply path was aligned (host apply, relay apply, host's own craft + kernel row), the destroy routing was split into world/scene vs carried/fact vs unattributable/no-action with the owner anchor, the pickup-race sibling is pinned negative (it must NOT retire the winner's row), and the drop path's every-owner removal was left as the drop event's own shape with its comment now naming the two seams
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/craft-carried-fact-retirement-selfcheck.md` — seven mechanism rows, the change, the review's blocker/major/minor/nit dispositions and what the cycle did not verify, each naming its file, test or batch
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the source-shape pin (`InventoryChangeReportOrderPinTests`: the wear withhold with a guard-block matcher, the craft postfix order, the patch-bypass check, the binding route, both apply paths and both stamps; four of its cases were observed red on the pre-fix tree) plus the simulation (`CraftSyncSimulationTests`: host/relay retirement, kernel anchor, race negative, Changed negatives, host-own row) and the fact-table monitor (`CloneFactTableDivergenceMonitorTests`); L1 is the ticket's three-client batch `20261025-d` — a member crafts the `slingbag` from four carried destroyable materials, every viewer at zero divergence over ≥ 1 Hz cycles, the host's apply line, the relay's apply line and each receiver's `[CarriedSync] removed …` line in the window
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket as the round's first work item with its stated fix shape (retire the carried row on both apply halves, skip the in-craft snapshot, pin both, then drive the acceptance row); the only correction made to the ticket is a NATIVE fact (the displaced-wearable premise is false — `Body.cs` quoted in the ticket), not a new user-visible choice
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `CraftSyncService` 596, `ItemService` 598, `CloneFactTable` 599, `CraftCarriedRowRetirement` 49, `GameAdapterSessionBinding` 190, `CraftingSync` 474, `BodyPatches` 517, `IItemControl` 365 (all under the cap without a new debt entry; the attribution + host-own retirement were extracted to `CraftCarriedRowRetirement` when the touched type crossed the cap); `CharacterDataSync` was already recorded debt (683) and its owner-addressed delegation takes it to 686, recorded with the reason in `docs/architecture-debt.json`; no state boolean added; the relay's old "heals via the 1 Hz snapshot" comment and the stale ticket pointer are gone, and each added mechanism names its reader (the event ← the binding; the stamps ← the relay; the pins ← the two test classes; the ticket ← the batch)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; behaviour suite 6138/6138; normative gates 750/750 with this box the only one excluded (the checklist's evidence-run rule), and the full-project rerun after it was checked reports 751/751
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
