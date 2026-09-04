# Composite command sequential semantics self-check (2026-09-04)

This fact sheet records the kernel composite transaction semantics change: inner
commands now decide and reduce in declaration order on the same working copy, so
later commands observe earlier staged results, while the whole composite still
commits or rejects atomically.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Composite command | `src/CasualtiesUnknownOnline.GameState/CompositeGameCommand.cs` | Flat list of inner typed domain commands; only the composite `OperationId` is an idempotency key. |
| Composite execution | `src/CasualtiesUnknownOnline.GameState/GameStateKernel.cs` (`ExecuteComposite`) | For each inner command: build read model from current working copy, decide, reduce accepted events immediately. |
| Working copy | `src/CasualtiesUnknownOnline.GameState/Kernel/MutableKernelState.cs` | Transaction-local state; discarded on any inner rejection. |
| Domain modules | `src/CasualtiesUnknownOnline.GameState/Domains/*` | Decide/reduce/invariant ownership unchanged. |

## Evidence table

| Claim | Evidence |
|---|---|
| Later inner command sees earlier staged result | `CompositeCommandKernelTests.Composite_LaterInnerCommandSeesEarlierStagedResult` (spawn then update same item with `ExpectedRevision=1`). |
| Earlier staged result is rolled back when a later inner command rejects | `CompositeCommandKernelTests.Composite_Rollback_WhenLaterInnerCommandRejected`. |
| Duplicate composite `OperationId` is idempotent | `CompositeCommandKernelTests.Composite_DuplicateOperationId_ReturnsOriginalDecision`. |
| Inner `OperationId`s are not independent idempotency keys | `CompositeCommandKernelTests.Composite_InnerOperationIdsAreNotSeparateIdempotencyKeys`. |
| Existing cross-domain atomic commit and guest replay remain correct | `CompositeCommandKernelTests` existing tests; full suite 2170 tests green. |
| Composite semantics documented in architecture docs | `docs/architecture/current.md` §5.4/§6.5, `docs/architecture/domains.md` ownership rule 5. |

## Verification design

This is a kernel-only behavioral change; unit tests exercise the full
Decide -> Reduce -> invariant -> commit path with an in-memory `GameStateKernel`.
No runtime/deployment verification is required for the kernel transaction
semantics itself.
