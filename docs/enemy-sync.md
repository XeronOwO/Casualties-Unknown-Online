# NPC / Enemy Synchronization — Design

> Architecture context: enemy terminal facts are kernel-owned; see
> [`architecture-evolution/domains.md`](architecture-evolution/domains.md) and
> [`architecture-evolution/protocol.md`](architecture-evolution/protocol.md).

Status: **landed** — host-authoritative enemy sync, multiplayer targeting, host-ordered attacks and the dedicated enemy-proximity effect events are in; the previously listed remaining enemy-interaction gaps are closed (see §6).

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

**Prefab component mapping (runtime-verified 2026-08-15 via HotRepl `Resources.Load` +
`GetComponents<MonoBehaviour>`)**: `cavetick`, `shadecrawler`, `wallbiter`, `thornbackyoung`,
`overgrowntick` and `snowstrider` all carry `SpiderHandler`; `thornbackelder` carries
`SpiderHandlerTBE` + `ElderThornbackBehaviour`; `crystalenemy` carries `CrystalEnemy`;
`grabberplant` carries `GrabberPlant` + `IKHandle`; `xaloris` carries `XalorisScript` + `Heater`.
Every moving script is therefore already covered by `EnemyPatches` (SpiderHandler.Update/FixedUpdate
are inherited by `SpiderHandlerTBE`; CrystalEnemy has its own patches) — no freeze-list extension
was needed. `LookTarget` runs locally and its gaze/scare is carried in the
player entity stream for remote-clone presentation; `Heater` is an excluded
local-body effect (see §6).

**Non-deterministic**: movement is random numbers + Unity physics, so two sides simulating
independently inevitably diverge (and can damage different players independently).

**Spawn**: deterministic `WorldGeneration.DistributeEntities` (WorldGeneration.cs:1347) — enemies
like `caveticks`/`shadecrawler`/`wallbiter`/`thornbackyoung`/`overgrowntick`/`grabberplant` are
distributed with `isTrap=true` — plus a runtime spawner (`CaveTickSpawner.cs:41-52` spawns 16
`cavetick` on trigger).

### What CUO already covers

- Enemy position / velocity / rotation / health ride the host-authoritative 20 Hz `EnemyState`
  stream, with a full `EnemySnapshot` on world entry; the guest freezes its locally generated copies
  and drives them from the stream (`EnemySyncCoordinator` + `RemoteEnemyDriver` + `EnemyPatches`).
- Enemy health changes are synced (`BodyAttackPatch` → `OnBuildingEntityDamaged` →
  `OnRemoteBuildingEntityDamaged` applies `entity.health -= damage`; `RemoteEntityDeath` suppresses
  the remote drop roll).
- Enemy targeting and attacks on remote players are synced through `EnemyCombatDirector` +
  `EnemyAttack`/`EnemyBite`/`EnemyLunge` (§3.6-3.7).
- Spider presentation is synced on the same stream: `EnemyState.SpiderLegTargets`
  carries the host's `IKHandle.targetPos` values and host-ordered bites replay
  the one-shot `ClawAnim` on both the host view and the victim.

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

### 3.6 Multiplayer targeting (the host-only limitation fix)

The game's enemy AI discovers players through physics queries / `PlayerCamera.main.body`, which
only see the LOCAL body — remote render clones have every collider disabled (`RemoteBodyFactory`,
by design). A host-side `EnemyCombatDirector` resolves the missing targeting without re-enabling
clone colliders:

- `SpiderHandler.Update` recomputes its move target on the `moveTime` expiry edge
  (SpiderHandler.cs:95); the patch replaces the single-player `OverlapCircle` result with the
  nearest in-world player inside `seeDistance` (SpiderHandler.cs:71).
- `CrystalEnemy.body` (the private property the whole AI reads, CrystalEnemy.cs:15) resolves to
  the nearest in-world player body inside the game's own 64-unit `close` radius
  (CrystalEnemy.cs:25).
- The nearest player wins on both sides — host body + every remote position from the 20 Hz entity
  stream. No clone collider is re-enabled, so the known physics pitfalls stay closed.

### 3.7 Host-ordered attacks (remote clones have no colliders)

Because the host's collision callbacks can never touch a remote clone, an enemy that reaches a
remote player gets a one-shot `EnemyAttack` command (host → guest, NetMsg 83). The victim's side
applies the game's own damage path locally and reports the post-attack terminal state:

- Spider bite: host arbitrates inside the 1.5-unit chase-stop radius (SpiderHandler.cs:125) and
  mirrors the post-bite retreat/cooldown (SpiderHandler.cs:146-151); the guest calls its frozen
  copy's `DamageLimb` (virtual — `SpiderHandlerTBE` included) and the existing `EnemyBite`
  report carries the terminal state back.
- Crystal lunge: host arbitrates the player first along the lunge ray before the first ground hit
  (CrystalEnemy.Lunge, CrystalEnemy.cs:133-168); the guest applies the same armor-reduced damage
  constants and reports the terminal state through the new `EnemyLunge` event (NetMsg 84).
- Frozen spider collision callbacks are skipped on the guest (`OnCollisionStay2D` /
  `OnCollisionEnter2D`): the old frozen-copy bite path would race the host command and
  double-apply one attack. One attack = one apply path.

### 3.8 Spawn / death

- Death: extend the existing BuildingEntity health sync to animal entities (`RemoteEntityDeath`
  already suppresses the remote roll).
- Runtime spawn (`CaveTickSpawner` and `CrystalMimic`): the `cavetick` creations and the
  `crystalenemy` spawns already ride the generic `OnEntityInstantiated` → `EntitySpawned` channel
  (prefab id + position + rotation). `CrystalMimicTriggered` (one-shot) consumes the mimic's
  `activated` latch so a peer cannot spawn a second set; the spawns themselves ride this channel.
  The guest
  freezes each runtime animal copy at its Start (`OnAnimalInstantiated`, before AI/physics move it);
  the host marks every id allocated after the initial deterministic mapping as a runtime spawn and
  carries those facts in `EnemySnapshot.RuntimeSpawns` (id + prefab + current position/rotation).
  Live 20 Hz batches bind the unbound runtime ids to the EntitySpawned-created copies by position
  (`EnemyRuntimeSpawnArbitration.TryPairByPosition`, all-or-nothing); a late joiner materializes the
  missing copies from the snapshot facts (`MatchRuntimeSpawns` + `Utils.Create`) and binds them to the
  host ids.

## 4. Domain modelization (testable judgment into Runtime)

- `EnemySpawnArbitration` (pure machine): deterministic id assignment/matching for animal entities.
- `EnemyRuntimeSpawnArbitration` (pure machine): runtime-spawn position pairing for the live 20 Hz
  bind and same-prefab nearest matching / materialization-list judgment for the late-joiner snapshot.
- `EnemyCombatArbitration` (pure machine): nearest-player selection, spider-bite gate/range and
  crystal-lunge ray-before-ground decisions — the host judgment is L0-tested without Unity.
- `EnemyStateCodec` (pure): snapshot roundtrip (position/velocity/rotation/health/presentation flags)
  with a reflection completeness guard over the presentation-flag bits.
- Death/spawn arbitration (pure): first-writer-wins for enemy death, spawn-event id allocation.

## 5. Tests

- Pure logic: `EnemySpawnArbitration` deterministic assignment/matching; `EnemyRuntimeSpawnArbitration`
  runtime-spawn pairing/materialization decisions; `EnemyStateMsg`/`EnemySpawnEntryMsg` roundtrips;
  `EnemyCombatArbitration` nearest/bite/lunge-ray decisions (multi-dimensional L0 coverage replaces
  manual dual-open acceptance of the decision layer).
- Wire simulation: host-ordered `EnemyAttack` reaches only the victim and fires the apply seam;
  `EnemyLunge` report relays like `EnemyBite`.
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
4. **Runtime spawn** (`CaveTickSpawner`) is the new generation-side surface (16 caveticks on
   trigger) — now covered by the EntitySpawned channel + runtime id binding + late-joiner
   materialization; a future second runtime enemy prefab must reuse the same path and its prefab must
   carry the same `BuildingEntity.id` facts.
5. **Proximity side effects are now kernel-event-synced** — `ElderThornbackBehaviour` (horror tick +
   defeat reward), `XalorisScript` (septic tick) and `GrabberPlant` (grab) each report their
   post-effect terminal state through `RecordEnemyEffectCommand` / `EnemyEffectResultEvent`;
   the `EnemyCombatKernelProjection` merges guest reports into the host saved character and
   restores the presentation event on the peers. `LookTarget` gaze/scare now
   rides the 20 Hz player entity stream (v31); the `Heater` temperature field is
   **excluded by design** — a local-body effect that writes only the local
   player's body temperature (already carried by the 1 Hz character stream),
   recorded in `backlog.md`.
6. **Host-local crystal lunge now has a dedicated report** — `CrystalEnemyLungePatch` captures a
   pre-lunge limb trace, the native hit runs unchanged, and the postfix reports `EnemyLungeMsg`
   only after the pre/post limb diff verifies the actual write.

## 7. Implementation order

1. Domain + protocol (`EnemySpawnArbitration`, `EnemyStateMsg`) + pure-logic tests.
2. Host-side snapshot capture + broadcast.
3. Guest-side freeze (patch enemy scripts, now including spider collision callbacks) +
   snapshot-driven rendering.
4. Late-joiner full snapshot + spawn/death events.
5. Simulation + contract tests + gates.
6. Multiplayer targeting + host-ordered attacks (`EnemyCombatDirector`, `EnemyAttack` /
   `EnemyLunge` protocol) + tests + gates.
7. Enemy-proximity side-effect events (`EnemyProximitySync`, `EnemyEffectMsg`) + the host-local
   lunge verified terminal-state report + tests + gates.
