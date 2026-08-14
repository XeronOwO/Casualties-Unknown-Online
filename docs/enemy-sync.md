# NPC / Enemy Synchronization — Design

Status: **proposal** (awaiting review before implementation).

## 1. Current state & problem

### Enemy mechanics (decompiled evidence)

An "enemy" is a `BuildingEntity` with `animal == true` (BuildingEntity.cs:202) — that carries
health, drops and the death path (BuildingEntity.cs:56-122) — plus one of several heterogeneous AI
scripts. There is **no shared enemy base class**; the only shared anchor is the `animal` flag:

- `SpiderHandler` (SpiderHandler.cs) / `SpiderHandlerTBE : SpiderHandler` — random wander/aggro in
  `Update` (SpiderHandler.cs:32-105, `Random.value` / `Random.insideUnitCircle`), physics movement in
  `FixedUpdate` (SpiderHandler.cs:114-133, `rb.AddForce` / `AddTorque`), bite/burrow on collision.
- `CrystalEnemy` (CrystalEnemy.cs) — track/wind-up/lunge state machine in `Update`
  (CrystalEnemy.cs:44-112) + physics chase in `FixedUpdate` (CrystalEnemy.cs:169-191).
- Others: `DroneScript`, `GrabberPlant`, `ElderThornbackBehaviour`, `ScrapEaterScript`, `Vomiter`,
  `XalorisScript`, `CaveTicks` — each a `MonoBehaviour` with its own `Update`/`FixedUpdate`/`Rigidbody2D`.

**Non-deterministic**: movement is random numbers + Unity physics, so two sides simulating
independently inevitably diverge (and can damage different players independently).

**Spawn**: deterministic `WorldGeneration.DistributeEntities` (WorldGeneration.cs:1347) — enemies
like `caveticks`/`shadecrawler`/`wallbiter`/`thornbackyoung`/`overgrowntick`/`grabberplant` are
distributed with `isTrap=true` — plus a runtime spawner (`CaveTickSpawner.cs:41-52` spawns 16
`cavetick` on trigger).

### What CUO already covers

- Enemy health changes are synced (`BodyAttackPatch` → `OnBuildingEntityDamaged` →
  `OnRemoteBuildingEntityDamaged` applies `entity.health -= damage`; `RemoteEntityDeath` suppresses
  the remote drop roll).
- **Not synced**: enemy position / rotation / animation / AI-visible state. Each side simulates its
  own copy, so enemies visibly diverge.

## 2. Goal

Both sides see the same enemy position / rotation / animation / alive-state, with a late-joiner full
snapshot and consistent death/spawn.

## 3. Architecture (host-authoritative + snapshot, reusing the player-sync pattern)

### 3.1 Recognition & identity

- Recognize enemies uniformly via `BuildingEntity.animal == true` (not by enumerating AI scripts).
- Identity: reuse `NetworkEntityId`. The host assigns an id to each animal entity in a deterministic
  order (e.g. sort by generated position); the guest matches its locally generated copy by the same
  order (position-keyed, like the world-mutation table).

### 3.2 Host-authoritative simulation

- Host: enemies simulate normally (AI + physics) — authoritative.
- Guest: enemies are **frozen** — a `RemoteEnemyDriver` marker + Harmony patches on each enemy AI
  script's `Update`/`FixedUpdate` (Prefix checks the marker and skips), Rigidbody2D static /
  non-simulated. Position/rotation/animation are driven from the snapshot (the same pattern as the
  player `RemoteBodyDriver`).

### 3.3 Snapshot sync (20 Hz, reusing the entity stream)

- New `EnemyStateMsg` (or a generalized entity state): position, velocity, rotation, health + a packed
  presentation-flag byte (stuck / stun / wind-up … for animation).
- Reuse `EntitySyncService`'s 20 Hz throttle + unreliable stream + seq dedup.

### 3.4 Late-joiner full snapshot

- Host fans out the full enemy snapshot on member world-entry (same pattern as
  `HandlerContext.SendWorldStateToMember`).

### 3.5 Spawn / death

- Death: extend the existing BuildingEntity health sync to animal entities (`RemoteEntityDeath`
  already suppresses the remote roll).
- Runtime spawn (`CaveTickSpawner`): sync the spawn event (16 caveticks + ids + positions), reusing
  the `OnEntityInstantiated` runtime-creation channel.

## 4. Domain modelization (testable judgment into Runtime)

- `EnemySpawnArbitration` (pure machine): deterministic id assignment/matching for animal entities +
  runtime-spawn id arbitration.
- `EnemyStateCodec` (pure): snapshot roundtrip (position/velocity/rotation/health/presentation flags)
  with a reflection completeness guard over the presentation-flag bits.
- Death/spawn arbitration (pure): first-writer-wins for enemy death, spawn-event id allocation.

## 5. Tests

- Pure logic: `EnemySpawnArbitration` deterministic assignment/matching; `EnemyStateMsg` roundtrip
  (reflection completeness); death/spawn arbitration.
- Simulation: FakeTransport host-simulated enemies + guest-applied snapshots converge to the host
  position.
- Contract: freeze-patch contracts for every enemy AI script (existence + signature + param names) —
  a new enemy script fails the contract until a freeze patch is added.

## 6. Risks (honest)

1. **Heterogeneous script freezing is the biggest surface**: ~9-10 scripts to patch; the contract
   test locks the list (a new enemy script fails the gate until frozen).
2. **Enemy state surface**: only the presentation subset is synced (position/velocity/rotation/health
   + a few animation flags), NOT the full AI internal state (target/stun timers). Attack side-effects
   (bites, drops) ride the existing damage/event paths — no double-sync.
3. **Unity physics determinism**: the guest never simulates enemy physics (frozen + snapshot-driven)
   — same as the player clone, so no determinism risk.
4. **Runtime spawn** (`CaveTickSpawner`) is a new generation-side surface (16 caveticks on trigger).

## 7. Implementation order

1. Domain + protocol (`EnemySpawnArbitration`, `EnemyStateMsg`) + pure-logic tests.
2. Host-side snapshot capture + broadcast (extend `EntitySyncService`).
3. Guest-side freeze (patch enemy scripts) + snapshot-driven rendering.
4. Late-joiner full snapshot + spawn/death events.
5. Simulation + contract tests + gates.
