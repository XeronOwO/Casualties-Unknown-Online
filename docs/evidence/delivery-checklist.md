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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry and no native counterpart exist here: the cycle retypes a mod-facing contract and moves one framework seam between layers, adds no player-facing gesture and changes no behaviour a player can see, so there is nothing to approve
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the self-check §1 — `IModNativeApi`, the Runtime → Game Adapter native-API seam, `DisabledModNativeApiProvider`, `ModNativeApiPolicy`, the Game Adapter's provider half, the moved `IStartingSupplyBehaviour` and its two referencing files, each naming the case or gate that covers it
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the self-check §2 — the contract-wide `object` scan (two BCL overrides left, everything else a doc-comment word), the zero-reference check for `TryInvoke`, the moved seam's three referencing sites, the same-shape-same-layer check against `IModItemSpawner`, the ticket's third row re-checked rather than assumed, and decision 249's net48 array measurement carried into 250 instead of dying with its code
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the self-check §1 (nine rows, each naming its case or gate), §5 (the independent review's twelve findings and their dispositions, majors first) and §6 (the limits)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the self-check §4 — the contract shape is proven by the baseline gate plus the review that updates it (rule 15's deliberate no-scan design, tested end to end here for the first time), the typed projection's three failure paths and its happy path run against the real composition root through `FakeModNativeApiProvider`, and the Game Adapter's own implementation is covered reflectively because the test project cannot compile-reference it; no row needs a game process
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's source is the user's 2026-10-08 sweep ruling, the handoff orders it first and names its one open judgement ("开工前先裁 `IStartingSupplyBehaviour` 算不算模组契约") as the agent's to settle, and decision 250 records both that ruling and the effects-surface rule it settles
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 with `git status` showing no file it wanted to change; behaviour 4850/4850 (net48); focused 64/64 with the full filter syntax; gates 572/573 through the cycle with the only red being this checklist's own required boxes, and 573/573 once they are filled
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `ModNativeApiPolicy` 39 lines (was 133), `ModNativeApiAdapter` 48, `IModNativeApi` 47, `IModNativeApiProvider` 27, `IStartingSupplyBehaviour` 46, `ModNativeApiTests` 149 — all far under the gate's 600; no new state bool; the DEAD mechanisms went with the change (the generic invoke path on both sides of the seam, the value surface with its four caps and its array rule) and no shim was left beside the typed projection; `GameAdapter.cs` 588 (was 593) is named in the ticket as the one thin margin
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
