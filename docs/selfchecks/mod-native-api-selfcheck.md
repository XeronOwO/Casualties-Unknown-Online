# Mod AccessNativeApi — mechanism inventory and self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "AccessNativeApi".
Decision: implement the last declared permission as a **Game Adapter-curated
native operation registry**, not arbitrary reflection. The first slice exposes
one read-only local-player state operation (`local.player.state`) through
`IModNativeApi`; write/native-mutation operations are deliberately withheld
until a concrete consumer exists and its sync/authority boundary is designed.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Public API | `IModContext.NativeApi` + `IModNativeApi` in `CUO.Abstractions` — the only assembly mods may reference. |
| 2 | Permission | `ModPermission.AccessNativeApi` gets its first live enforcement point in `ModService.NativeApi`; every invoke checks and logs it. |
| 3 | Registry policy | Not reflection: only named operations registered by the Game Adapter are invokable (`IModNativeApiProvider.IsRegistered`). |
| 4 | Value policy | `ModNativeApiPolicy`: operation-id rails, argument count/type rails, result type rails. Unity/game objects are refused before and after the seam. |
| 5 | Registered operation | `ModNativeApiOperations.LocalPlayerState` = `"local.player.state"` returns `IModNativeLocalPlayerState` (position, brain health, hunger, thirst, stamina, energy, temperature, consciousness, alive/conscious). |
| 6 | Boundary | `IModNativeApiProvider` is the Runtime → Game Adapter seam; disabled/no-op in the Runtime-only composition, replaced by the plugin with `GameAdapter`. |
| 7 | Wire | No new NetMsg, no direction-table row, no ProtocolVersion change (local read-only state only). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContext` | Added `NativeApi` property (new public API surface). |
| `IModNativeApi` / `IModNativeLocalPlayerState` / `ModNativeApiOperations` | New binding contract + operation id in `CUO.Abstractions`. |
| `IModNativeApiProvider` | New Runtime boundary contract (Game Adapter implements). |
| `ModNativeApiPolicy` | New pure policy rails (operation id, safe argument/result value surface). |
| `ModService` | New `ModService.NativeApi.cs` partial: permission/policy gate + `ModNativeApiAdapter`. |
| `DisabledModNativeApiProvider` | Default no-op registration keeping the Runtime-only/test graph constructible. |
| `CuoBootstrap` | Registered the disabled default; the plugin replaces it with the real adapter. |
| `GameAdapter` | New `GameAdapter.NativeApi.cs` partial reading local `Body` and returning the framework DTO; no Unity object crosses. |
| `Plugin` | Registered the real `IModNativeApiProvider` from `GameAdapterImpl`. |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Public API | `IModContext.NativeApi` / `IModNativeApi` | Contract + tests below; `docs/mod-api.md` §4i. |
| Permission enforcement | `CanAccess` and every invoke require `ModPermission.AccessNativeApi` | `ModNativeApiTests.MissingAccessNativeApiPermission_IsRefused`. |
| Registry policy | Unknown operation is refused/not available | `ModNativeApiTests.UnknownOperation_IsRefused`. |
| Malformed operation / unsafe arguments | Refused before the provider seam | `ModNativeApiTests.MalformedOperationOrUnsafeArguments_IsRefusedBeforeProvider`, `ArgumentCountCap_IsRefusedBeforeProvider`. |
| Unsafe provider result | Refused after the seam, never returned to a mod | `ModNativeApiTests.UnsafeProviderResult_IsRefusedAfterSeam`. |
| Typed local player state | Forward + DTO projection works | `ModNativeApiTests.WithPermission_ForwardsToProviderAndReturnsSafeResult`. |
| Value surface rails | Exact safe/unsafe classification | `ModNativeApiTests.PolicyRails_AreExact`. |
| Game Adapter shape | Implements the Runtime seam and owns the DTO | `GameAdapterNativeApiContractTests` (3 reflective rows). |
| No wire/protocol regression | No new NetMsg, ProtocolVersion stays 32 | `docs/mod-api.md` §7; full suite green. |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real `ModService` / `TestNode` stack: permission
  refusal, delegation, unknown operation, malformed/unsafe arguments, unsafe
  result, argument cap, policy rails.
- Static evidence: no new NetMsg; the surface only reads local game state; the
  mod-facing API stays in `CUO.Abstractions`; the Game Adapter contract is
  locked reflectively.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-23)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1170 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| `check-delivery.ps1` | pass (checked boxes tracked in `../delivery-checklist.md`) |
| No manual acceptance | per development-period rule |
