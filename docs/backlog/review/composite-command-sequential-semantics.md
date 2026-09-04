# Composite command sequential semantics

- Status: Review
- Priority: Medium
- Category: Kernel / transactions
- Source: Loomi architecture review (2026-09-04)

Implemented Option B: `GameStateKernel.ExecuteComposite` now decides and reduces each
inner command in declaration order on the same working copy, so a later command can see
an earlier command's staged result. Any rejection discards the working copy and the whole
composite is atomic. Only the composite's `OperationId` is an idempotency key; inner
`OperationId`s are not independently recorded.

Covered by `CompositeCommandKernelTests`: staged spawn-then-update dependency, rollback on
later rejection, duplicate composite `OperationId` idempotency, plus existing atomic commit
and guest replay. Docs updated: `docs/architecture/current.md`, `docs/architecture/domains.md`.
Selfcheck: `docs/evidence/selfchecks/architecture/composite-command-sequential-semantics-selfcheck.md`.
