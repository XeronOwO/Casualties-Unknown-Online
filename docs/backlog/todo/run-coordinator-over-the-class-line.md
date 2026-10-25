# The run coordinator is over the class-size line

- Status: Todo
- Priority: Medium
- Category: Structure / maintainability
- Source: the design audit (`docs/design/audit/README.md`, row 5). `done/architecture-split-pass.md` states "no coordinator is
  close to the 600-line gate"; the tree no longer agrees.

## Problem

`src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs` measures 922 lines, while the delivery checklist's structure
rule requires "touched classes <= 600 lines". Either the coordinator is exempt from that rule by a decision this ticket has
not found, or the split the earlier ticket describes has been undone by growth. The audit records the disagreement and does
not decide it.

## Design

Read `docs/design/audit/README.md` (row 5) for the finding, and `docs/evidence/delivery-checklist.md` for the structure
rule it is judged against ("touched classes <= 600 lines"). This ticket is structure rather than player-facing behaviour,
so no mechanic page applies; the audit row is the design record it came from.

## What the fix decides

1. Whether the 600-line rule applies to this class (if an exemption exists, it is named and the checklist says so).
2. If it applies: split along the seams the class already has, with the same behaviour and the same log lines, and record the
   measured sizes before and after.
3. If it does not: correct the delivery checklist's rule or the earlier ticket's claim, whichever is the stale record.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The class is under the line, or the exemption is recorded in the rule it exempts from |
| 2 | The split changes no behaviour: the existing suites pass unchanged and the log lines the run depends on are identical |
| 3 | The earlier ticket's claim and this ticket's outcome agree, so a reader cannot find two answers |
