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

- [x] Inventory every remaining legacy surface:
  - `Legacy`/`Compat` names;
  - old service facades;
  - wire-forwarding methods in domain services;
  - projection authority corrections;
  - old save DTOs and old message enum values;
  - shadow double-write code;
  - session reset paths that bypass `RunEpoch`/kernel restore.
- [x] Confirm remaining service facades are active single-path controls; no
      kernel-replaced old facade remains in the current production graph.
- [x] Confirm domain Service wire-forwarding is the active protocol adapter layer
      for non-kernel domains; no legacy forwarding duplicates kernel facts.
- [x] Confirm no projection-side authority corrections remain; the kernel is the
      only writer and item projection ownership is guard-enforced.
- [x] Confirm old save DTOs and old removed message enums are gone; the only
      remaining legacy frame is the guarded `ItemReject` exception.
- [x] Unify session reset:
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
- [x] Verify the codebase cannot silently reintroduce dual architecture:
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
| 2026-08-30 | Phase E seventh batch: session reset audit | current | Docs | Audit found a single `ItemKernelAuthority.ResetForSession()` kernel reset path plus projection/transient clears on `SessionEnded`; no bypass or reset-coordinator refactor required. Recorded in `docs/selfchecks/phase-e-legacy-inventory-selfcheck.md`. |
| 2026-08-30 | Phase E eighth batch: narrow reset surface + architecture doc note | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | Made `ItemService.ResetSessionState`/`WorldService.ResetSessionState` private and updated `docs/architecture.md` to identify the evolution target as active during Phase E. |
| 2026-08-30 | Phase E ninth batch: add command-authority guard | current | Architecture gate passed | Added `tools/check-command-authority.ps1` and wired it into `check-architecture.ps1`; every `GameCommand` subclass must now carry an authority policy. |
| 2026-08-30 | Phase E tenth batch: add kernel-shape guard | current | Architecture gate passed | Added `tools/check-kernel-shape.ps1`; GameState kernel rejects string-keyed dictionaries and `Hashtable` state. |
| 2026-08-30 | Phase E eleventh batch: remove ItemService kernel facade | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | `CraftSyncService` now depends on `ItemKernelAuthority` directly; `ItemService.KernelAuthority` passthrough removed. |
| 2026-08-30 | Phase E sixteenth batch: confirm remaining facades/old surfaces are active single-path | current | Docs | The remaining `IItemControl`/`IWorldControl` style surfaces and direct frames were confirmed as active single-path controls/protocol for non-kernel domains, not dual architecture. |
| 2026-08-30 | Phase E twelfth batch: centralize kernel reset in kernel protocol lifecycle | current | 1792 tests green; build/format/architecture/event/entity/delivery gates pass | `KernelProtocolService.ResetForSessionEnd()` now calls `ItemKernelAuthority.ResetForSession()` first; `ItemService` no longer owns the kernel reset. |
| 2026-08-30 | Phase E thirteenth batch: tick completed work-breakdown items | current | Docs | Marked legacy inventory, unified session reset, and no-reintroduction guard as complete in the Phase E work breakdown. |
| 2026-08-30 | Phase E fourteenth batch: guard ItemReject and record decisions | current | Architecture gate passed | `NetMsg.ItemReject` is now a guarded exception limited to two files; tech-decisions.md #158 records the reset centralization and Phase E guard decisions. |
| 2026-08-30 | Phase E fifteenth batch: classify remaining direct NetMsg families | current | Docs | Recorded a full direct-NetMsg classification table; all remaining frames are active single-path session/control, presentation, request, or non-kernel-domain protocols, not kernel-dual authority. |

## Next actions

1. Read Phase D completion evidence in `status.md` and the Phase D selfcheck; include the ad-hoc prediction/rollback caches from tech-decisions.md #157 in the legacy inventory.
2. [x] Create the legacy inventory from `src/` search results.
3. Delete in small batches with tests; do not leave an in-between dual architecture state longer than necessary.
