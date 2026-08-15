# NPC / Enemy Synchronization — Design

Status: **landed** — host-authoritative enemy sync, multiplayer targeting and host-ordered attacks are in; remaining gaps are listed in §6 and `backlog.md`.

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

- Enemy position / velocity / rotation / health ride the host-authoritative 20 Hz `EnemyState`
  stream, with a full `EnemySnapshot` on world entry; the guest freezes its locally generated copies
  and drives them from the stream (`EnemySyncCoordinator` + `RemoteEnemyDriver` + `EnemyPatches`).
- Enemy health changes are synced (`BodyAttackPatch` → `OnBuildingEntityDamaged` →
  `OnRemoteBuildingEntityDamaged` applies `entity.health -= damage`; `RemoteEntityDeath` suppresses
  the remote drop roll).
- Enemy targeting and attacks on remote players are synced through `EnemyCombatDirector` +
  `EnemyAttack`/`EnemyBite`/`EnemyLunge` (§3.6-3.7).

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
- Runtime spawn (`CaveTickSpawner`): the 16 `cavetick` creations already ride the generic
  `OnEntityInstantiated` → `EntitySpawned` channel (prefab id + position + rotation). The guest
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
5. **Proximity side effects stay local-first** — `ElderThornbackBehaviour` (horror/stamina),
   `XalorisScript` (septic shock) and `GrabberPlant` (tendril grab) read
   `PlayerCamera.main.body` and mutate that body directly. They are not part of the
   move-toward-player family this page covers; they are tracked in `backlog.md` until they get
   dedicated event chains.
6. **Host-local crystal lunge still rides the 1 Hz snapshot** — the native host-body hit has no
   dedicated terminal-state report yet (remote victims report `EnemyLunge`; the host-local path
   is the same pre-existing snapshot fallback). Tracked in `backlog.md`.

## 7. Implementation order

1. Domain + protocol (`EnemySpawnArbitration`, `EnemyStateMsg`) + pure-logic tests.
2. Host-side snapshot capture + broadcast.
3. Guest-side freeze (patch enemy scripts, now including spider collision callbacks) +
   snapshot-driven rendering.
4. Late-joiner full snapshot + spawn/death events.
5. Simulation + contract tests + gates.
6. Multiplayer targeting + host-ordered attacks (`EnemyCombatDirector`, `EnemyAttack` /
   `EnemyLunge` protocol) + tests + gates.
