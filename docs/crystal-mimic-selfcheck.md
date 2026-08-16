# CrystalMimic Sync — Delivery Self-Check

The 2026-08-16 delivery for the CrystalMimic → CrystalEnemy native game
mechanism. Plan approved by the user before deployment.

## Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | CrystalMimic `Touched` / `Hit` flip the private `activated` latch, play `observerlaugh` and create 1-2 `crystalenemy` via `Utils.Create` | `reversing/Assembly-CSharp/Assembly-CSharp/CrystalMimic.cs:23-49` |
| 2 | The public `CrystalBehaviour` dispatchers `OnCollisionEnter2D` / `BuildingHit` invoke every effect | `reversing/Assembly-CSharp/Assembly-CSharp/CrystalBehaviour.cs:74-88` |
| 3 | `crystalenemy` carries `BuildingEntity.animal`; its Start reports through the generic `EntitySpawned` channel; the host marks post-baseline ids as runtime spawns and the guest freezes/binds the copies | `GameAdapter.cs:174-183`, `EntitySpawnSync.cs:69-97`, `EnemySyncCoordinator.cs:96-132` |
| 4 | One-shot trap consumptions ride `TrapConsumptionRegistry` → `TrapStateSnapshot` → late-joiner replay | `EntityEventSync.cs:47-97`, `TrapConsumptionRegistry.cs` |
| 5 | Late joiners materialize runtime enemies from `EnemySnapshot.RuntimeSpawns` | `EnemySyncCoordinator.RuntimeSpawns.cs:23-112` |
| 6 | Host-triggered one-shot events were never recorded (only the remote-report path calls `ReportTrapConsumed`) | `EntityEventSync.cs:47-62` vs `:77-83` |
| 7 | `EntityEvent` / `EntitySpawned` were relayed twice (handler-level broadcast + adapter domain broadcast) | `EntityEventHandler.cs:23-26`, `EntityEventSync.cs:84`; `EntitySpawnedHandler.cs:23-26`, `EntitySpawnSync.cs:154` |

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Mimic trigger detection | Patch `CrystalBehaviour.OnCollisionEnter2D` + `BuildingHit`; report the mimic `activated` false→true edge | `CrystalMimic.cs:23-49`; `TrapCrystalPatch` |
| Protocol | `EntityEventKind.CrystalMimicTriggered = 30`; `ProtocolVersion` 13→14 | `EntityEventKind.cs`; `ProtocolVersion.cs` |
| One-shot classification | Add to `EntityEventProfiles.OneShotConsumptions` + `EntityEventArchives` | Runtime profile + test archive |
| Host apply / guest replay | `TrapStateActions.ApplyCrystalMimic` writes `activated`; live replay plays the original `observerlaugh` call; late-joiner replay is state-only (silent) | `CrystalMimic.cs:29/43`; `TrapEffectApplier`; `TrapVisualReplay` |
| Trap layout | `TrapEntityScan.CrystalKinds` maps `CrystalMimic` → the new kind | `TrapEntityScan.cs` |
| Runtime enemy chain | Unchanged — `EntitySpawned` + `EnemySyncCoordinator` runtime binding + `EnemySnapshot.RuntimeSpawns` | `EntitySpawnSync.cs`, `EnemySyncCoordinator*` |
| Host-triggered one-shot record | `EntityEventChannel.SendEntityEvent` host branch records one-shot consumptions before broadcasting | `EntityEventChannel.cs` |
| Duplicate relay removal | `EntityEventHandler` / `EntitySpawnedHandler` no longer broadcast; the adapter domain is the single relay owner | `EntityEventHandler.cs`, `EntitySpawnedHandler.cs` |
| Contract coverage | `GameFieldContractTests` rows for `CrystalBehaviour.effects` and `CrystalMimic.activated`; `PatchContractTests` asserts the two `CrystalBehaviour` patches; `PatchInventory` lists all 7 dynamic contracts | `GameFieldContractTests.cs`, `PatchContractTests.cs`, `PatchInventory.cs` |
| Audit tables | `event-replay-matrix.csv` new row; `entity-features` matrix + narrative update; `enemy-sync.md` runtime-spawn note; `backlog.md` resolved | docs |

## Verification design (no manual acceptance)

- L0 simulation: the combinatorial entity-event suite automatically runs the
  new kind (report/relay/one-shot race/snapshot/reset).
- New simulations: host-triggered one-shot reaches the late-joiner snapshot;
  the mimic event and the `crystalenemy` spawn report ride their separate
  channels; `EntityEvent` and `EntitySpawned` each produce exactly one relay.
- Contract tests: the new `CrystalBehaviour` patches resolve with exact
  signatures; the traverse-accessed game fields have their exact types.
- Gates: `dotnet build`, `dotnet format`, `check-architecture`,
  `check-event-replay`, `check-entity-event-dispatch`.
- Deployment: `tools/deploy.ps1` with the real machine game directory only.
- Runtime verification is recorded as L0 simulation + static evidence, per
  the development-period zero-manual-acceptance rule.
