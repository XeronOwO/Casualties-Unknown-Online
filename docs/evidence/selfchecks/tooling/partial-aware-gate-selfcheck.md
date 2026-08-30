# Partial-aware architecture gate + first real split — self-check (2026-08-23)

Backlog §3.1 called out that `tools/check-architecture.ps1` counted per file,
so partial classes could hide a logical class far above 600 lines. This cycle
makes the gate partial-aware, records the existing debt explicitly, and lands
the first real responsibility split (`WorldBuildingEntitySync`).

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Old gate | Per-file line/bool counts only; `WorldService.MessageFlow.cs`, `ModService.Commands.cs`, etc. could hide large logical types. |
| Gate parsing | The new script reads each `.cs` file, extracts namespace + top-level type, aggregates line count and `private bool _...` fields by full type name across partial files. |
| Debt ledger | `docs/architecture-debt.json` records the current aggregate sizes of every logical type above the 600-line gate. |
| Strict mode | `tools/check-architecture.ps1 -Strict` refuses even recorded debt; used when the flattening is complete. |
| First real split | `WorldEventSync.BuildingEntities.cs` partial replaced by `WorldBuildingEntitySync` (a separate top-level class); `WorldEventSync` delegates the patch entry points and owns the event subscription to the new class. |

## 2. Whole-family audit

- All partial files in `src/` are considered by the aggregate gate, not just the
  known offenders.
- The per-file "one top-level type" rule is unchanged.
- The debt ledger is the single source of approved legacy debt; any unrecorded
  over-limit type or any growth beyond its recorded size is a hard failure.
- `WorldEventSync` was split as a *real* responsibility move, not another
  partial: its building-entity report/apply/snapshot surface is now a separate
  top-level collaborator, and the main class delegates the public patch-entry
  methods.
- No wire/protocol/behavior change from the split.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Gate aggregation | Per-file → per-logical-type line/bool aggregation | `tools/check-architecture.ps1` |
| Debt control | Unrecorded debt / growth beyond ledger fails | script + `docs/architecture-debt.json` |
| Strict mode | `-Strict` refuses all recorded debt | script |
| `WorldEventSync` | 643 aggregate → under 600 (removed from ledger) | `WorldBuildingEntitySync.cs`, gate output |
| `WorldBuildingEntitySync` | New top-level building-entity sync class | file; build |
| No behavior change | Existing world-event/building-entity tests pass | full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | passed (recorded debt warnings only) |
| `tools/check-architecture.ps1 -Strict` | fails on remaining recorded debt (expected until flattening complete) |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + gate checks.
- The gate is itself verified by running it before and after the ledger is
  populated (it must fail on unrecorded debt and pass with recorded debt).
- The `WorldEventSync` split is verified by the existing World/Block/Building
  event L0 tests; no manual dual-side acceptance needed (user rule 2026-08-16).

## 6. Plan approval

The user instructed this session to continue autonomously ("继续") after the
previous cycles, so this cycle's plan is approved without a separate
interactive approval step.

## 7. Structure review

- The gate now sees real logical sizes.
- `WorldEventSync` is smaller; `WorldBuildingEntitySync` is one top-level
  responsibility per file.
- No new expression-state bool fields.
- The remaining 7 large logical classes are explicitly recorded as debt in
  `docs/architecture-debt.json` and tracked in `docs/backlog/README.md`; they must not
  grow, and the next cycles should split them one by one.
