# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — the block relay lands asynchronously, so a one-shot member read is a race
- [ ] Native parity — a trader is an animal row in the enemy table, and its blast answer is a refusal path
- [ ] Native parity probe — the mine explosion does not carve its own cell (block 2 before and after)
- [ ] Entry mapping — the mine row's entry is `trap-stage` + `trap-trigger`, its witness must be a building
- [ ] Mechanism inventory — the breakable-cell scan is now shared (`CaseContext.FindBreakableCellAsync`) by `BlockSyncCase` and the mine row
- [ ] Whole-family audit — both fixture flakes in the run (block-sync's single read, enemy-kill's trader pick) are fixed together
- [ ] Self-check table — build 0 errors, gates 834/0, suite 28 passed 0 failed
- [ ] Verification design — the suite's own report is the judge
- [ ] Plan approved by the user — night mode: rows converted with readings, commits per slice
- [ ] Structure review done — the second caller of the finder pulled it into the context
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
