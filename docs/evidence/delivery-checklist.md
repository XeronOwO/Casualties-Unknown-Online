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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the ticket's research and *Fix landed* sections name the native family (`SplintLimb.TakeOff`, `TourniquetScript.TakeOff`, the splint/tourniquet `useLimbAction` delegates in `Item.cs`) and the cross-player counterpart (the delegate run on the affected side, with the host-side removal a documented deviation); the take-off's identity is the applied row keyed by (owner, limb, definition) — an exact authority key, no fallback — and the cross-player "became component" is not derivable by the host, so it is reported and admission-anchored (census row `NetMsg.PlayerItemActionOutcome`); no new user-visible decision was introduced
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's *Fix landed* names every mechanism and its owner — the outcome flag (`PlayerItemActionOutcomeService`), the command/event/`WirePlayerInteraction` 27/codec chain, the marker (`PlayerInteractionApply`), the take-off scope (`LimbItemTransform`), its two silenced hooks, the refusal router, and the removal data (`SendItemRemovedFromLimb`); each has a live test or a structural gate, and the Unity-side handler is acceptance-only (a test host has no game `Item`/`Body`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both apply directions (host owner and guest owner through wire 27) and both removal sides (the local take-off and a refused relocation) were aligned; the pickup, container-load and pickup-rollback families were read end to end — the rollback was the killer the batch observed, so the refusal router now settles the limb commands by naming them; the applied family's display/codec gate (`LimbComponentApplyGateTests`) was checked for overlap and stays untouched
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's nine-row *Self-check: mechanism × change × evidence* table names a live test or structural gate for every mechanism, with the acceptance rows I1/I2/I4 (and R1 for the sibling heal ticket) carrying the Unity-side half
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the unit reds (the two refused-relocation cases and the self-contradictory pair, each observed failing before its fix) plus the gate's four mutation proofs cover the wiring without a game; the live proof is the acceptance re-run's I1 (the applied row survives, no destroy report) and I4 (the taken-off item stays live and carried) and R1 (the native heal survives a save/reconnect) on the deployed artifact, with the clients' own `[ItemUse] … installed …` / absence-of-destroy / absence-of-refusal / absence-of-reconcile-kill lines as the traces
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the session handoff orders this fix as the round's first work item ("做 `docs/backlog/review/item-identity-across-transforms.md` 的修复") and names both defects, the acceptance re-run and the sibling row to judge; the fix implements the ticket's frozen ruling (decision 259, option 1) and no open fork was reopened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched types stay under 600 lines — `PlayerInteractionApply` 500, `ItemMessageFlowService` 400, `BodyItemPatches` 340, `IItemControl` 334, `LimbItemTransform` 242, `PlayerItemActionOutcomeService` 181, `ItemCommandRejectionRouter` 145, `ContainerItemPatches` 94; no new state bool (the take-off scope is a `CallContext` origin, the marker already existed), and the one gate grew by one structurally checked fact rather than a second mechanism
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0 (0 warnings); `dotnet format` exit 0; the full suite is 5840/5840 behavioural and 741/741 normative gates (`.acceptance/test-full-review-fixes.log`, `.acceptance/gates-review-fixes-final.log`), and the focused runs, the two unit reds and the gate's four mutation proofs are in `.acceptance/red-limb-refusal.log`, `.acceptance/red-inconsistent-pair.log` and `.acceptance/mutation-a…d.log`
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
