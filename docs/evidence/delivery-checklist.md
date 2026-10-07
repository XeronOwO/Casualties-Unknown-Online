# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and multiplayer acceptance are the agent's actions,
run after the commit per `docs/acceptance/` and outside this gate; feature
development verification uses simulation/static evidence. When a release cycle
lands, reset the checklist by manually unchecking every box so the next cycle
starts clean.

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
leaves item 8 and FORBIDDEN unchecked, but may write its boxes in one pass. That is the deliberate
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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1: eleven rows — `Body.WearWearable` (`Body.cs:1480-1518`), the `wearable` gate (`ItemInfo.cs:133`), the slot id the occupancy check and the game's own save record (`Body.cs:1590-1607`, `SaveSystem.cs:101`), the limb layout (`Body.cs:3745`, `CharacterDataCapture` index), and the measured family disjointness (all 40 wearables are also `usable = false` and `usableOnLimb = false`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2: nine rows — host admission and placement, the affected side's untouched native restore, the three operator entries, the sibling chains left alone, and the two reviewed surfaces (no new patch port, no new capability)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3: eight rows naming their cases — the item's-own-data placement, the no-placement refusal, both directions, the slot rule, the dismembered limb, the untouched families, the new content gate and the seam wiring
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: focused filter 39 cases green and the new gate 8 green, with BOTH mutations recorded (forced-false predicate and a changed slot encoding each reddened 3 of 6 `WearTests`); the native half (`GameWearPlacement` over `Item.GlobalItems`/`Body.limbs`, the target's restore parenting) named as not L0-reachable, its rows left to a later acceptance batch
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket's remaining chains by the ticket's own order (wear first) and the ticket's Part B table fixes their shape; the answer was measured from the decompiled tree before any code, and the design question this chain raised (empty the occupied slot or refuse) was decided by the recorded evidence and filed as a limit
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format` exit 0, then `dotnet test CasualtiesUnknownOnline.slnx` WITH build: behaviour 4777/4777 (net48, 48 s) and gates 488/489, the single red being this checklist's own pending boxes (the gate is working); the standalone gate run after they were ticked is the green record
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the largest touched types measured by line count: `GameAdapter` 591 (590 at HEAD, +1 ctor parameter), `PlayerItemUseService` 537, `PlayerInteractionApply` 575 (one comment), all inside the 600 architecture limit; the new types are 18-128 lines; dead mechanisms deleted in the same round are `RemoteWearCatalog.cs` (80) and `RemoteWearProfile.cs` (13), with `RemoteWearApplication` keeping its role; no new mutable state and no new bool (the only new field is the injected seam reference)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
