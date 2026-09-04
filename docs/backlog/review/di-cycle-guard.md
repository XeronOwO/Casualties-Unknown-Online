# DI cycle guard / cycle-path diagnostics

- Status: Review
- Type: Framework hardening
- Category: Composition root / DI observability
- Source: 2026-09-04 ModService ↔ GameAdapter startup hang. The cycle surfaced only after deploying the current source; the game hung with no useful startup error because the recursion happened while resolving the production composition root.

## Goal

Prevent accidental DI circular dependencies in the CUO composition root, or at minimum make a cycle fail loudly with a log line that names the exact service chain.

## Background

The current production composition includes a real cycle shape:

- `ModService` depends on `IModEntitySpawner`, `IModItemSpawner`, `IModTilePlacer`, `IModStructurePlacer`, `IModLiquidPlacer`, `IModNativeApiProvider`.
- The production registrations route those interfaces to `GameAdapter`.
- `GameAdapter` previously also depended on `ModService`, so DI recursively re-entered while resolving `ModService`.

The immediate fix was to inject `ModStatusStore` into the adapter instead of `ModService`. That removes this specific cycle, but the composition root has no automated guard against another one being introduced later.

## Requirements

1. Detect circular dependency at composition-root build time where statically detectable.
2. For factory-based or runtime `GetRequiredService` cycles that cannot be resolved statically, detect re-entrant resolution at startup and log an actionable error:
   - the service being created
   - the full chain of services/factories on the current resolution stack
   - the two endpoints that form the cycle
3. The check must run before normal startup proceeds, so a regression fails fast instead of hanging the game in a not-responding state.
4. The diagnostic must be logged to both BepInEx `LogOutput.log` and CUO `latest.log` through the normal logging path.

## Candidate approaches

- **Static graph walk after registrations**: traverse singleton constructor parameter types plus factory bodies (when the factory is simple `GetRequiredService<T>`), detect strongly connected components, and throw/log before `BuildServiceProvider`.
- **Runtime re-entrancy guard**: wrap the provider (or a factory resolver hook) with a resolution-stack recorder. On a nested request for a service already on the stack, stop and emit the stack instead of recursing until the process hangs.
- **`ServiceProviderOptions.ValidateOnBuild` / custom `IServiceProviderFactory`**: evaluate whether the MS DI validate-on-build path already detects this shape; if not, add a custom validator.
- **Composition-root smoke test**: resolve the full production graph in a test/CI process with a timeout, asserting no recursive resolution; useful as a regression net even if not a runtime diagnostic.

## Acceptance criteria

- A deliberately introduced `A → B → A` cycle in a test composition raises/logs `A -> B -> A` and does not hang.
- The production startup continues to succeed with the current acyclic graph.
- The check adds no meaningful startup cost (< 50 ms) and no behavior change for valid graphs.
- Existing tests + gates stay green.

## Implementation

- `CuoBootstrap.BuildServiceProvider` now builds the container with
  `ServiceProviderOptions.ValidateOnBuild = true`; constructor/implementation-type
  cycles fail at composition-root build time with the MS DI service chain instead
  of hanging later.
- Added `DiCycleGuard` (Runtime/Diagnostics) and wired it into
  `CuoBootstrap.BuildServiceProvider`. It wraps every factory descriptor so
  factory-mediated re-entrant resolution is recorded; a service already on the
  resolution chain throws immediately with the full service path.
- Startup diagnostics are written through both the BepInEx log source
  (`LogOutput.log`) and the rolling file provider (`latest.log`) when a
  composition-root build fails or a factory cycle is detected.
- Regression coverage: `DiCycleGuardTests` covers constructor-cycle validation,
  factory self-cycle, factory-to-constructor cycle, a valid factory chain, and
  descriptor-order preservation.
- Selfcheck: `docs/evidence/selfchecks/architecture/di-cycle-guard-selfcheck.md`.
- Verification: `dotnet build` 0 warnings/errors, `dotnet test` 2186 passed,
  `dotnet format`, architecture/event-replay/entity-event-dispatch/delivery
  gates all pass.
