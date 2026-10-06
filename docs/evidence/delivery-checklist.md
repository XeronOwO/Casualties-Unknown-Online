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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `retired-display-proxy-adopt-selfcheck.md` §1 — eleven entries: the drop report's adopt path, the clone renderer's retire step (`Container.UnloadItem` detaches and hops 1.5 = `AdoptTolerance`, then deactivate + deferred `Object.Destroy`), `Item.allItems`' add/remove window, the inactive-object lookup hole, the destroy-report guard that let the destroy be reported at all, the kernel's `terminal`/`InvalidTransition` consequence, the two excluded candidates, the four-classifier/six-domain-path family, the input paths deliberately left, and the fifth classifier filed as its own ticket — all quoted from batch `20261006-h`'s three logs (still on disk), its artifact `w2-host-tables-after-repeat.json`, and `reversing/`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 rows 9-11 and §2 — the one predicate now guards the four classifiers that take authority over an id-less standalone world item (adopt scan, reconcile late-local sweep, generation publish, restored-cut leftover sweep) AND the six domain-path tests that must never address a proxy (three `FindWorldItem` lookups, `OnItemDestroyed` — the independent review's finding, and the door the batch's destroy was reported through — and both `BindToContainer` candidate loops); the input-path guards are named with why they keep their own marker tests (a click cannot reach an inactive object), and the family's fifth classifier is filed with its fixture instead of half-fixed
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/retired-display-proxy-adopt-selfcheck.md` §3 — five rows: the rule's truth table (`AdoptTargetRuleTests`), the wiring and both censuses plus every matcher (`AdoptTargetGateTests`), the pre-fix RED (four shape facts, names not counts), the mutation (`!retired` dropped → three cases fail), and the runtime row named as the next batch's re-drive of `20261006-h`'s fixture
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the static half is `AdoptTargetGateTests` (read RED on the unfixed tree: the missing rule call, the missing clause set, the missing predicate, and no display-proxy test in any of the four classifiers — a session measurement, recorded by name because the gate grew its clause-identifier theory afterwards) plus `AdoptTargetRuleTests`; the runtime half is the next three-client batch re-driving the batch's own fixture and reading BOTH ids standing in the host's world table, BOTH materialized on the third peer, no `terminal` entry and no `InvalidTransition` — the second child expected as its own `[ItemSpawn] materializing …` with no `[ItemBind] …` for the proxy the renderer retires in that frame
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this cycle's handoff names the fix of `second-drop-report-loses-its-world-object` as the first work item, with its reason (the batch's own fixture and driving path are already recorded, so the re-judgement is cheapest) and the instruction to attribute before changing anything; the work-item order is the agent's per `AGENTS.md` rule 9; the ticket's own acceptance left the fix's shape to this cycle; no user question was asked this cycle
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` clean (0 warnings, 0 errors) through the filtered full run; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` = gate project 416/416 and behaviour 4646/4646 on the frozen tree (this checklist's own gate is the one filtered out and is proven by the focused gate run that follows this edit)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: physical lines — `AdoptTargetRule` 64 (new, one type, pure), `RemoteItemSceneOps` 488 (the scan's five guards became one rule call), `ItemWorldSync` 518 (+22: the predicate and the destroy guard's rewrite), `ItemReconcile` 241, `GeneratedItemAuthority` 207, `GeneratedItemReconcile` 142, `AdoptTargetGateTests` 314, `AdoptTargetRuleTests` 87 — every file under the 600-line gate; no new state and no new bool (the added members are the pure rule, the predicate and the gate's own constants); the adopt scan's dead guard list was REPLACED rather than kept beside the rule, and the independent review's findings were fixed in the same round instead of deferred — the one thing it leaves open is named, not hidden: the family's fifth classifier is a filed ticket (self-check §1 row 11)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
