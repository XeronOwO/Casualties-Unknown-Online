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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the give verb's entry is unchanged (CUO's own console surface, the game's prefab capture and the landed push); this cycle changes no placement behaviour, and the destination ladder the user ruled on 2026-10-17 is recorded in the ticket as the NEXT unit rather than half-implemented here
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: four mechanisms in the ticket's *The defect fixes this ticket reopens for* — the prefab liquid capture (`ItemStateCodec.CaptureLiquids`, runtime red `d33-give-exception.log`), the slot resolver's tag (`PlayerInventoryTakeService.DeliverCreatedItem`), the answer's docs (`PlayerInventoryGiveResultMsg`, `PlayerItemGiveService.SendAnswer`) and the answer's sender anchor (`PlayerInventoryGiveResultHandler`, `InventoryIntentRefusedHandler`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the sender anchor was applied to BOTH host-to-guest answer handlers (`PlayerInventoryGiveResultHandler`, and the pre-existing gap in `InventoryIntentRefusedHandler`); the prefab capture was swept for other nullable collections (only `WaterContainerItem.stack` is one; `Contents` iterates the transform's children and the components come from `GetComponents`)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the row-2 defect's runtime red (`d33-give-exception.log`) and the sender-anchor test's red/green pair (`CommandConsoleMemberItemGiveTests.AnAnswerFromANonHostSender_IsDropped`: `.acceptance/20261017-q/red-sender-check.log` failed with the check neutralised, `green-sender-check.log` passes with it; 26/26 focused cases green in `fix2-focused.log`)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the liquid fix's proof is the rejected row re-run on the deployed artifact (`/give @p waterbottle liquid water 500` answering `liquid water … -> 500 ml` with the member reading 500 ml); the sender anchor's proof is the two-node test with a third peer, which is the run's own producer of a non-host frame
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the acceptance batch's rejection names the row and the fix is the normal defect path; the destination ladder is the user's own 2026-10-17 ruling (quoted in the ticket), not a choice this cycle had to ask about
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched owners are under the gate (`ItemStateCodec` 501, `PlayerInventoryTakeService` 455, `PlayerInventoryGiveResultHandler` 35, `InventoryIntentRefusedHandler` 45, `IPlayerInteractionHandlerContext` 14); the answer handler gained the logger the sender check needs, and no state bool was added
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0; `dotnet format` exit 0; the full suite in `.acceptance/20261017-q/fix2-full-suite.log` records the behavioural suite 5698/5698 (the new sender-anchor case included) — the normative run in that same log was 692/693 with this box itself the one failure, which the next run after checking it closes
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
