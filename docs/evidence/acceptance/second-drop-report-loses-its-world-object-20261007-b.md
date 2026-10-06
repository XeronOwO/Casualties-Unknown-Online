# Acceptance record — A second drop report at the same position loses its world object

- Ticket: `second-drop-report-loses-its-world-object` — verdict: **moved to `done/`**: the adopt path's
  tie-break is pinned and green in this run, and the three-client row that failed in batch `20261006-h` now
  passes 2/2 — both same-frame children of one expansion stand as their own world objects on the operator and
  on the third peer, with no `terminal` entry and no kernel refusal for either.
- Batch: `20261007-b` — tickets `second-drop-report-loses-its-world-object` and
  `drop-pending-single-slot-overwrite` (the sibling ticket's producer, whose re-scoped row this run also
  judged)
- Commit: `208bab68` (the run's tree; the two commits above the fix commit are documentation-only) · Deployed
  artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+2675221cc96cbe1e8bc39a84e5ac1dfbeab21671`,
  verified against this tree's build output before the clients started
- Run: 2026-10-07 00:28 → 00:34 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` (the
  owner) · Third peer: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`)
- Artifacts: `20261007-b/` in the directory named by `acceptance-artifacts-dir` — the two releases
  (`r1-host-release.json`, `r2-host-release-repeat.json`), the host's authoritative tables (`w0`, `w1`, `w2`),
  the two viewers' clone views (`c2-*`), the fixture trees (`f-tree-before`, `f-tree-before2`), the three log
  marks (`marks-h1-before.txt`, `marks-h2-before.txt`, `marks-h3-after.txt`) and the local log helper
  (`log.ps1`)

## Batch scope

The run re-drove batch `20261006-h`'s fixture on the deployed artifact — the owner carries a `trashbag` whose
one nested container is an EMPTY `plasticbag`, and a `duffelbag` holding two light `dogfood`s; the operator
opens the owner's `trashbag` as a remote container panel and releases the source bag's proxy onto the nested
bag's panel button with the game's own `expanddesc` bind held. The sibling ticket in this batch is the row's
producer and is judged from the same two gestures. No other ticket in `review/` is served by this run.

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | **Unit / pinned behaviour.** The adopt path's tie-break is a named rule with a regression case: an id-less same-prefab world copy at the reported position is either adopted and SURVIVES, or refused and the row materializes beside it | machine | **pass** | This run's own test runs against the frozen tree: `dotnet test tests\CasualtiesUnknownOnline.Tests … --filter "FullyQualifiedName~AdoptTarget"` → **9 passed / 0 failed** (`tests/CasualtiesUnknownOnline.Tests/Items/AdoptTargetRuleTests.cs`, the truth table plus this defect's exact object read clause by clause) and `dotnet test tests\CasualtiesUnknownOnline.NormativeGates.Tests … --filter "FullyQualifiedName~AdoptTarget"` → **26 passed / 0 failed** (`tests/CasualtiesUnknownOnline.NormativeGates.Tests/AdoptTargetGateTests.cs`: the scan's rule call, the rule's clause set, the display-proxy predicate's two markers with their inactive coverage, the four classifiers at a census floor of four, and the six domain paths at their own floor). The mutation (`!retired` dropped from the rule) was run in the fix cycle and is a process record, not re-run here — see `Limits` |
| 2 | **Runtime (three clients).** One gesture, two same-frame children, BOTH ids in the host's world table, BOTH materialized on the third peer, no `terminal` entry, no `InvalidTransition`, the owner's own reading unchanged, and the second child's line shape changed | machine | **pass (2/2)** | `w0-host-tables-before.json` (world 259, terminal 0) → `w1-host-tables-after.json` (**world 261, terminal 0**) → `w2-host-tables-after-repeat.json` (**world 263, terminal 0**), with `22377858709`+`26672826005` and then `30967793301`+`35262760597` each in `world` and none in `terminal`; the third peer writes its OWN `[ItemSpawn] materializing dogfood (id 22377858709) at (0.6,423.0)` 00:30:04.340 (sibling .349) and `[ItemSpawn] materializing dogfood (id 35262760597) at (-1.2,423.4)` 00:30:50.731 (sibling .739), each preceded by `[ItemDrop] … not present — requesting materialization`; the operator writes the same pair on its own view (`[ItemSpawn] materializing …` 00:30:04.325/.332 and 00:30:50.708/.713). **No `[ItemBind]` line for any of the four ids on any client**, no `[ItemTrace] … origin=OnItemDestroyed`, no `Kernel command rejected` and no `InvalidTransition` anywhere in the session on all three clients; the owner still re-places both of its own objects (`[ItemDrop] dogfood (id 22377858709) present — re-placing at (0.6,423.0), container 0.` 00:30:04.343) and both viewers' clone trees hold the owner's proxies with `orphanCount: 0` (`c2-host-clone-owner.json`, `c2-alt-clone-owner.json`) |

## What the run read about the defect

- **The failure mode this ticket attributed is gone, on the same gesture and the same roles.** Batch
  `20261006-h` read, on both viewers and in two independent runs, the second report taking
  `[ItemBind] bound existing dogfood at (3.1, 490.3) to id … (no materialization)` followed 10–30 ms later by
  the adopted copy's destroy (`origin=OnItemDestroyed`), leaving the id `terminal` in the host's kernel and the
  guest's own `ItemDestroy` refused as `InvalidTransition`. This run reads the changed shape on all four
  children and both viewers: its own `[ItemSpawn] materializing`, no bind, no destroy, no `terminal`, no
  refusal. A whole-session scan of all three logs for `InvalidTransition`, `Kernel command rejected` and
  `OnItemDestroyed` returns `NO MATCHING LINES`.
- **The adopt path still adopts, which is what the rule is for.** The `[ItemBind]` lines the session does carry
  are the world-entry shape on the two guests only, for six world-generated items the local scene already holds
  (`dynamite`, `medicalsuture`, `scrapmetal`, `makeshiftwrench`, `woodpitchfork` and a world-generated
  `duffelbag`, all at far-away positions, 00:29:16). None of them carries a fixture id, and the host — which
  materialized rather than adopted — has no bind line at all. So the fix bounds the CANDIDATE and did not turn
  the scan into "never adopt".
- **The zero-monitor criterion of the sibling row is unaffected by the fix.** Both gesture windows and a
  ~35-second quiet window (00:31:40 → 00:32:15, the monitor's 1 Hz snapshot cycles) read no `[CharSync]
  divergence` on the operator or the third peer; the four lines per viewer that the whole session does carry
  are the fixture staging's create shape at 00:29:34/.36 and 00:30:36/.37, all before their gesture's mark.
- **The fix's own pins are the two classes the ticket names, and both are exercised in this run's test runs.**
  `AdoptTargetRuleTests` reads the rule clause by clause, including this batch's exact object, and
  `AdoptTargetGateTests` holds the wiring, the predicate's inactive coverage and the six domain paths that must
  never address a display proxy — the door the batch's destroy was reported through among them.

## Residuals for the user

None: both rows are machine rows, and each names its evidence.

## Limits

- **The mutation was not re-run in this batch.** The ticket's unit row asks for a case whose failure without
  the rule is shown; that mutation (`!retired` dropped) was run in the fix cycle and is recorded in the
  ticket's `## What landed` as a process result. This batch ran the pins, not the mutation: a batch does not
  change code, and a mutation is a source change. The pins' own cases do fail on the unfixed shape — that
  reading is the fix cycle's, not this run's.
- **One owner direction, one fixture, two gestures.** The owner was the sandbox guest and the operator the
  physical-machine host; the mirrored direction was not driven, and both gestures used the same target
  definition in one world. How often another world presents an id-less same-prefab copy inside the adopt
  tolerance is not measured here.
- **"Found by the third peer" is judged from the adapter's own account lines.** The row writes it that way (an
  `[ItemSpawn]` or an explicit bind line each); no live per-receiver object census of the four ids was taken.
  The ad-hoc probe written for one could not be delivered — the local probe client's submissions that carry a
  string literal are refused by the evaluator, while the committed recipes run through the driver's channel.
- **No wire, save or feel reading.** No messages and no saves were read, and no hand-feel was judged.
