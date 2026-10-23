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
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the wound view's own apply (`PlayerCamera.TryPerformSpecialUIAction`'s `WoundViewLimb` branch → `ApplyWoundItem` → `useLimbAction`), and the change admits the report the game's own action already produces; no new gesture, window or key. The review corrected the premise the earlier draft carried: the bag panel and the wound view are mutually exclusive (`OpenContainer` is reached only from `HandleReleaseDragging`, which closes the wound view, and `ToggleWoundView` refuses while the radial is open), so the reachable shape is a LAGGING kernel row (the item in hand, the record still `Contained`), not a panel-to-wound-view drag — the ticket now records that sequence and the change is scoped to it
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the native path is decompiled (`PlayerCamera.cs:1656-1659` `WoundViewLimb` → `ApplyWoundItem` at `:739-763`, `useLimbAction`); the report path is the adapter's `LimbItemTransform.OnItemAppliedToLimb` → `IItemControl.SendItemAppliedToLimb` → `WireCommandKind.ItemApplyLimb`; the seam is `KernelCommandGateway.MayInstallOnOwnLimb` and the sibling `MayReportOn`, plus `KernelProtocolCommandHandler`'s container-sync home check; the domain acceptance is `LimbItemLocationRules.DecideApply`; the chain fact is `ItemLocationChain.HolderOf` (its doc says exactly "the child's owner only at creation … the CHAIN always names who holds the root") and its fail-closed branches (missing parent, cycle, self-parent, applied/terminal/world root) were read branch by branch
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the install switch decides every location family (`Carried`, `Contained` via the chain, `Applied` on the same limb, `World`/`Terminal` refused); the SAME owner-field defect was found and fixed one rule over in `MayReportOn` (state/destroy/container-sync parent) and in the handler's container-sync home check, both now reading `HolderOf` while keeping world-rooted content reportable; the remove rule and the container-sync destination read were re-read (their rows are `Carried`, whose owner is current, so the field is the right fact there); the world-row half is a recorded decision point, not a settled line. The earlier audit claim ("MayReportOn already uses HolderOf") was false and the review corrected it — the fix makes the sentence true now
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (1) the contained child in the sender's own bag — admitted (new case, RED before the seam fix `.acceptance/20261024-c/limb-red.log`, green after); (2) another member's bag — ignored; (3) a nested chain one hop deeper than the bag check — ignored (a one-hop read would pass); (4) a real WORLD-row child and a ground world row — refused by kind (the world half is the decision point, recorded); (5) the changed-hands report family — the new holder admitted and the stale owner ignored, both observed RED on the old owner-field arm (`.acceptance/20261024-c/owner-red.log`) and green after; (6) the kernel half — a contained child applied through the limb report becomes `Applied` and survives the bag's next container report (`ContainerTakeOutTests`); (7) the chain walk itself — seven cases pin the carried root and every fail-closed branch (`ItemLocationChainTests`); `KernelCommandGatewayTests` 34/34, `KernelItemReportEligibilityTests` 26/26, the three suites together 70/70 (`.acceptance/20261024-c/limb-focused3.log`), chain tests 7/7, full behaviour 6357/6357
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the seam suites plus the kernel-level relocation test (the red-first cases above); L1 is the ticket's machine row — the lagging-record shape on two clients (the item in hand, the record still `Contained`), the host's row ending `Applied` on that limb, the bag's next report leaving it `Applied`, and the take-off relocating the same instance id, read from the host's kernel row and the peers' clone (NOT the `[ContainerSync] … no longer names` line, which only the host's own carried-sync path writes). That row is OWED: it needs the lagging record staged on a client plus the native wound-view apply, which the driver's UI cannot drive, and a probe-side call would not be evidence of a reachable gesture (the review corrected the earlier "reflect-set dragItem" plan)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket froze the rule in its own text ("an item the record holds inside the member's OWN container is one that member may install on its own limb — the home row decides, never the child's own recorded owner") and asked for both shapes to be decided together, which this change does; no user-visible choice is introduced (the world-crate refusal keeps a report the game never made as an outcome)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `KernelCommandGateway` 552 lines, `KernelProtocolCommandHandler` 595 (both under 600; the handler gained one comment line, no split owed); `KernelCommandGatewayTests` 34 cases and `KernelItemReportEligibilityTests` 26, both under the 40-case test-class gate; no state added (the three rules are pure reads over the existing facts); the owner-field arm was replaced by the chain read, not kept beside it; one source per fact — `ItemLocationChain.HolderOf` owns "who holds this chain" and all three rules ask it
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/20261024-c/limb-build3.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/limb-format3.log`); the final full run after this checklist was completed is `.acceptance/20261024-c/limb-final-full4.log` — behaviour 6357/6357 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
