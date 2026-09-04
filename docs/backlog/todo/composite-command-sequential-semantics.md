# Composite command sequential semantics

- Status: Todo
- Priority: Medium
- Category: Kernel / transactions
- Source: Loomi architecture review (2026-09-04)

`CompositeGameCommand` executes all inner `Decide` calls against the same original
read model and only reduces all collected events after the loop. Therefore an inner
command that depends on an earlier inner command's intermediate state cannot see it.

Goal: define and implement one explicit semantics:

- Option A: formally define composite inner commands as independent, add a guard or
  validation, and document the restriction.
- Option B (preferred by the review): execute each inner command's `Decide` -> `Reduce`
  on the same working copy in order, so later commands see earlier staged results,
  then run the final invariant check and commit atomically.

Required tests: rollback on failure, duplicate `OperationId` semantics, a
spawn-then-update-same-item dependency, and rejection atomicity.
