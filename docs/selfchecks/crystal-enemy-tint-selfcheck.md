# CrystalEnemy Presentation Tint Sync — Self-Check

## Mechanism

A runtime-created `crystalenemy` (the CrystalMimic spawn) gets its presentation
tint from `CrystalEnemy.SetColor` (CrystalEnemy.cs:208-216), called by the mimic
on the TRIGGERING side only (CrystalMimic.cs:32/46):

- `SetColor` writes `sprite.color` and `light.color` with the mimic's passed
  color plus a per-channel 0.9–1 jitter from `Random.Range` (CrystalEnemy.cs:210-212).
- It also writes `light.intensity = Random.Range(0.5f, 1f)` (CrystalEnemy.cs:215).
- The call is synchronous inside the touch/hit callback; the spawned
  `BuildingEntity.Start` runs one frame later, so by the time the spawn report
  fires the copy already carries its final tint.

The jitter is per-side random. Re-running `SetColor` on a receiver would produce
a DIFFERENT color, so the receiver must be written the exact host-captured
post-SetColor values, never a re-roll.

## Change

ProtocolVersion 24. Creation data now carries the crystalenemy tint on BOTH
runtime-spawn paths:

- **Live spawn** (`EntitySpawnedMsg`): `EntitySpawnSync.OnEntityInstantiated`
  captures the creating side's `CrystalEnemy` post-SetColor `sprite.color` +
  `light.intensity` (`CrystalEnemyTintAccess.TryRead`) and reports them as
  `HasEnemyTint` / `EnemyTintColor` / `EnemyLightIntensity`. Every receiver
  (`EntitySpawnSync.OnRemoteEntitySpawned` → `ApplyEnemyTint`) writes the exact
  values onto its created copy (`CrystalEnemyTintAccess.ApplyTint`). The host's
  keypad-code relay path preserves the tint fields when it rebuilds the message.
- **Late-joiner backfill** (`EnemySpawnEntryMsg`): the host's `EnemyEntity`
  carries the same tint for runtime-spawned crystalenemies, and
  `EnemySnapshot.RuntimeSpawns` entries include it. `EnemySyncCoordinator`
  applies the tint both when binding an existing local copy and when
  materializing a fresh one (`ApplySpawnTint`).

`NetColorRgba` / `NetColorRgbaMsg` are the engine-agnostic color wire pair: the
Runtime never references UnityEngine, the Game Adapter converts at the boundary.

## Verification evidence

| Mechanism | Change | Evidence |
|---|---|---|
| Tint captured at the true post-SetColor moment | `CrystalEnemyTintAccess.TryRead` on the host/creating side | `GameFieldContractTests` locks `CrystalEnemy.sprite` + `CrystalEnemy.light` (untyped fields read by the accessor) |
| Receiver writes exact color, never re-rolls | `ApplyEnemyTint` / `ApplySpawnTint` call `ApplyTint` directly | code path: `EntitySpawnSync.ApplyEnemyTint`, `EnemySyncCoordinator.ApplySpawnTint`; `GameFieldContractTests` covers the reflected fields |
| Live wire carries the tint | `EntitySpawnedMsg` new fields | `NetPacketTests.EntitySpawned_CrystalEnemyTint_RoundTrips` |
| Late-joiner wire carries the tint | `EnemySpawnEntryMsg` inside `EnemySnapshotMsg` | `NetPacketTests.EnemySpawnEntry_CrystalEnemyTint_RoundTrips`; `EnemyStateRoundtripTests.EnemySpawnEntry_Roundtrip_FromEntity` |
| Domain → wire mapping preserves tint | `EnemyEntity.ToEnemySpawnEntryMsg` | `EnemyStateRoundtripTests.EnemySpawnEntry_Roundtrip_FromEntity` |
| Relay preserves tint on keypad rebuild | `EntitySpawnSync.OnRemoteEntitySpawned` copies `HasEnemyTint`/`EnemyTintColor`/`EnemyLightIntensity` | static evidence in the relay construction |
| Full-chains green | 995 tests | `dotnet test` |
| Repo gates | format + architecture + event-replay + entity-features | all pass |

**L0 wire roundtrips + reflective field contracts + static evidence, no manual
acceptance** (development-period no-manual-acceptance rule).
