# Phase E — Delete the Dual Architecture

> Status: **Not started** (Phase D completed; generic Prediction Runtime is a Phase E residue item per tech-decisions.md #157)
> Source: target architecture §15-§18; migration roadmap "Phase E".

## Objective

Finish the iteration by removing every remaining legacy/compat/shadow/double-write
surface and locking the new architecture with guards. The final state has one
authoritative kernel, typed domains, and projections. There is no permanent `Legacy`,
`Compat`, shadow double-write, or second authority table.

## Scope

In scope:

- Delete legacy service facades such as large `IItemControl`-style send/fire/event interfaces.
- Delete domain Service wire-forwarding layers that are no longer needed; forwarding is
  done by protocol adapters/projections.
- Delete authority-correction logic hidden inside projection layers.
- Delete old save DTOs and old message enums that are no longer used.
- Unify all session reset paths into `RunEpoch` / kernel restore.
- Remove all temporary compatibility shims from Phases A-D.
- Add architecture tests that prevent reverse dependencies and legacy reintroduction.
- Final structure review: every touched class meets size/responsibility/state rules,
  dead mechanisms are removed in the same round.

Out of scope:

- New gameplay features.
- General code cleanup unrelated to removing the dual architecture.
- Keeping any compatibility layer for old protocol/save.

## Prerequisites

- Phase D exit criteria met: all domains live in the kernel.
- Production protocol and save use the Phase C formats.
- No active feature depends on an old facade.

## Work breakdown

- [ ] Inventory every remaining legacy surface:
  - `Legacy`/`Compat` names;
  - old service facades;
  - wire-forwarding methods in domain services;
  - projection authority corrections;
  - old save DTOs and old message enum values;
  - shadow double-write code;
  - session reset paths that bypass `RunEpoch`/kernel restore.
- [ ] Delete old service facades after confirming their consumers use the deep module.
- [ ] Delete domain Service wire-forwarding; keep protocol adapters as the only translation layer.
- [ ] Remove projection-side authority corrections; projections rebuild from kernel
      state/checkpoint instead.
- [ ] Delete old save DTOs and old message enums, including their serializer paths.
- [ ] Unify session reset:
  - every leave/disconnect/scene/run transition goes through kernel restore or RunEpoch;
  - no leftover per-domain reset caches.
- [ ] Add/strengthen architecture tests:
  - `GameState` isolation;
  - domain isolation;
  - no wire DTOs in domain public interfaces;
  - no Unity types in kernel data;
  - event reducer/serialization registration;
  - every Command declares authority policy;
  - checkpoint completeness;
  - invariant suites for key aggregates;
  - no string event names / `Dictionary<string, object>` core state;
  - no legacy/double-write code without a deletion milestone (after Phase E: fail).
- [ ] Run a full structural review:
  - 600-line gate;
  - state bool gate;
  - one top-level type per file;
  - no dead mechanisms left behind.
- [ ] Update documentation:
  - `README.md` root project overview;
  - `docs/architecture.md` to reflect the new architecture (or mark the old one superseded and link to the evolution area);
  - `docs/tech-decisions.md` with the final removal decisions;
  - `docs/backlog.md` to remove completed architecture items;
  - `docs/architecture-evolution/status.md` to mark Phase E complete;
  - `docs/architecture-evolution/README.md` to reflect completion.
- [ ] Verify the codebase cannot silently reintroduce dual architecture:
  - add a search/test for disallowed patterns;
  - make the guard part of CI.

## Exit criteria

- No `Legacy`, `Compat`, shadow double-write, or two authoritative tables remain in production code.
- All old save DTOs and old protocol message enums are deleted from production paths.
- Session reset is unified through `RunEpoch`/kernel restore.
- Projection code never corrects authority; it only rebuilds from kernel state/checkpoint.
- Architecture guard tests pass and reject reintroduction.
- Full build/test/format/gates pass.
- Historical user-observable replay semantics remain equivalent.
- The repository's main architecture documentation describes the new architecture as the
  current design, not a plan.

## Verification design

- Static grep/architecture test for legacy names and double-write patterns.
- Full build/test/format/gates.
- `dotnet test` plus replay/simulation and projection rebuild tests.
- Review every deleted facade's former consumers to ensure no dangling dependency.
- L0 + static evidence, no manual acceptance.

## Deliverables

- Deleted legacy facades, wire-forwarding, old DTOs, old enum values.
- Unified RunEpoch/kernel restore.
- Final architecture guard suite.
- Updated main docs.
- Final phase self-check fact sheet.

## Open questions / risks

| Risk | Mitigation |
|---|---|
| Deleting old facades can break obscure consumers | Grep consumers before deletion; compile/test with full solution. |
| Projection corrections may have been load-bearing | Rebuild projections first and prove equivalence before deleting corrections. |
| Hidden legacy may remain under other names | Add the architecture guard search to CI before declaring complete. |
| Docs may still describe old architecture | Update `docs/architecture.md` and root README in this phase, not later. |

## Session handoff

- Mark Phase E complete only after the final guard suite is green and no prohibited
  legacy pattern remains.
- Update `status.md` with "Architecture evolution complete".
- Future feature work should treat the new architecture as the only supported design.

## Session log

| Date | Scope | Commits | Verification | Notes |
|---|---|---|---|---|
| 2026-08-30 | Phase E start: legacy inventory + remove dead `ItemCheckpointStore` | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | Inventory recorded in `docs/selfchecks/phase-e-legacy-inventory-selfcheck.md`; removed the Phase B temporary in-memory checkpoint store and its DI registration/tests. |
| 2026-08-30 | Phase E second batch: remove `Shadow` naming from production | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | Renamed `ItemService.KernelShadow` to `KernelAuthority`, updated CraftSyncService/tests, renamed the kernel convenience test class, and removed the last `Shadow` token from `src/`. |
| 2026-08-30 | Phase E third batch: add no-legacy architecture guard | current | Architecture gate passed with the new `check-no-legacy.ps1` scan | Created `tools/check-no-legacy.ps1`, wired it into `tools/check-architecture.ps1`, and documented the Phase E addendum in `docs/architecture-evolution/architecture-guards.md`. |
| 2026-08-30 | Phase E fourth batch: move shadow-differential helper out of GameState | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | `ItemDiagnosticsProjection` moved from the GameState kernel project to the test project as a test-only comparison helper; no production kernel code depends on it. |
| 2026-08-30 | Phase E fifth batch: remove test-only kernel accessor | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | `ItemKernelAuthority.KernelForDiagnostics` removed; tests now use the public `FindItem`/`QueryItems` surface. |
| 2026-08-30 | Phase E sixth batch: extend no-legacy guard markers | current | Architecture gate passed | Added `KernelShadow`, `KernelForDiagnostics`, and `ItemDiagnosticsProjection` to the prohibited production-source markers. |

## Next actions

1. Read Phase D completion evidence in `status.md` and the Phase D selfcheck; include the ad-hoc prediction/rollback caches from tech-decisions.md #157 in the legacy inventory.
2. [x] Create the legacy inventory from `src/` search results.
3. Delete in small batches with tests; do not leave an in-between dual architecture state longer than necessary.
