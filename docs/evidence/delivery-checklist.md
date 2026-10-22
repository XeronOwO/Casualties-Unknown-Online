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

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture; the 2026-10-22 user ruling (view-only middle, red look, giving is an operation) is the design, and the policy only gates existing entries — the ticket's "What the policy is" and the review's M2/M3 (no fallback, no order-based routing; the item's own Container data still picks the give target, `PlayerCameraDragUsePatch.ResolveDisplayProxyTarget`)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's "Independent review" names each mechanism with its reader — the rule surfaces (`HostRulesOptions`/`IHostRules`/`HostRuleSet`/`HostRuleCensus`/`HostRulesMsg`), the host seams (`PlayerRemoteInventoryIntentService`, `PlayerInventoryTakeService.HandleTakeCore`), the client seams (`RemoteBackpackCoordinator`, `RemoteDragIntentDispatcher`), the display patch (`InvButton.UpdateGraphic`) — and the decompiled bases it rests on (`InventorySlot.cs:9-15`, `InvButton.cs:137`, `PlayerCamera.cs:1367`/`:1456`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family was walked on the review — every intent kind through the one host gate, `GiveToBody` judged on the destination, the direct card take, the double-Tab transfer, the give release, the while-dragging drain and the favourite store; its two findings are M2 (the give half of the take core had no reaching test) and M3 (the `canPickUp` display could fire a native drop), both fixed in this commit and recorded in the ticket
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's "Independent review (2026-10-22)" table (3 majors, 10 minors, 4 nits, each with the code it named and its disposition) plus the L0 names — `HostRulesPolicyTests`, `HostRuleCensusTests`, `RemoteIntentRequestTests`, `TakeTests`, `OnlineUiMemberProjectionTests`, `HostRuleReplicationTests` — and acceptance rows 1-8
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the focused suites above (274 cases green, `focused2.log`); L1 is the ticket's acceptance rows on the deployed artifact — the view-only red slots (visual), the rule lines and refusals (machine), the give destination and the live rule change (machine) — judged through `remote-gesture`, `host-rules-read`, `body-force` and the console
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's ruling of 2026-10-22 (view-only middle) answered the ticket's one open design question face to face, and the handoff names this ticket as the round's first work item; no work-item choice was re-asked
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `GameAdapter` 629 (recorded in `docs/architecture-debt.json`), `PlayerInventoryTakeService` 590, `PlayerRemoteInventoryIntentService` 459, `RemoteBackpackView` 290, `RemoteDragIntentDispatcher` 238, `RemoteBackpackCoordinator` 160, `RemoteBackpackNotice` 46, `RemoteBackpackAccessPolicy` 49, `HostRulesMsg` 140, the two patches 49/25; no class over 600; the new state is `RemoteBackpackView`'s two booleans (read-only focus, read-only drag) and the notice's two throttle fields; no dead mechanism added (the old `canPickUp` patch was replaced, not kept)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; behaviour suite 6237/6237 and normative gates 755/755 green (`full-suite4.log` in the local artifact area), the checklist gate included
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
