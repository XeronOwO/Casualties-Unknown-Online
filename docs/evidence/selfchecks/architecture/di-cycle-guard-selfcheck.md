# DI cycle guard — self-check

Backlog item `di-cycle-guard.md`: after the ModService ↔ GameAdapter startup hang,
the composition root had no automated guard against a future DI cycle. This cycle
adds a fast-failing composition-root guard with actionable cycle chains.

## 1. Mechanism inventory

| Mechanism | Prior state |
|---|---|
| Constructor/implementation-type DI cycles | Microsoft DI `ValidateOnBuild` can detect them at `BuildServiceProvider` time, but CUO built with the default options (`ValidateOnBuild = false`), so a regression would only fail when the service was resolved. |
| Factory-based cycles | A factory can call `GetRequiredService` back into a service already being constructed; static validation does not inspect factory bodies and the provider can recurse until the process hangs. |
| Startup diagnostics | The previous ModService cycle produced no useful startup error because the recursion happened during production resolution. |

## 2. Change

- `CuoBootstrap.BuildServiceProvider` now uses
  `ServiceProviderOptions { ValidateOnBuild = true }`, so constructor and
  implementation-type cycles fail at composition-root build with the MS DI
  service chain.
- Added `DiCycleGuard` in `CasualtiesUnknownOnline.Runtime.Diagnostics`:
  - wraps every factory descriptor before `BuildServiceProvider`;
  - passes each factory a guarded `IServiceProvider` that records the current
    resolution chain;
  - factory services record themselves when their factory starts; non-factory
    services record when requested from a guarded provider, so the diagnostic
    chain includes mixed factory/constructor paths;
  - a re-entrant request throws `InvalidOperationException` with the full chain
    (e.g. `A -> B -> A`) instead of letting the provider recurse.
- Startup failure diagnostics are written through both the BepInEx log source
  (`LogOutput.log`) and the rolling file provider (`latest.log`) via
  `LogCompositionRootFailure`, both for build-time validation failures and for
  factory-cycle detections that happen during subsequent startup resolution.

## 3. Verification

| Evidence | Result |
|---|---|
| `DiCycleGuardTests.FieldBackedConstructorCycle_ValidateOnBuildThrowsWithChain` | Passed |
| `DiCycleGuardTests.FactorySelfCycle_ResolveThrowsInsteadOfHanging` | Passed and returns immediately, no hang |
| `DiCycleGuardTests.FactoryToConstructorCycle_ResolveThrowsWithFullChain` | Passed; chain includes both factory and constructor service |
| `DiCycleGuardTests.FactoryCycle_InvokesDiagnosticCallback` | Passed; the startup diagnostic callback receives the cycle exception |
| `DiCycleGuardTests.ValidFactoryChain_StillResolves` | Passed |
| `DiCycleGuardTests.WrapFactoryDescriptors_PreservesDescriptorOrder` | Passed |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2186 passed |
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet format`, architecture/event-replay/entity-event-dispatch/delivery gates | Passed |

## 4. Remaining scope

The guard is a fast-failing runtime/composition-root safety net, not a static
factory IL analyzer. Factory cycles that are never resolved at startup will not
be reported until that service is actually resolved; this is intentional because
forcing every factory to resolve during build would instantiate services with
side effects. The production graph remains acyclic in the current test composition.
