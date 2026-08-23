# CUO Tech Decisions (landed)

This file is the landing log for binding technical decisions that were once inline in the
workspace instruction file. Each entry states the decision, the reasoning, and enough
traceability (commit hashes, protocol versions, `file:line` evidence) to audit it later.
The live rules and conventions stay in the workspace instructions; the architecture
blueprint stays in [architecture.md](architecture.md).

## 1. Technical stack & toolchain

- **BepInEx 5.x, not 6** — the game's installed mod ecosystem (KrokMP-derived mods,
  JustUnknownCharacters, …) is BepInEx-5-only; BepInEx 6 cannot load 5.x plugins, so
  switching would break every existing mod. Revisit only if the ecosystem migrates.
- **net48 TFM** — the game's Mono runtime is .NET 4.x (mscorlib 4.0.0.0, `netstandard.dll`
  present); net48 assemblies are verified to load (KrokMP's Steamworks.NET.dll is
  net48-targeted). Chosen over net452 because MSBuild drops references whose TFM exceeds
  the project's (MSB3274) — Steamworks.NET 2025.163 requires it. Caveat: use only BCL APIs
  the game's Mono actually implements.
- **UnityEngine via NuGet `UnityEngine.Modules 5.6.0`** (template default); **game assemblies
  via `references/` on demand** (see `references/README.md`). Copy game DLLs into the project
  first — never reference them straight from the game folder.
- **Plugin metadata** — `[BepInProcess("CasualtiesUnknown.exe")]` once the plugin does anything
  game-specific; the GUID is permanent, never change it after release. Logging tags map to
  `Logger.CreateLogSource(tag)` sources, not string prefixes.
- **Microsoft.Extensions as CUO infrastructure (landed 2026-08-06, architecture.md §5.5)** —
  DI + `ILogger<T>` bridged to BepInEx logging + Options as config abstraction. Never the
  Generic Host / `BackgroundService` — BepInEx/Unity own the lifecycle; CUO receives it via a
  small `ICuoService` (Initialize/Start/Update/Stop/Dispose) in `CUO.Abstractions`. Package
  layering: `CUO.Abstractions` (Abstractions/Options only — the ONLY assembly mods reference)
  → `CUO.Runtime` → `CUO.GameAdapter`. Pinned to **3.1.32** (net48-compatible line); the
  transitive closure ships 6 `System.*` DLLs that CUO owns centrally. Rolling file log at
  `<game>/BepInEx/logs/`; `tools/deploy.ps1` copies build-output DLLs, never BepInEx-owned ones.
- **Steamworks.NET** — referenced from `references/` (2025.163.0, from the KrokMP install).
  Latest NuGet releases are netstandard2.1-only; the direct DLL reference sidesteps the TFM
  check. The game has NO Steam integration of its own (verified by reversing) — CUO is the sole
  SteamAPI initializer, so no duplicate-init conflict.
- **HarmonyX (0Harmony 2.9.0)** — the game's BepInEx core owns 0Harmony.dll 2.9.0; nuget.org's
  `Lib.Harmony` stops at 2.4.2. Referenced from `references/` so compile-time = runtime version;
  never deployed. Patches live in `CUO.GameAdapter` only.
- **protobuf-net 3.2.56 as the wire serializer (landed 2026-08-07)** — every session message is a
  `[ProtoContract]` class in `Runtime/Protocol/Messages/`; the frame is `[msgId:1][protobuf payload]`.
  Lagrange.Core's Proto module was dropped (SIMD intrinsics a runtime unknown on this Mono, GPL-3.0
  conflicts with CUO's MIT, upstream tracks net8/9/10); protobuf-net is mature, net48-official,
  Apache-2.0, span-based. Hand-written BinaryWriter layouts deleted.

## 2. Wire transport (landed 2026-08-07)

Channel choice follows **"can the loss self-heal"**, not "event vs state". One-shot semantics
(Handshake/HandshakeAck/PlayerJoin/SceneState/WorldStartParams/BlockDamaged/CharacterData) go
reliable; the 20 Hz state stream (PlayerState/PlayerStateReport) goes unreliable with a snapshot
`Seq` (drops are harmless — the next tick overwrites). Reliable-on-stream causes head-of-line
blocking. Never blindly retry a non-idempotent event; retries need idempotency keys. **Topology is
pure star (host-authoritative), no envelope**: guest→host, the host validates/arbitrates and fans
out per-recipient (`SendTo`), excluding the source for echoed events. No guest↔guest traffic, no
src/dst/broadcast-flag envelope.

## 3. Session layer (landed 2026-08-08)

Per-message handlers (`Runtime/Session/Handlers/`, `[PacketHandler(NetMsg.X)]` +
`PacketHandlerBase<TPacket>`); `PacketDispatcher` builds a read-only `Dictionary<NetMsg, IPacketHandler>`
at startup (O(1) dispatch). The session owns its state internally (identity/flags/presence table
are private fields, never DI services — "state belongs to its owner"); consumers depend on the
narrow `ISessionControl` factory, **resolved after the session is built** (abstract extraction over
`Lazy`; reason through "who constructs whom"). Data plane: `PacketReceiver` (bind + direction
validation + `MessageArrived`) + `PacketSender` (one send primitive) + `PacketDispatcher` (routes
with `HandlerContext{ISessionControl, IEntitySyncControl, ICharacterDataControl, IWorldControl}` —
handlers take no service constructor deps, keeping the graph acyclic). Domain services:
`EntitySyncService`, `CharacterDataStore`, `WorldService` hang off the session through the control
interfaces. `ICuoService : IDisposable`.

## 4. Item physics (landed 2026-08-09/10, user mandate; terminal form #124)

The host's physics is the single AUTHORITY for every world item. Guests' copies **freeze-wait** at
the drop spot (kinematic, zero velocity — NO local simulation before the host's stream arrives),
then switch to **local physics** on the first stream tick, isolated to the ground layer (Item=7
collides only with Ground=6; pickup queries are layer-mask queries and ignore the collision matrix)
and mine-shielded (`MineScriptPatches`). The 10 Hz stream soft-corrects: velocity sync every tick,
hard snap past 3-unit divergence, settled copies ease residual gap to zero; the motion→rest EDGE is
sent immediately, then 1 Hz re-aligns. One drop = ONE report carrying the COMPLETE initial vectors
(position, linear/angular velocity, rotation) — the game splits one drop into DropItem→ThrowItem +
a duplicate DropWearable hook, and per-call reporting materialized a zero-velocity ghost. Domain
classes are one responsibility each (`ItemWorldSync` / `ItemPositionAuthority` / `ItemPositionFollow`
/ `ItemApplication` / `DropProtectionGuard` / `MineScriptPatches`). No local "grace period" — the
correct fix is completing the initial condition.

## 5. World entity events (landed 2026-08-10, #123)

Every trap/mechanism entity syncs its trigger through ONE channel: `EntityEventMsg` (NetMsg 66,
report → host apply → relay excluding source → replay) + `TrapStateSnapshot` (67, late-joiner
consumptions) + `EntitySpawned` (68). Replay is three tracks: **explosion family** (visual +
real-body effect, never a local CreateExplosion), **state family** (run the trap's own state
machine), **visual family**. Side effects ride existing channels (craters via SetBlock, building
damage via CreateExplosion health-diff, stats via CharacterData). Model: **item×trap = host
computes** + broadcast; **character×trap = local compute + report**, host re-runs the area effect,
guests replay. Duplicate guard is per-entity per-one-shot flag. The **entity-features matrix**
(`docs/entity-features-matrix.csv`) is the completeness gate.

## 6. Patch contracts + contract tests (landed 2026-08-12, the game-update guard)

Every Harmony hook is a stringified `PatchContract` in `Runtime/Patching/`; `PatchInventory.BuildContracts()`
extracts them from `[HarmonyPatch]` attributes (the 3 dynamic internal-type patches are hand-declared
there). `PatchContractChecker` is the pure verdict; the contract tests load the game assemblies
REFLECTIVELY from `references/` (csproj `ExcludeAssets="compile"`) and assert every contract resolves.
After a game update: re-copy DLLs into `references/` → `dotnet test` names every broken hook BEFORE
the game launches. Missing references are a test FAILURE, never a skip.

## 7. Replay archive + regression (landed 2026-08-12)

Real bugs are fossilized as data files — `tests/.../Replays/*.replay` (one step per line on a monotonic
timeline; `fault` per-link injection; `expect … within <ms>` assertions). Components: `ReplayParser`,
`ReplayRunner`, `ItemSimWorld` (the shared 3-node world; "one player operation = one message"). A file
that cannot be parsed is a FAILURE, never a skip. Every run also emits a `SimTrace` comparable against
real logs via `tools/extract-itemtrace.ps1`. Later extended to the entity/fluid domain (`event` /
`snapshot` / `fluid` actions).

The real-log vs simulation diff automation landed 2026-08-16:
`tools/compare-itemtrace.ps1` resolves a replay's SimTrace (or regenerates it with `-Refresh`),
normalizes real log (plain or `.log.gz`) and SimTrace through the same begin-event/result/events
surface, matches the expected sequence inside a whole-session log (`-Contiguous` / `-Strict` /
`-NoBegins` variants) and enforces the begin-without-end leak contract. See
`docs/selfchecks/simtrace-diff-selfcheck.md`; 887 tests green (9 new script-contract tests).

## 8. Entity-event behavior suite (landed 2026-08-12, Phase 5)

The 25 entity-event kinds are behaviorally homogeneous, so coverage is a DATA-DRIVEN cross-product:
`EntityEventArchives` (one row per kind) × the scenario families in `EntityEventBehaviorTests`
(101 cases; a new kind runs every family automatically). Historical-bug generalizations extracted as
pure units: `FluidRleCodec`, `TurretReplayTimeline`, `GameFieldContractTests`, `ReplayMatrixDataTests`,
`CharacterDataStoreTests`.

## 9. Mod API first round (landed 2026-08-13; `docs/mod-api.md` is the binding contract)

Discovery + lifecycle + manifest + mod messages + session events + handshake consistency. BepInEx 5
loads plugins one-by-one load-then-Awake, so `ModService` discovers on the first update frame
(`[CuoMod]` + `ICuoMod`, both in `CUO.Abstractions`; `NetworkMode` defaults to `Unspecified` and is
REJECTED — fail-closed). Mod = empty BepInEx plugin shell + a separate `[CuoMod]` class. `IModContext.Session`
is a bind-time snapshot. Mod messages (NetMsg 75) are report/directed, star topology, NO auto-relay,
64 KiB cap, opaque payload. Handshake consistency (ProtocolVersion 2→3, behaviorally breaking) rejects
BEFORE the member is created.

## 10. Crafting domain (landed 2026-08-13)

One operation = one report. The game splits one craft across several calls; `CraftingSync` opens a
`CallContext.Origin.Craft` scope, snapshots the pre-state (materials, liquid fingerprints, full-inventory
object set), silences the five sub-hooks in scope, and commits ONE `CraftReportMsg` (Destroyed/Changed
with post-state digests; material disposition from the RECIPE data, never scene inference). End-of-frame
destroys ride a destroy-claim set. The host (`CraftSyncService` + pure `CraftReportJudge`) classifies
each entry by table membership. Blueprint unlock is `RecipeUnlockMsg` (77). Codec kind 6 covers enums.
ProtocolVersion 3→4.

## 11. Cross-domain fix round (2026-08-13 — #191/#192/#194)

- **#191 destroy echo** — a quitting guest tore down its scene while the session read alive; every world
  item's OnDestroy reported as a player destroy (70/637 reports) and the host deleted its copies.
  `ItemWorldSync.SuppressDestroys` is engaged by `SceneLoadPatches` (the game's ONE `SceneManager.LoadScene`
  path) and `Plugin.OnApplicationQuit` (via `IGameAdapter.OnApplicationQuit`); reset on the world-entry
  edge. A destroy during teardown is the teardown, never a player operation.
- **#192 transfer-table cross-run residue** — `_transferred` never cleared on a new run, so a reconnect
  restore merged the OLD run's entries in. `ItemArbitration.ClearTransferred()` runs in
  `RunCoordinator.OnWorldJoinRequested` beside `ClearSavedCharacters()`.
- **#194 world-item use** — using a world item in place had no transfer-table entry, so the old path
  warned and fell back to a carried-fact broadcast, leaving the host's world state stale. Now classified:
  world item → `UpdateWorldItemState` + local correction + `SendWorldItemCorrection`; carried → adoption.
  `ItemActionSync` split out of `ItemService` at the 600-line gate.

## 12. Reconnect-restore rounds (2026-08-13, ProtocolVersion 5)

- **Item identity** — `RestoreItem`/`RestoreWearable` now attach `itemData.InstanceId` (exact rebuild; the
  id-less restore read as a runtime spawn → UnknownItem reject → rollback).
- **Leave-spot position** — `CharacterDataMsg.ProtoMember(7) Position` (null = no claim) captured at every
  snapshot; restore applies it with zero velocity.
- **World-entry snapshots** — the InWorld edge now sends block-state + trap-state + opened-entities
  (`OpenedEntityRegistry` + `OpenedEntitiesSnapshot`, NetMsg 78) together; 60 s resend kept as fallback.
- **Runtime-feedback fixes** — spawn-frame teleport (`ApplyPendingPosition` on the body's first frame),
  door replay from elapsed (`EntityEventMsg.ProtoMember(4) ElapsedSeconds`, extracted `ShuttleDoorReplayState`),
  spike-vanishing diagnostic (`LogGoneWithNearest<T>`).

## 13. Test-hardening round (2026-08-13, 499 → 545 tests)

The coverage audit: all 499 tests sat in the Runtime domain while every user-found defect lived in the
GameAdapter application layer (zero direct test face). Closed by (a) contract completion —
`GameFieldContractTests` grew to 40 rows covering EVERY `Traverse` access; (b) behavior simulations for the
zero-covered live paths (`ItemSnapshotSimulationTests`, `ItemMoveSyncTests`, `ItemIdCoordinatorTests`,
`WorldEventRelayTests`, `CharacterDataStoreTests`); (c) judgment extraction (`SaveableFieldKind`,
`ShuttleDoorReplayState`, `LayerModifierDecision`).

## 14. Trap layout authority (landed 2026-08-14, ProtocolVersion 6)

Generated trap POSITIONS diverged between sides while the block fingerprint stayed identical. Root cause,
source-verified: entity distribution is NOT determinism-safe — `DistributeEntities` validates via PHYSICS
queries (per-side collider timing) and `PlaceBody` scans `Physics2D.OverlapBox` (Body.cs:1733), so the first
`body.y`-dependent entity lands wherever each side's physics agreed. Fix = host-authority, not determinism
guessing: `TrapLayoutSnapshot` (NetMsg 79) rides the world-entry snapshot group; `TrapLayoutScanner` enumerates
sync-domain trap entities (the `TrapEntityScan` component table) with each instance's OWN prefab name; the
guest's `TrapLayoutApplication` aligns via `TrapLayoutAlign` (pure greedy nearest-neighbour within 3 units).
Recorded boundary: decorative plants are not aligned (visual divergence only).

## 15. Guest-leave must never end the host's session (fixed 2026-08-14)

The "all members left → EndSession" block was wrong: a host playing alone is legitimate, and EndSession is
IRREVERSIBLE, so a guest quitting killed the lobby (Members 0, rejoining guest could never handshake back,
loading screen stuck). Deleted the block and `_hadMembers`: a vanished member is removed and the session
CONTINUES; only the host's own absence ends it. Two tests had locked the WRONG semantics and were rewritten.
Lesson: a test that locks "what the implementation does" instead of "what the semantics should be" is a
liability, not a guard.

## 15. Multiplayer enemy targeting + host-ordered attacks (ProtocolVersion 7)

The game's enemy AI discovers players through physics queries / `PlayerCamera.main.body`, which only
see the LOCAL body — remote render clones have every collider disabled by design
(`RemoteBodyFactory`, evidence: contacts re-activate frozen rigidbodies). Rather than re-enable clone
colliders (route B, known physics pitfalls), the host-side `EnemyCombatDirector` resolves targets by
distance: SpiderHandler on its own `moveTime` expiry edge (SpiderHandler.cs:95) and
`CrystalEnemy.body` inside its 64-unit close radius (CrystalEnemy.cs:25). Since the host's collision
callbacks still cannot touch a remote clone, attacks on remote players travel as the one-shot
`EnemyAttack` command (NetMsg 83) — the victim applies the game's damage path locally and reports the
terminal state (`EnemyBite` existing, `EnemyLunge` NetMsg 84 new). Frozen spider collision callbacks
are skipped on the guest so one attack has exactly one apply path (the old frozen-copy bite would race
the host command). Accept-first: the host decides the hit by nearest-along-ray/distance, never by
strict collision validation.

## 16. Runtime enemy spawn binding (ProtocolVersion 8)

The cave-tick nest spawns 16 `cavetick` enemies at RUNTIME on the triggering side only
(`CaveTickSpawner.cs:41-52`; prefab evidence: `cavetick` = BuildingEntity animal + SpiderHandler). The
generic `BuildingEntity.Start` → `EntitySpawned` channel already creates the copies on the peers, so the
gap was identity, not creation. Fix: the guest freezes every runtime animal copy at its Start
(`EnemySyncCoordinator.OnAnimalInstantiated`) before AI/physics move it; the host marks every enemy id
allocated after the initial deterministic mapping as runtime; `EnemySnapshotMsg.RuntimeSpawns`
(ProtoMember 2, id + prefab + current position/rotation) backfills a late joiner, which matches existing
copies by prefab/position or materializes them with `Utils.Create` + `SpawnReplayMarker`. Live 20 Hz
batches bind unbound runtime ids to the EntitySpawned-created copies by sorted position,
all-or-nothing (`EnemyRuntimeSpawnArbitration`). A v7 peer neither binds runtime ids nor materializes
late-join runtime copies, so the version gate refuses mixed sessions instead of silently degrading
enemy spawn sync.

## 17. Enemy proximity effects + host-local lunge report (ProtocolVersion 9)

The remaining enemy-interaction gaps are closed with the same star-shaped dedicated-event pattern
as `EnemyBite` / `EnemyLunge`:

- **Prefab freeze surface** — runtime component check confirmed every moving enemy prefab carries
  `SpiderHandler` / `SpiderHandlerTBE` / `CrystalEnemy`, all already frozen by `EnemyPatches`;
  no freeze extension needed.
- **`EnemyEffectMsg` (NetMsg 85, bidirectional)** — `ElderHorrorTick / ElderHorrorDefeat /
  XalorisSepticTick / GrabberGrabbed` carry the post-effect terminal state; `EnemyProximitySync`
  reports on the game's own verified transition edges (`timeChecked`, `lastTime`, `grabBody`).
  `EntityEventKind.GrabberGrabbed` is retained ONLY as the trap-layout identity key.
- **Host save-merge** — `EnemyTerminalStateApplier` (pure) applies bite/lunge/effect terminal state
  to a `CharacterDataMsg`; the host's `CharacterDataStore` merges events into `_savedCharacters`
  immediately, so a disconnect before the next 1 Hz snapshot no longer loses the last event.
  `CharacterHealthMsg` gained `HorrifiedLevel / FocusedLevel / EyePanicTime` (ProtoMember 62-64).
- **Host-local CrystalEnemy lunge** — `CrystalEnemyLungePatch` passes a pre-lunge limb trace
  through Harmony `__state`; the native `Lunge` still applies the hit, and the postfix reports
  `EnemyLungeMsg` only after the pre/post limb diff verifies the actual write (verified commit).
- ProtocolVersion 8→9: a v8 peer would drop `EnemyEffect` and the save-merge, so mixed-version
  sessions are refused instead of silently degrading enemy-effect sync.

## 18. Lobby-domain lifecycle refactor (2026-08-15)

The lobby identity used to be process-lifetime: `SessionService.OnLobbyEntered` returned early when
the client had ever hosted (`Role == Host`), so a player who first hosted and then joined a friend
never became a Guest and never followed the host's run. The lobby identity is now a state machine:

- **Role follows the actual lobby** — `None` when in no lobby (`LobbyLeft`), `Host` on
  `LobbyCreated` / entering one's own lobby, `Guest` on entering someone else's lobby. `EndSession`
  still keeps the role for same-lobby outage/rejoin; only a real `LobbyLeft` drops it.
- **Explicit leave-before-acquire** — both `JoinLobby` and `CreateLobby` leave the current lobby
  first and fire `LobbyLeft` on `ISteamService`; the official JoinLobby docs do not promise an
  automatic leave, so CUO no longer depends on one.
- **Session teardown is complete** — `SessionService.TeardownSession` stops sends, fires each
  member's `RemoteSceneChanged(false)` edge, clears presence, and fires `SessionEnded` once; the
  world/item/character/entity/enemy/adapter domains reset their session-scoped state on that event.
- **Menu-only switch policy** — `LobbySwitchGuard` (pure) refuses lobby changes while a world is
  running or generating, except the existing solo-in-world -> host-lobby conversion. The guard lives
  in the Plugin (F8/F9/Steam-friend join request), so the session layer never sees a half-switched
  run. No wire change: ProtocolVersion stays 9.
- **Late Steam init** — `EntitySyncService.Update` refreshes the local entity's SteamId every frame;
  the F8 retry path after a startup `SteamAPI.InitEx` failure used to leave it 0 and the
  self-activation `PlayerJoin` never matched ("no member with that entity id").
- **Steam receive batch is all-or-nothing** — `SteamTransport.Poll` now catches per-message handler
  exceptions and releases each message in a `finally`; one throwing snapshot used to kill every
  later message in the same received batch (observed: `WorldReady` was lost for the full 60 s gate
  timeout because an enemy-snapshot materialization threw before it in the same poll batch).


## 19. Mod API second round — permissions, host commands, dependencies, SemVer (ProtocolVersion 10)

- **Permission model** — `ModPermission` flags on `[CuoMod]` default to None (nothing implicit);
  `ModPermissionPolicy` rejects unknown bits and host/state permissions on ClientOnly/Cosmetic.
  Live enforcement: `SendNetworkMessage` gates `IModNetwork` send+receive, `RegisterCommand`/
  `ExecuteHostAction` gate `IModCommands`. Handshake carries the flags; state-bearing modes
  require exact flag equality.
- **Host commands** — `IModCommands.Register`/`TryExecute`, execution ONLY on the host's mod copy.
  Guest→host `ModCommandRequest` (NetMsg 86) → host validation (shape caps, handshaken member,
  registration, permissions, per-guest token bucket 4/s burst 8) → execution → directed
  `ModCommandResult` (NetMsg 87) settles the guest callback by request id. Pending callbacks are
  settled on SessionEnded/Dispose. Handler output cap 32 KiB, error cap 4 KiB.
- **Dependency ordering** — `[CuoMod].Dependencies`; discovery rejects missing/self/duplicate/
  cycle/transitive-rejected targets and returns a stable Kahn topological order; Stop/Dispose run
  in reverse order. Runtime load skips a dependent when its dependency failed to load.
- **SemVer** — strict SemVer 2.0.0 parser/precedence (`SemanticVersion`); discovery rejects
  non-SemVer versions; state-bearing handshakes compare precedence (build metadata ignored).
  Same-id NetworkMode mismatch is now rejected when either side is state-bearing (a first-round
  matrix gap).
- **Rate limits** — token buckets per sender: mod messages 20/s burst 40; command requests 4/s
  burst 8 (`ModRateLimitPolicy`/`ModRateLimiter`, virtual-clock testable).
- **ProtocolVersion 9→10** — v9 peers drop the permission flags and command messages, so mixed
  versions are refused instead of silently degrading.
- Binding contract: `docs/mod-api.md`; two-process runtime verified with host + Steam1 sandbox
  (permissions discovered, handshake end-to-end, guest→host `echo`/`whoami` results success,
  host-local command output, mod-message report).

## 20. Damaged building-entity health snapshot (ProtocolVersion 11)

The live `BuildingEntityDamaged` relay is position-keyed and only updates peers that are already
in the world; a late joiner regenerates every building entity at full health, so destroyed
plants/crates resurrected and intermediate damage was lost. The fix mirrors the opened-entities
snapshot family:

- **`BuildingEntityHealthRegistry`** (Runtime/World, host-authoritative): position-keyed
  (`(floor x, floor y)`) latest-health records, cap 4096, reset with `ResetDamagedBlocks`.
- **`BuildingEntityHealthSnapshot` (NetMsg 88, host → guest)**: cell-centre position + current
  health entries, sent in `HandlerContext.SendWorldStateToMember` (world entry + reconnect) and
  the 60 s resend. Floats are immune to protobuf zero-omission (an omitted 0 decodes to 0), so
  destroyed-entity health 0 round-trips.
- **Game Adapter recording** — the local damage path, the remote-damage apply path, the local
  open path and the remote-open apply path all report the post-write health (host-only); a
  guest-reported hit applied on the host is therefore part of the snapshot history.
- **Guest application** — `WorldEventSync` finds the generated copy at the position, writes the
  host's health, and marks `< 0.5` with `RemoteEntityDeath`, so the local `BuildingEntity.Update`
  never rolls a second set of drops.
- **ProtocolVersion 10→11** — a v10 peer drops NetMsg 88 and would regress the late-joiner state,
  so mixed-version sessions are refused instead of silently degrading.
- Tests: registry semantics (`BuildingEntityHealthRegistryTests`), direction table, wire round-trip
  with health 0, and the world-entry/reconnect snapshot groups extended from five to six snapshots.

## 21. Partial block-damage snapshot + metallic damage multiplier (ProtocolVersion 12)

The live `BlockDamaged` relay is delta-based and only aligns peers that were present for every
hit; a late joiner regenerates every block with zero accumulated `BlockDamage.damage`, so a
partially-mined block was back at full HP and broke later (the chain desynchronized). The same
audit found a live-relay multiplier bug: the `DamageBlock` postfix reported the RAW damage but not
`bonusMetal`, and the receiver hard-coded `false` — a laser (`metalMoreDamage = true`,
Item.cs:4645) against a metallic tile (WorldGeneration.cs:715 multiplies by 10) applied 10× on the
attacker and 1× on the peers. Closeout:

- **`BlockDamageRegistry`** (Runtime/World, host-authoritative): block-cell-keyed latest
  `BlockDamage.damage`, cap 256 (the game's own `blockDamages` list caps at 128,
  WorldGeneration.cs:732-737), reset with `ResetDamagedBlocks`; a break / applied air write
  removes the cell.
- **`BlockDamageSnapshot` (NetMsg 89, host → guest)**: integer block cells + accumulated damage,
  sent in `HandlerContext.SendWorldStateToMember` (world entry + reconnect) and the 60 s resend.
  Integer zero coordinates are protobuf zero-omission transparent.
- **Host recording** — `BlockBreakSync` reads the game's own post-write `BlockDamage.damage` on
  the local damage path and the host-side remote-damage apply path; the value is already
  post-metallic-multiplier, so the snapshot carries true accumulated HP.
- **Guest application is an ABSOLUTE set** — find or create `BlockDamage`, write the host damage,
  `UpdateSprite`; it never rides `DamageBlock`, so a re-sent snapshot cannot add or go negative.
  Air cells and `damage >= blockHealth` are skipped (a break is the block-state snapshot's
  semantic).
- **`BlockDamagedMsg.MetalBonus` (ProtoMember 4)** — raw damage + source bonus flag; every apply
  path passes the flag into the receiver's own `DamageBlock`, which multiplies identically on the
  same generated block type. The pending-break state carries it through the one-frame drops hold.
  A damage-only report against an already-air cell is ignored before `DamageBlock` (air health is
  0 — WorldGeneration.cs:315-322 — so the old path created a transient air `BlockDamage` and
  played hit sounds/particles); an accepted break relay's drops still materialize on an
  already-air guest cell.
- **ProtocolVersion 11→12** — a v11 peer would drop NetMsg 89 and apply the wrong metallic
  multiplier, so mixed-version sessions are refused instead of silently degrading.
- Tests: registry semantics (`BlockDamageRegistryTests`), direction table, wire round-trips
  (origin cell / damage 0 and `MetalBonus = true`), pending-state bonus preservation, the
  accepted-break relay bonus preservation, and the world-entry/reconnect snapshot groups extended
  from six to seven snapshots.

## 22. World-time flow — host-authoritative fast-forward + all-unconscious sleep (ProtocolVersion 13)

`Time.timeScale` is process-global world state, so per-side fast-forward/sleep would run each
world at a different rate. KrokMP's server-relay shape was reviewed as reference only; CUO uses
its own request/policy split and pure decision machine:

- **`WorldTimeSpeed` (Normal/Fast/SuperFast/UnconsciousFast/DyingFast)** — Slowmo and Paused are
  deliberately NOT wire speeds; they remain local-only presentation semantics.
- **Guest intents are requests, never local writes** — `PlayerCameraSetTimeScalePatch` routes
  Normal/Fast/SuperFast to `WorldTimeRequest` (NetMsg 90); the host answers with `WorldTime`
  (NetMsg 91). UnconsciousFast/DyingFast local calls are swallowed (host-owned). Slowmo/Paused
  and forced local transitions stay local-only.
- **Sleep acceleration is host-computed, all-unconscious only** —
  `PlayerCameraHandleUnconsciousScreenPatch` opens a `WorldTimeSleepLocal` CallContext scope, so
  the vanilla black-screen 25×/3.5× SetTimeScale calls never run in a session. `WorldTimePolicy`
  (pure) accelerates only when every in-world ALIVE player has `consciousness <= 20`; any
  `brainDying` player limits the session to 3.5× (DyingFast), otherwise 25× (UnconsciousFast).
  Dead players are ignored; an unobserved (just-joined) player blocks acceleration.
- **Movement gate** — `WorldTimePolicy` treats any conscious player above 0.5 m/s (squared
  threshold 0.25) as moving and returns Normal; the request is CLEARED (the
  `WorldTimeDecision.NextRequested` field), so a fast-forward never re-applies itself after the
  blocking condition ends. The policy uses the host's 20 Hz velocity buffers plus the 1 Hz
  character snapshots for exact consciousness/blood pressure.
- **Direct-write adoption / enforcement** — the host pump maps actual `Time.timeScale` back into
  the domain (quake-start reset, console) and broadcasts; the guest pump enforces the last host
  speed when a direct writer moved it to another domain speed.
- **Late joiners + self-heal** — the host broadcasts its current speed on
  `RemoteSceneChanged(inWorld=true)` and every 5 s (idempotent absolute speed).
  `WorldTimeSync` is gate-aware: while the start gate holds (`WaitingForReady`),
  the gate owns `timeScale = 0`; a broadcast arriving then is recorded and
  enforced on release.
- **ProtocolVersion 12→13** — a v12 peer would keep its own timeScale and diverge world timers,
  so mixed-version sessions are refused instead of silently degrading.
- Tests: `WorldTimePolicyTests` (pure policy matrix), `WorldTimeFlowTests` (request/broadcast
  over the real wire), direction-table rows, protobuf zero-omission round-trip, and
  `PatchContractTests` re-verifies both new PlayerCamera patches against the game assembly.

## 23. CrystalMimic trigger sync — one-shot latch event + EntitySpawned enemies (ProtocolVersion 14)

CrystalMimic spawns 1-2 `crystalenemy` on its first touch/attack and latches `activated`
(CrystalMimic.cs:23-49). The spawns already ride the generic `EntitySpawned` channel and the
runtime enemy-spawn binding; the missing piece was the latch: a peer whose mimic stayed
unconsumed would spawn a SECOND set on its own collision/attack. Closeout:

- **`CrystalMimicTriggered` (EntityEventKind 30, one-shot)** — observed on the PUBLIC
  `CrystalBehaviour.OnCollisionEnter2D` / `BuildingHit` dispatchers (the exact entry points that
  call the internal effect, CrystalBehaviour.cs:74-88); the patch reports the mimic `activated`
  false→true edge. No dynamic patch target needed.
- **Host apply / guest replay** — `TrapStateActions.ApplyCrystalMimic` writes the latch only
  (never spawns from the event); live replays play the trigger side's exact 2D `observerlaugh`
  call (CrystalMimic.cs:29/43). A late-joiner snapshot replay is state-only (silent).
- **Spawn chain unchanged** — the game-created `crystalenemy` entities ride `EntitySpawned` +
  `EnemySyncCoordinator` runtime binding; late joiners materialize them from
  `EnemySnapshot.RuntimeSpawns`. The trigger-side `SetColor` now travels as creation data
  (`EntitySpawnedMsg` / `EnemySpawnEntryMsg` tint fields, ProtocolVersion 24): the host carries
  the EXACT post-SetColor color + light intensity and receivers write it directly — never the
  native `SetColor`, whose per-side random jitter would diverge.
- **Channel family fixes landed in the same round** — (a) host-triggered one-shot events now
  record into `TrapConsumptionRegistry` in `EntityEventChannel.SendEntityEvent` (the host is not
  in its own presence table, so the old remote-report-only path lost every host-triggered
  consumption for late joiners); (b) `EntityEventHandler` / `EntitySpawnedHandler` no longer
  broadcast — the adapter domain (`EntityEventSync` / `EntitySpawnSync`) is the single relay
  owner, which removes the duplicate relay every guest used to receive.
- **ProtocolVersion 13→14** — a v13 peer would leave the mimic's latch unconsumed and re-trigger
  the crystalenemy spawn, so mixed-version sessions are refused instead of silently degrading.
- Tests: the combinatorial entity-event suite auto-runs the new kind; new simulations lock the
  host-triggered late-joiner snapshot, the event/spawn channel split, and exactly-one relays;
  `GameFieldContractTests` locks `CrystalBehaviour.effects` + `CrystalMimic.activated`;
  `PatchContractTests` locks the two `CrystalBehaviour` dispatcher patches and the now-complete
  7-entry dynamic patch inventory.

## 24. In-flight pickup queue — bounded hold instead of immediate UnknownItem reject

A pickup report that beat its own spawn report used to be refused immediately, which rolled
back the picker's local pickup and left the late spawn in the world for a manual re-pickup.
The old branch conflated that in-flight race with the obvious first-writer conflict. The
host-side arbitration is now three-way:

- **Known item** — the normal transfer (`CompleteAcceptedPickup`, the extracted single path).
- **Obvious conflict** — the item is already in any guest's transfer table
  (`ItemArbitration.IsTransferredToAnyGuest`) → immediate `UnknownItem` reject (the pending
  queue is not for completed transfers).
- **Registration still in flight** — claim waits in `PendingPickupQueue` for **500 ms**
  (pure state; `PendingPickupPump` is the per-frame expiry edge). A spawn/drop registration
  that confirms the item settles the first queued claim through the normal transfer and
  rejects later queued claims (first-writer-wins); a registration that makes the claim a
  container content resolves it silently (the container transfer carries it). Expiry sends
  exactly one late `UnknownItem` reject, or transfers if the item registered through a
  non-settling path.
- **No wire change, no ProtocolVersion bump** — the queue is host-local timing; every
  message involved already existed. A mixed-version session stays compatible.
- **Test-harness correction** — `FakeNetwork.FlushDue` re-entered from a handler's no-delay
  send and delivered a later-due frame in the middle of the current handler (A,C,B instead
  of the production poll-batch A,B,C). Nested flushes are skipped now; the outer flush
  drains everything due. This locked the whole-handler atomicity the queue reasoning
  depends on.
- Tests: pure `PendingPickupQueueTests`; settle-inside-hold and timeout simulations in
  `ItemRaceTests`; the jittered random-lifecycle oracle now models the queue + pump ticks;
  two replay fossils (`pickup-spawn-inflight.replay`, `pickup-spawn-inflight-timeout.replay`);
  `TransportTests.HandlerSends_DoNotReenterTheFlushingBatch`. 818 tests green.

## 25. Config foundation — BepInEx ConfigFile → IOptionsMonitor + logging levels + state-stream cadence

The 2026-08-09 config decision set Phase 4 Mod API as the trigger; that round has landed, so the
config foundation goes in now (no protocol change):

- **Bridge** — the plugin owns the `ConfigEntry` declarations and replaces the Runtime's default
  `MutableOptionsMonitor<T>` registrations with `BepInExOptionsMonitor<T>` (subscribes to
  `ConfigFile.SettingChanged`, filters watched `ConfigDefinition`s, re-reads the snapshot and
  notifies `IOptionsMonitor<T>.OnChange` listeners).
- **Logging levels** — `[Logging] MinimumLevel` (default Information) is enforced by both log
  providers, not by `SetMinimumLevel`. The logging factory stays at Trace so a config change takes
  effect live; normal play no longer fills CUO logs with Trace/Debug traffic.
- **State-stream cadence** — `[Sync] StateStreamHz` (1-60, default 20, BepInEx range-clamped)
  replaces the hard-coded 20 Hz consts in `EntitySyncService` (player host broadcast + guest
  report) and `EnemySyncService`. The 1 Hz `CharacterData` snapshot is explicitly NOT part of
  this knob — it is the full-fact fallback, not a state stream.
- **Attack-swing hold adapts** — `AttackSwingState` now holds
  `max(300 ms clip, 6 × configured interval)` so the `IsAttacking` rising edge keeps its original
  six-tick drop resilience at any configured cadence.
- **Layering** — `Configuration/` options types and monitors live in the Runtime; the Plugin is
  the only layer that knows the BepInEx `Config` instance.
- Tests: `BepInExOptionsMonitorTests` / `MutableOptionsMonitorTests` / `StateStreamOptionsTests`
  (L0 bridge + normalization), `LoggingOptionsTests` (both sinks + DI replacement path),
  `StateStreamFrequencyTests` (real production pumps counted at 20/10/5 Hz over the fake network),
  extended `AttackSwingStateTests`. 839 tests green.

## 26. Heater cooker meat→steak conversion — one ItemCook event (ProtocolVersion 15)

The raw→cooked item-domain TODO is closed as a dedicated host→guest event, never a decomposed
`ItemDestroy` + `ItemSpawn` pair:

- **Trigger side stays native** — `HeaterCookPatch` does not reimplement `Heater.OnCollisionEnter2D`
  (Heater.cs:41-49). The host/solo original instantiates the steak, writes `condition * 0.3f`,
  destroys the raw meat and plays Scald; the patch only verifies and reports the terminal fact.
- **Guest can never cook** — guest world items are layer-isolated to Ground
  (`ItemPositionFollow.cs:186-198`), and the patch's prefix additionally returns false for a guest
  in an active session (`IPatchBridge.IsHeaterCookAuthority`). The host's full-physics copy is the
  single conversion site.
- **Created-steak fingerprint** — the postfix claims the created steak only when it is the exact
  `"steak"` id, is not yet registered in `Item.allItems` (Start has not run in the same physics
  callback), has the exact `source × 0.3` condition and sits at the captured raw-item position.
  A failed fingerprint claims nothing — the existing generic hooks remain the fallback.
- **One operation = one message** — `HeaterCookSync` stamps the steak's `ItemInstanceId` before
  `Item.Start`, claims the raw-meat destroy, and commits `ItemCook` (NetMsg 92) through
  `ItemService.SendItemCooked`, which performs one atomic table transition (source removed, steak
  registered) and broadcasts the full cooked-item capture.
- **Guest replay is atomic** — `ItemApplication.OnRemoteItemCooked` kills the source copy and
  materializes the steak in one `RemoteApply` scope, idempotently (missing source is fine, an
  existing cooked id skips the duplicate), then replays the exact `Sound.Play("Scald", ...)` call.
- **No new late-joiner snapshot** — the cooked steak is an ordinary world-table entry and rides the
  existing world-entry `ItemSnapshot` + periodic keyframe + position stream.
- **ProtocolVersion 14→15** — a v14 peer would never learn the conversion and would keep raw meat
  where the host has a steak, so mixed-version sessions are refused.
- Tests: `ItemCookSimulationTests` (wire + table transition), `HeaterCookPatchTests` (reflective
  pure rule + patch surface + contract), the `heater-cook.replay` fossil, and the `DirectionTests`
  completeness guard. 863 tests green.


## 27. Character-data disk persistence (no protocol change)

The host's per-SteamID character saves were in-memory only and died with the host
process. The saves are now disk-backed without touching the wire:

- **`CharacterDataFileStore`** (Runtime/Session/CharacterData) owns path, format and
  atomic writes. The file is a versioned protobuf wrapper (`CharacterDataFile`) with
  `(SteamId, CharacterDataMsg)` entries; serialize to `<file>.tmp`, flush, then
  `File.Replace`/`File.Move`, so a crash never leaves a half-file current.
- **Lifecycle** — `CharacterDataStore` loads the file exactly once at construction
  (the host-restart / continue-run path), persists after every verified mutation
  (guest report save + enemy bite/lunge/effect terminal-state merges), and keeps
  memory session-scoped: `SessionEnded` clears memory, the file survives.
  `ClearSavedCharacters` (new run) writes an empty-table tombstone first, then
  deletes — a failed delete can never resurrect the old run's supplies.
  `SendSavedCharacter` requires the host to be `LocalInWorld`; a menu handshake
  never stages a previous run's save for the next run.
- **No same-process lazy reload** — after a session end the next run's identity is
  unknown, so the old process must not hand out the previous run's save to a
  brand-new lobby; only a new process start (restart / continue-run) reloads.
- **Production path** — the Plugin passes `Paths.ConfigPath /
  CasualtiesUnknownOnline.character-data.bin` (computed at runtime, no committed
  machine path). `null` disables persistence and is the test-composition default.
- **Degradation** — missing file = empty; corrupt/unknown-version file = warn +
  empty (never a startup crash); failed save/delete = warn and keep the in-memory
  session working.
- **No protocol bump** — ProtocolVersion stays 15; the file is a local artifact and
  the Game Adapter/Item domains are untouched.
- Tests: `CharacterDataFileStoreTests` (full-field round-trip, missing/corrupt/
  version degradation, delete, temp-file contract) + `CharacterDataPersistenceTests`
  (full-DI restart restore, new-run clear, session-end disk survival, the three
  terminal-state merge kinds across restart, corrupt-file startup, failed-clear
  no-leak). 877 tests green.

## 28. Tutorial-claw props are per-player until pickup (no protocol change)

The tutorial courses run per side (`TutorialHandler.main` exists in each process and every
course coroutine drives that side's claw). Reporting both sides' `objectToCreate` copies into
the shared item/entity domains produced the claw double-give — one host-id copy plus one
guest-id copy on every screen. The props are therefore declared per-player course objects:

- **Marker at the native creation** — `TutorialHandlerUpdatePatch` opens the
  `TutorialClawSpawn` call-identity scope around `TutorialHandler.Update`; the
  `UtilsCreateTutorialPatch` postfix on the exact `Utils.Create(string, Vector2, float)`
  overload adds the field-less `TutorialClawProp` marker in the same postfix, before the
  created object's `Item.Start`/`BuildingEntity.Start` run.
- **Item path** — `ItemWorldSync.OnItemInstantiated` skips marked items, so a claw item stays
  id-less exactly like a generation-time item. The first real pickup/wear exits through the
  existing id-less branch of `PickupSync.OnPickedUp` (spawn-then-pickup, one commit); from then
  on the item is an ordinary domain item (position stream, keyframe, character data).
- **Entity path** — `EntitySpawnSync.OnEntityInstantiated` skips marked building props; a
  claw-placed fence/terminal never rides `EntitySpawned` and stays local to the player's course.
- **No cross-player binding** — `ItemApplication.FindExistingAt` and
  `EntitySpawnSync.FindExisting` both skip marked props, so one player's shared pickup/spawn can
  never bind to (and then destroy) another player's private course object.
- **No protocol bump** — the marker is process-local and the wire formats are untouched;
  ProtocolVersion stays 15.
- **Accepted boundary** — tutorial course state was already per-side; a prop created before a
  late joiner arrives is not in the joiner's snapshot (the joiner's own course creates its own
  copy). The deliberate tutorial-domain sync pass is decision #36 (the 20 Hz claw presentation
  stream; course/prop state itself remains per-side).
- Tests: `TutorialClawPropTests` (reflective marker/scope/patch-shape/contract locks) plus the
  existing item simulation/race suites covering the unchanged spawn-then-pickup transfer.
  899 tests green.


## 29. Limb/death/bleed/mining presentation sync — LimbStateEvent + SwingSeq (ProtocolVersion 16)

The character-presentation backlog item is closed with one full-terminal-state event for every
limb latch, a clone limb renderer fed by the 1 Hz snapshot, and a per-swing sequence for rapid
mining swings:

- **Trigger family is complete** — `LimbStatePatches` covers `BreakBone` / `MendBone` /
  `Dislocate` / `UnDislocate` / `Dismember` (Limb.cs:193-273; natural healing reaches
  MendBone/UnDislocate through Limb.Update, Limb.cs:518-522). Those five methods are the only
  writers of `broken`/`dislocated`/`dismembered` in the decompiled assembly.
- **Verified transitions, not post-state** — each patch captures the latch in a prefix
  `out bool __state` and reports only a false→true / true→false edge; a repeated BreakBone that
  merely refreshes `boneHealTimer` is not an event. Clones are excluded by `RemoteBodyDriver`.
- **One operation = one full message** — `LimbStateEventMsg` (NetMsg 93, bidirectional star
  relay like EnemyBite) carries the owner's WHOLE post-event limb set + `CharacterHealthMsg`,
  because `Dismember` deactivates lower limbs and mutates connected limbs in the same call
  (Limb.cs:91-145) and every latch also writes body fields. The host merges via
  `EnemyTerminalStateApplier.ApplyLimbState` (whole-set replace) into both the saved character
  and the clone fact table.
- **Clone limb visuals are applied, never simulated** — `CloneLimbRenderer` replaces the
  skipped clone `Limb.Update` with the snapshot/event state: replicated brokenBone sprite,
  both-direction dismember toggle, the full seven shader params
  (`_SkinDamage/_MuscleDamage/_InfectionPercent/_SnowAmount/_Dirtyness/_Pain/_BloodOverlay/_Wetness`,
  Limb.cs:487-488/501-506), and the game's >0.95 fur-blood drip threshold. Its bone sprite uses
  the separate `RemoteCloneLimbRender` marker so the inventory renderer's worn-item cleanup
  never destroys it.
- **Rapid mining swings** — `EntityStateMsg.SwingSeq` (proto field 7) is a rolling per-swing
  sequence published beside the held `IsAttacking` flag; `SwingReplay` replays `ArmsSwing` on
  every sequence change (each swing inside one held window), keeps the flag rising edge as the
  old-sender fallback, and only seeds the sequence on the first snapshot (no historical replay).
- **Death pose L0-locked** — the pump's `(!standing || !alive) && !sleeping` rule is extracted
  as the pure `LyingPose` machine.
- **ProtocolVersion 15→16** — a v15 peer would not understand NetMsg 93 and would merge rapid
  swings; mixed-version sessions are refused instead of silently degrading.
- **Accepted residuals** — the clone's body-level `FacialExpression` latches (disfigured/eye
  sprites + the owner's random disfiguredIndex) stay template-driven; underwater/downward
  fur-blood transfer branches are owner-side simulation, so the synced terminal furBloodAmount
  is applied directly. Both are recorded in `docs/selfchecks/limb-presentation-selfcheck.md`.

Tests: `LimbStateSyncTests` (wire + star relay + saved merge), `LyingPoseTests`,
`SwingReplayTests`, `LimbStatePatchTests` (reflective patch/formula surface), extended
`EnemyTerminalStateApplierTests` / `EntityStateRoundtripTests` / `DirectionTests`. 932 tests green.
See `docs/selfchecks/limb-presentation-selfcheck.md`.


## 30. Character action sounds — one CharacterSound event + native block/building sound paths (ProtocolVersion 17)

The broad "character sound / block sound sync" item is split into three precise mechanisms:

- **Attack / throw / exert sounds are dedicated events.** `SoundPlayPatch` captures the EXACT
  clip from the real string `Sound.Play` call inside the `CharacterAttack` / `CharacterThrow` /
  `CharacterExert` call-identity scopes (`Body.Attack`, `Body.ThrowItem`,
  `Body.TryExertSound`). `CharacterSoundMsg` (NetMsg 94, bidirectional star relay like
  LimbStateEvent/Speech) carries owner + kind + clip + position + volume + follow/2D mode, and
  `CharacterSoundSync` replays it on the owner's render clone under `RemoteApply`.
- **Block hit/break sounds already ride the native apply** — every `BlockDamaged` receiver
  applies through `WorldGeneration.DamageBlock(hitSound: true)`, which plays the game's own
  block hit/break sounds; adding an event would double-play them. This half is closed by
  evidence, not by new code.
- **Building-entity hit sounds ride the existing `BuildingEntityDamaged` message** — the
  receiver plays the local entity's own `hitSound` when it applies the damage (one operation =
  one message; no asset path on the wire).

- **Capture is call-identity, not guessing** — the innermost `DamageBlockOrigin` scope excludes
  block sounds during an attack, the `RemoteApply` scope excludes replays, and the pure
  `CharacterSoundPolicy` maps scope + clip to the kind.
- **ProtocolVersion 16→17** — a v16 peer would silently miss remote action sounds;
  mixed-version sessions are refused by policy.

Tests: `CharacterSoundSyncTests` (wire + star relay), `CharacterSoundPolicyTests`,
`CharacterSoundPatchTests` (reflective patch/capture surface), the `DirectionTests`
completeness guard, and the automatic patch-contract cover for the new `TryExertSound` patch.
947 tests green. See `docs/selfchecks/character-sound-selfcheck.md`.

## 31. Weapon-fire direction + recoil — no new message, gunangle kick rides CharacterSound (ProtocolVersion 18)

`#193` split into two findings:

- **Direction was already synced.** `Body.HandleVisuals` computes the arm
  `gunangle` from `(targetLookPos - limbs[1].position)` (Body.cs:3271), and
  `SessionStatePump` writes the peer's 20 Hz `LookPos` into every render clone's
  `targetLookPos`. The only local-mouse item orientation calls are the three
  hand-slot lights fixed by #119 (`CustomItemBehaviour.cs:439/512/526`), so the
  gun render path never reads the local mouse. No new sync bit needed.
- **Recoil was missing and is now a dedicated trigger event.** `GunScript.Fire`
  adds `knockBack * 8` to the OWNER's `armsAnimator.gunangle` (GunScript.cs:221);
  a render clone never runs that path. `GunFirePatch` (Postfix on `GunScript.Fire`)
  reports a new `CharacterSoundKind.GunFire` on the existing `CharacterSound` event
  (NetMsg 94), carrying the exact fire-sound clip name + `knockBack * 8` as the new
  `CharacterSoundMsg.RecoilDegrees` field. The receiver plays the sound and adds the
  same kick to the owner's clone arms animator; `Body.HandleVisuals` then lerps it
  back to the synced aim — the same transient the owner sees.

- **Wire shape**: no new NetMsg id, no direction-table change — the existing
  bidirectional star relay already fans the event out. ProtocolVersion 17→18
  because a v17 peer would not understand the new enum value/field.
- **One event = one message**: the fire sound + recoil travel together as one
  reliable presentation event; the snapshot stream is untouched.

Tests: extended `CharacterSoundSyncTests` (GunFire wire round-trip with recoil),
new `GunFirePatchTests` (patch surface + PatchInventory contract + protocol field),
automatic patch-contract cover for the `GunScript.Fire` postfix. See
`docs/selfchecks/weapon-fire-recoil-selfcheck.md`.

## 32. Periodic keyframe self-heals world-item top-level state (no protocol change)

The 5 s periodic item snapshot already carries the host table's full
`CharacterItemMsg` (condition/favourited/liquids/components/contents), but the
reconcile applied only condition to existing world items. A dropped use/craft
report or a missed correction therefore left component/liquid state stale until
the item was picked up or corrected again. The keyframe reconcile now captures
the local top-level digest and, when `ItemStateEquality` reports divergence,
restores condition/favourited/liquids/components from the snapshot:

- **Position stays owned by the position stream** — the keyframe still never
  places or re-positions; it only aligns state.
- **Container contents stay on the content/container message family** — the
  top-level equality deliberately ignores `Contents`; this is a top-level
  self-heal, not a recursive reconcile.
- **The comparison rules are shared** — the evidence-check tolerance logic was
  extracted from `ItemArbitration` into pure `ItemStateEquality`, so
  arbitration and keyframe reconcile cannot drift apart.
- **No protocol bump** — the wire formats are untouched; ProtocolVersion stays.

Tests: new `ItemStateEqualityTests` (pure tolerance/field rules) and
`PeriodicSnapshot_CarriesTopLevelComponentAndLiquidState` (wire-level keyframe
evidence). 1018 tests green. See `docs/selfchecks/item-keyframe-state-selfcheck.md`.

## 33. Cross-player carry/release (ProtocolVersion 27)

The direct-player-interaction family's carry half lands with the same
host-authoritative shape as the take slice, but the movement model stays inside
the existing local-compute boundary:

- **Host is only the carry-relation authority.** `PlayerInteractionService`
  validates the target (unconscious/dead), the carrier (conscious/alive), the
  one-carrier/one-carried rule, records the relation and broadcasts
  `PlayerCarryStateMsg` (NetMsg 101). The host never simulates the carried
  body's movement.
- **The carried client is the mover.** On receiving the state, the carried
  player's GameAdapter adds `CarriedBodyDriver` to its local body; BodyPatches
  then skips that body's simulation (same render-proxy path as
  `RemoteBodyDriver`) and the adapter moves the transform to an offset on the
  carrier's back from the carrier's entity buffer every frame.
- **No second movement channel.** The carried body reports its new position
  through the ordinary 20 Hz entity stream (and 1 Hz character stream), so all
  peers already see the carried-on-back result without a carry-specific render
  network. Late joiners need no carry snapshot for the same reason.
- **Wire**: `PlayerCarryStartRequestMsg` (99, guest→host),
  `PlayerCarryStopRequestMsg` (100, guest→host), `PlayerCarryStateMsg` (101,
  host→all). ProtocolVersion 26→27 because v26 peers would not understand the
  new messages.
- **Accepted boundaries**: one carrier/one carried, no piggyback stack, no
  distance/line-of-sight validation, no heal in this slice.

Tests: `PlayerInteractionServiceTests` carry family (start/stop/refusal/mirror)
and `DirectionTests` rows for the three new IDs. 1053 tests green. See
`docs/selfchecks/carry-interaction-selfcheck.md`.

## 34. Cross-player heal (ProtocolVersion 28)

The direct-player-interaction family's final slice uses the same
host-authoritative shape as take and carry:

- **Host validates and owns the healing operation.** `PlayerInteractionService`
  checks both are in-world and have snapshots, the healer is conscious/alive,
  the target is alive, and the requested item (or an auto-selected one) is a
  known heal profile. It refuses dead targets (no CPR) and non-medical items.
- **Host computes the effect as pure data.** `RemoteHealProfile` /
  `RemoteHealApplication` define the supported dressing/medicine items and apply
  the effect to the target's most injured limb (dismembered limbs skipped). The
  Runtime has no game-assembly dependency in this logic.
- **Item consumption is authoritative.** The host reduces the item's condition
  by the profile cost and destroys it at zero; the guest transfer table is
  updated/removed in the same round so a reconnect restore cannot resurrect the
  consumed amount.
- **One result message to both participants.** `PlayerHealResultMsg` (NetMsg
  103) carries the item consumption state and the target's full post-heal
  health/limb state; the GameAdapter applies the local half inside RemoteApply
  and immediately re-reports the character snapshot. The heal result is not a
  second health channel — it rides the existing 1 Hz character data as the
  convergence path.
- **Wire**: `PlayerHealRequestMsg` (102, guest→host),
  `PlayerHealResultMsg` (103, host→participants). ProtocolVersion 27→28.
- **Accepted boundaries**: auto-select first item from the Online UI; no CPR /
  dead-target healing; only the dressing/medicine profile set; no
  distance/line-of-sight validation.

Tests: `PlayerInteractionServiceTests` heal family, `RemoteHealApplicationTests`
pure profile/limb tests and `DirectionTests` rows for the two new IDs.
1066 tests green. See `docs/selfchecks/heal-interaction-selfcheck.md`.

## 35. GameAdapter construction readability split (no protocol change)

#122 re-evaluated: the pre-migration idea of collapsing the hand-wired fields
into a DI service was rejected before — the `new`s are state-belongs-to-its-owner,
not DI services, and the domain logic has already sunk out of the coordinator.
The only remaining actionable form is a **readability grouping of the
construction block**, which is now landed without changing that architecture:

- The coordinator file (`GameAdapter.cs`) owns only lifecycle/session wiring and
  the thin `IPatchBridge` forwards.
- A new `GameAdapter.Construction.cs` partial owns the adapter's state fields and
  the constructor dependency wiring.
- No factory, no late wiring, no DI collapse: every `readonly` field is still
  assigned directly in the constructor, and every domain still owns its own
  state. Existing per-domain partial fields (`_characterSoundSync`,
  `_heaterCookSync`, `_worldTimeSync`) remain in their domain partials.
- No protocol/wire change; `GameAdapter.cs` 472 lines,
  `GameAdapter.Construction.cs` 155 lines.

See `docs/selfchecks/gameadapter-construction-selfcheck.md`; 1066 tests green.

## 36. Tutorial-claw 20 Hz presentation stream (ProtocolVersion 29)

The backpack "claw 20 Hz flow todo" is now a stream, not a full tutorial-course
sync. Per-side tutorial course state and per-player claw-created props remain by
design (decision #28); the missing piece was the host's continuous claw *visual*
flow — the world-space rig that otherwise appears only on each side's own
simulation.

- **Host authority**: the host's `TutorialHandler` remains the only live rig.
  The Game Adapter captures `handPos` / `handPosCurrent` / arm material each
  frame and publishes to `TutorialClawService`.
- **20 Hz fan-out**: `TutorialClawService` broadcasts the latest snapshot at
  the configured state-stream cadence (default 20 Hz, unreliable, seq-gated)
  to in-world handshaken guests. The stream is absolute, so a late joiner
  receives the current claw pose within one cadence tick; no separate
  join snapshot is needed.
- **Guest apply**: `TutorialClawSync` applies the streamed state to the local
  `TutorialHandler` when the guest is not running its own course (a local
  course keeps ownership of its claw). `TutorialClawRemoteDriver` overwrites
  the claw-arm material in `LateUpdate` after the game's own `Update`.
- **Wire**: `TutorialClawStateMsg` (104, host→guest, unreliable). ProtocolVersion
  28→29 because a v28 peer does not render the remote claw flow.
- **No course/prop change**: the double-give marker family, the per-player
  pickup flow and the accepted late-joiner boundary are untouched. This is the
  deliberate "claw 20 Hz flow" slice; a later host-authoritative course-domain
  pass would be a separate decision.

Tests: `TutorialClawStreamTests` (wire round-trip, cadence, in-world gate,
seq gate, clear semantics) + the updated `DirectionTests`
host→guest row. 1072 tests green. See
`docs/selfchecks/tutorial-claw-stream-selfcheck.md`.

## 37. Mod-state saves (no protocol bump)

The first Mod-API save surface lands as a host-persistent, per-mod opaque
key/value store — not as a synced wire channel. The host is the only save
authority (architecture.md §8), so the store is host-write-only; a guest copy
gets `CanWrite = false` and coordinates through `IModNetwork`/`IModCommands`.

- **API**: `IModContext.State` / `IModState` — `TryGet`, `TrySet`,
  `TryRemove`, `TryClear`, `TrySetSchemaVersion`, `SchemaVersion`, `CanWrite`,
  `Keys`, `Count`. Payloads are opaque byte arrays; the framework never
  interprets mod bytes, so the mod owns its own schema/migration.
- **Permission**: writes require the host role + `ModPermission.WriteGameState`
  (the first live enforcement point for that flag). Reads are host-only too;
  the state table is not synced to guests.
- **Persistence**: `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin`
  (versioned protobuf wrapper, atomic temp+replace, same degrade-to-empty
  contract as `CharacterDataFileStore`). It loads once at `ModService.Initialize`
  (before discovery/Bind) and persists on each mutation.
- **Metadata/rails**: file entries carry mod id, mod version and schema version;
  missing mods keep their entries untouched. Safety rails: key ≤128 chars,
  ≤1024 keys per mod, value ≤64 KiB.
- **No wire change**: this is host-local persistence, so ProtocolVersion stays 29.
  Custom entities remain the un-landed Mod API surface; the local mod UI
  surface and content registration are now landed (see below).

Tests: `ModStateTests` (host write/read/remove/clear, guest refusal,
permission refusal, copy semantics, process persistence, corrupt-file
degradation). 1079 tests green after this slice. See
`docs/selfchecks/mod-state-saves-selfcheck.md`.

## 30. Mod UI — local immediate-mode windows (no protocol bump)

- **API**: `IModContext.Ui` / `IModUi` — per-mod local window registry.
  `Register(id, title, draw)` stores an immediate-mode callback;
  `Unregister`/`IsRegistered`/`WindowIds` manage it. The draw callback
  receives `IModUiWindow` (Label/Button/TextField/Separator), never Unity types.
- **Local-only**: UI windows cannot touch network/session/game state by
  themselves, so no permission is required and every `NetworkMode` may use it.
  Shared state still flows through `IModNetwork`/`IModCommands`; the window is
  a projection.
- **Wiring**: `ModService` owns the per-mod registry and exposes
  `IModUiControl.Windows` to the plugin. `CasualtiesUnknownOnline.Plugin`
  draws each window through `GUI.Window` + `GUILayout` (`ModUiDrawing`/
  `ModUiRenderer`); a throwing draw callback shows an inline error and is
  logged, never breaking the frame.
- **Rules**: empty id/title or null draw refused; duplicate id per mod refused;
  unregister removes. Mods persist their own UI state in the mod instance.
- **No wire change**: local presentation only, ProtocolVersion stays 29.
- Tests: `ModUiTests` (register/validate/unregister, plugin-facing control
  list, draw callback call sequence through a recording fake). 1085 tests
  green after this slice. See `docs/selfchecks/mod-ui-selfcheck.md`.

## 38. Mod content registration (no protocol bump)

- **API**: `IModContext.Content` / `IModContent` — a per-mod registry of
  opaque content definitions (`TryRegister(id, kind, data)` / `TryUnregister` /
  `IsRegistered` / `Definitions` / `Count`). `CanRegister` tells a mod whether
  it declared `ModPermission.RegisterContent`; every registration also checks
  the permission.
- **Content is static, not synced**: the framework stores definitions as
  opaque bytes and never interprets them. Content is part of the mod, so the
  Mod API handshake (id / SemVer / permissions / mode) is the consistency
  boundary — no content bytes cross the wire. A mod needing client-specific
  dynamic content coordinates through `IModNetwork`/`IModCommands`.
- **Permission**: `RegisterContent` gets its first live enforcement point.
  The permission policy already rejects that flag on `ClientOnly`/`Cosmetic`;
  only state-bearing modes may register content.
- **Safety rails**: id ≤128 chars, kind ≤64 chars, payload ≤64 KiB, ≤1024
  definitions per mod. Invalid/duplicate/over-cap entries are refused with a
  log, never silently truncated.
- **Framework read view**: `ModService` implements `IModContentControl`
  (`Entries` — mod id + definition snapshots) so the plugin and future
  native-content consumers can enumerate registered content without reaching
  into mod internals.
- **No wire change**: local registry only, ProtocolVersion stays 29.
- Tests: `ModContentTests` (Bind registration, permission refusal, invalid/
  duplicate/over-cap refusal, unregister, defensive copies, control-surface
  aggregation, policy caps). 1093 tests green after this slice. See
  `docs/selfchecks/mod-content-registration-selfcheck.md`.

## 39. Whole-protocol network traffic monitor (no protocol bump)

- **Scope**: `PacketSender` / `PacketReceiver` are the single data-plane
  boundaries, so the monitor observes every actual transport frame there —
  one record per recipient (not one per logical fan-out), including failed
  sends with the transport verdict.
- **Design**: a pure `NetworkTrafficTracker` (per-`NetMsg` send/receive byte
  counts, per-peer totals, failed send counts/bytes) + `NetworkTrafficMonitor`
  (`ICuoService`, 10-second rolling window, periodic `[NetworkTraffic]` log).
  `ItemTrafficTracker` remains the item-domain logical-operation counter.
- **Observability-only**: no batching, no rate-limit, no bandwidth decision is
  made from these numbers yet; `SteamSendFailureClassifier` still owns the
  transport-level failure-family log.
- **No wire change**: no new `NetMsg`, `ProtocolVersion` stays 29.
- Tests: `NetworkTrafficTrackerTests` + `PacketTrafficMonitorTests`
  (aggregation, failed sends, roll/reset, real ping/pong round trip, fan-out
  per-recipient). 1124 tests green after this slice. See
  `docs/selfchecks/network-traffic-monitor-selfcheck.md`.

## 40. Dynamite detonation sync — dedicated player-item explosion event (ProtocolVersion 30)

The last known gameplay-affecting item gap (`CustomItemBehaviour.data`'s
dynamite fuse flag) is closed as a one-shot explosion event, not by trying to
sync the unsupported `object[]` payload:

- **Native detonation stays native.** The dynamite use action
  (`Item.cs:6671-6682`) schedules `CustomItemBehaviour.DynamiteExplode`
  (`CustomItemBehaviour.cs:563-572`). `DynamiteExplodePatch` is a postfix that
  only reports the verified detonation (item id + world position) after the
  game's own explosion ran.
- **One event = one message.** `DynamiteExplosionMsg` (NetMsg 105,
  bidirectional) carries the destroyed item's `ItemInstanceId` (the one-shot
  identity) and the detonation position.
- **Host applies and relays.** `DynamiteExplosionSync.OnRemote` on the host runs
  `WorldGeneration.CreateExplosion(dynamite params)` inside `RemoteApply` — the
  host's real body/world items receive the effect without re-reporting the
  block/building damage that the trigger side's native explosion already
  synced through the existing channels — then broadcasts to the other guests
  (source excluded).
- **Guests replay, never re-explode.** The receiving guest uses
  `TrapVisualReplay.ReplayExplosion` (the shared trap-explosion visual/body
  segment), which never calls `CreateExplosion`; terrain/buildings were already
  aligned by the trigger side's block/building reports.
- **Duplicate suppression.** A session-scoped `HashSet<ulong>` of seen item ids
  drops a reliable-channel retransmission on the host and on replaying guests
  (an item can detonate at most once).
- **ProtocolVersion 29→30** because a v29 peer would not understand NetMsg 105.
- **Accepted residual (superseded by #53)**: the 5-second lit-fuse visual
  (child sprite + fuse audio) on remote clones was initially left local-only —
  short-lived presentation, not persistent state (same family as the
  crystal-unstable pre-explosion ticking).
- **Existing channels unchanged**: terrain craters, building damage, item
  condition/velocity destruction still ride `BlockPlaced`/`BlockDamaged`/
  `BuildingEntityDamaged`/item lifecycle/keyframe channels; this event adds the
  missing body/visual replay and authority apply.

Tests: `DynamiteExplosionSimulationTests` (guest report → host apply + relay
source-excluded; host broadcast to both guests), `NetPacketTests`
round-trip, `DirectionTests` bidirectional row, and the automatic
`PatchInventory` contract for `CustomItemBehaviour.DynamiteExplode`. 1128 tests
green. See `docs/selfchecks/dynamite-explosion-selfcheck.md`.

## 41. GrapplingHook presentation sync and clone owner-local script isolation (no protocol bump)

The remaining native-content known state gaps were closed without a new wire
message:

- **GrapplingHook state rides the existing item component digest.**
  `ItemStateCodec.MultiplayerStateFields` explicitly declares
  `fired`/`hookLatched`/`pulling` (private bools on the game's
  `GrapplingHook`, `GrapplingHook.cs:114-120`, no `[Saveable]`), so every
  existing item capture/report/snapshot path carries them and the clone
  renderer can present the owner's fired/normal sprite.
- **Clone display is safe and owner-local.** The new
  `RemoteItemPresentation` (display-domain helper) disables the original
  `GrapplingHook`/`WatchScript`/`AutoPump` scripts on render clones — those
  scripts read the local body or expect a live hook object and must not run on
  a display proxy — and applies the grapple sprite from the wire state. The
  rope/hook projectile remains a documented local projection (no hook
  transform is carried).
- **WatchScript/AutoPump are owner-local by design, not sync gaps.** Their
  timers/worn flag only drive the owning player's UI/body; remote clone copies
  are disabled. This closes the backlog's low-risk local-only entries as
  explicit exclusions instead of piecemeal state sync.
- **Peer-view renderer gained an L0 test face.** The pure
  `RemoteItemPresentation.IsGrapplingHookFired` decision is unit-tested;
  `GrapplingHookComponentSyncContractTests` guards the codec table and the
  game field shapes against a future game update.
- **No protocol change.** `ComponentStateMsg` already carries name/kind/value,
  so ProtocolVersion stays 30.

Tests: `RemoteItemPresentationTests` (3 cases), `GrapplingHookComponentSyncContractTests`
(2 cases), full suite 1133 green. See
`docs/selfchecks/grappling-hook-presentation-selfcheck.md`.

## 42. Remote player container content view — recursive Online UI projection (no protocol bump)

The remaining "open another player's inventory/container" backlog entry is
closed on the **view** side. The 1 Hz `CharacterDataMsg.Items` already carries
each container item's recursive `Contents` (the same data `CloneFactTable` and
`ItemStateCodec` use to materialize clones), so no wire change was needed.

- **`RemoteInventoryEntry` is now recursive.** It carries
  `IReadOnlyList<RemoteInventoryEntry> Contents`; `ContentsCount` stays as a
  derived convenience for the compact top-level line.
- **`RemoteInventorySnapshot` projects the tree.** `From` recursively maps
  `CharacterItemMsg.Contents`; `ToDisplayLines` emits indented `↳` child rows
  beneath each container parent so the Online UI can show what is inside a
  remote player's carried container.
- **`OnlineUiOverlay` renders the nested rows.** A recursive
  `DrawContainerContents` helper keeps the IMGUI member list readable; nested
  items are display-only (the existing Take operation remains top-level slot
  items only).
- **No protocol change.** `ProtocolVersion` stays 30; no new `NetMsg`.
- **Tests:** `RemoteInventoryServiceTests` gained recursive projection and
  display-formatting coverage (9 tests in that class); full suite 1134 green.
  See `docs/selfchecks/remote-container-content-view-selfcheck.md`.


## 43. Heal item selector — explicit Online UI medical item picker (no protocol bump)

The last "Online UI / interaction refinements" backlog item is closed. The
wire already supported a concrete `PlayerHealRequestMsg.ItemInstanceId`; the
UI simply always sent 0 for host auto-select. This cycle exposes the local
slot-held heal items and lets the user pick one.

- **`IGameAdapter.GetLocalHealItems()`** returns read-only `LocalHealItem`
  records (`InstanceId`, `ItemId`). The adapter scans inventory slots only,
  matching the host's `FindHealItemIndex` rule that skips worn items
  (`SlotIndex < 0`).
- **`OnlineUiOverlay` renders one `Heal <item>` button per local heal item**
  under the member row. The existing auto Heal button remains and still sends
  instance id 0.
- **`Plugin.TryHealWithItemFromUi`** forwards the chosen instance id through
  the existing `SendHealRequest`; the host re-validates authority from its
  character snapshots.
- **No protocol change.** `ProtocolVersion` stays 30; no new `NetMsg`.
- **Tests:** existing `PlayerInteractionServiceTests` already cover explicit
  ids on the host path; full suite 1134 green. See
  `docs/selfchecks/heal-item-selection-selfcheck.md`.

## 44. LookTarget gaze/scare — remote clone presentation via the player entity stream (ProtocolVersion 31)

The last recorded enemy-presentation local gap (`LookTarget` gaze/scare) is
closed on the **player clone** side. The enemy's `LookTarget` component still
runs natively on the local body (LookTarget.cs:12-16); the missing piece was
that a peer's render clone never received that transient gaze/face state.

- **Wire**: `EntityStateMsg` gains `LookOverridePos` (nullable `NetVector2Msg`,
  null = no override), `LookOverrideTime`, `EyeScareTime`, `EyePanicTime` and
  `EyeCloseTime` (ProtoMember 8-12). They ride the existing 20 Hz
  `PlayerState` / `PlayerStateReport` stream, so the remote clone is refreshed
  every stream tick.
- **Capture**: `RunCoordinator.PublishBodyState` sends
  `body.overrideLookTime > 0 ? body.overrideLookPos : null`,
  `body.overrideLookTime`, `body.eyeScareTime`, `body.eyePanicTime` and
  `body.eyeCloseTime` alongside the existing mouse `targetLookPos` — both are
  preserved, so weapon aim and head gaze stay distinct.
- **Apply**: `SessionStatePump` writes the override target/timer and the three
  face timers onto the proxy Body. `Body.HandleVisuals` then uses
  `overrideLookTime > 0` to turn the head/eyes toward the override point
  (Body.cs:3178), and `FacialExpression` reads `eyeScareTime`/`eyePanicTime`/
  `eyeCloseTime` for the scared/panic/closed eyes face (FacialExpression.cs:37-52).
- **Owner-local scripts unchanged**: `LookTarget` is not patched; it continues
  to drive only the local player's body on each side. No new `NetMsg`, no
  direction-table change.
- **ProtocolVersion 30→31** because a v30 peer would not send/render the new
  gaze/face fields.
- **Resolved residual**: the `Heater` temperature field on the `xaloris`
  prefab is **excluded by design** — a local-body effect (`Heater.OnWillRenderObject`
  writes only the local player's body temperature, which already rides the
  1 Hz character stream), not a sync gap. The rope/hook projectile and the
  5-second fused dynamite visual remain short-lived local presentation.

Tests: `EntityStateRoundtripTests` gained gaze/eye-scare round-trip and
defaults coverage (2 tests), `NetPacketTests` gained the wire round-trip for
the new fields (1 test); full suite 1137 green. See
`docs/selfchecks/looktarget-gaze-sync-selfcheck.md`.

## 45. Network health metrics — RTT history / jitter / probe loss (no protocol bump)

The backlog's "Network health metrics" item is closed: the missing
health-specific counts are now measured and logged, without changing
transport/protocol/bandwidth behaviour.

- **Pure peer-health state.** `PeerHealthTracker` owns per-peer rolling RTT
  samples (max 16), average RTT, jitter (absolute difference of the last two
  samples), completed/lost probe counters and the derived loss percentage.
- **Probe matching by tick stamp.** `SessionService.RequestPing` records the
  sent ping's original `UtcNowTicks` with the probe; `RecordPong` closes only
  the probe whose stamp matches. A late pong from an already-lost probe can
  never be mistaken for the current outstanding probe, and a duplicate pong
  cannot double-count a sample.
- **Observability only.** `NetworkTrafficMonitor` now owns the tracker and logs
  `[NetworkHealth] peer=... rtt=... avg=... jitter=... loss=...` on the same
  10 s window edge as `[NetworkTraffic]`; per-peer bandwidth remains in the
  existing `[NetworkTraffic]` log. No UI or gameplay path consumes these
  numbers yet.
- **Session boundary.** `TeardownSession` resets the traffic + health
  monitor so diagnostics from one lobby never leak into the next.
- **No wire change.** `ProtocolVersion` stays 31; no `NetMsg`, no
  direction-table change.

Tests: `PeerHealthTrackerTests` (5 pure-unit tests) and
`PacketTrafficMonitorTests.RequestPing_RecordsPeerHealthSnapshot`
(production stack integration); full suite **1143 green** (x64 vstest). See
`docs/selfchecks/network-health-metrics-selfcheck.md`.

## 46. Mod ReadGameState — read-only player character projection (no protocol bump)

The backlog's Phase 4 `ReadGameState` item is closed with a first read-only
game-state projection: the mod-facing `IModGameState` surface exposes the
same session-scoped remote player character facts the Online UI already
consumes, without exposing Unity or game-assembly types.

- **Permission is enforced at the read surface.**
  `IModContext.GameState` is available to every mod, but `CanRead` only
  reports true when the mod declared `ModPermission.ReadGameState`, and every
  `TryGetPlayer` call re-checks it (refused + logged otherwise). The permission
  was already declared/validated/carried by the handshake; this round gives it
  its first live enforcement point.
- **Projection, not a second data path.** `ModGameStateAdapter` reads the
  existing `RemoteVitalsService` and `RemoteInventoryService`, so a mod sees
  the same 1 Hz character-stream facts as the built-in UI. Vitals and
  inventory are the two halves; a missing half is simply null until its
  snapshot arrives.
- **Immutable DTOs in Abstractions.** `IModPlayerState`, `IModPlayerVitals`,
  `IModPlayerInventory` and `IModInventoryEntry` are read-only interfaces over
  private runtime records. Container contents are projected recursively, and
  the returned objects are copies (no live game object escapes).
- **Presence is included.** `IModPlayerState.InWorld` comes from the session's
  local/remote in-world state, so a mod can tell whether a player is actually
  in the world rather than only in the lobby.
- **Session-scoped lifecycle.** A remote leaving the world or a session end
  clears the underlying caches, so the mod surface can never serve a stale
  player from a previous run.
- **No wire change.** The data already arrives on the 1 Hz character stream;
  `ProtocolVersion` stays 31, no `NetMsg`, no direction-table change.

Tests: `ModGameStateTests` (5 integration tests over the production stack:
permission refusal, host guest-report projection, guest host-snapshot
projection, no-snapshot false, leave-world clear) and
`TestReadGameStateMod` (the declared-permission test mod); full suite **1148
green**. See `docs/selfchecks/mod-game-state-selfcheck.md`.

## 47. Remote clone FacialExpression disfigurement/eye-loss presentation (ProtocolVersion 32)

The limb-presentation cycle's recorded residual — the remote clone's
body-level `FacialExpression` latches (`Disfigured`, `EyeGone`,
`BothEyesGone`, the owner's random `disfiguredIndex`) remained template-driven
— is closed on the clone side.

- **Wire**: `CharacterHealthMsg` gains `DisfiguredIndex` (int),
  `DisfiguredTimeFullSkin` (float) and `EyeTimeHealed` (float)
  (ProtoMember 65-67). The three Body booleans already rode the 1 Hz
  character snapshot; the new fields carry the FacialExpression presentation
  state that Mapster cannot read from `Body`.
- **Capture**: `CharacterDataSync.CaptureCharacterData` and
  `CaptureLimbStateEvent` now call `CloneFacePresentation.Capture(body, health)`,
  which reads `FacialExpression.disfiguredIndex` /
  `disfiguredTimeFullSkin` / `eyeTimeHealed` from the owner's body child and
  writes them into the health message after the normal `Body → CharacterHealthMsg`
  map.
- **Apply**: `RemotePlayerRenderer` calls `CloneFacePresentation.Apply(clone,
  health)` whenever a clone is created or its snapshot updates, so the render
  clone gets the body latches and the same disfigurement head index / heal
  progress. The clone's own `FacialExpression.Update` continues to run and uses
  the written fields exactly like the owner's.
- **Robustness**: `Apply` clamps the disfigurement index into the clone's
  `disfiguredHead` array (a malformed/old wire value cannot index outside the
  sprite array).
- **ProtocolVersion 31→32** because older peers cannot send/render the new
  face-latch presentation fields.
- **Tests**: `NetPacketTests.CharacterHealth_FaceLatchPresentation_RoundTrips`,
  `CharacterDataFileStoreTests` full-field round-trip assertions, and
  `CloneFacePresentationTests` reflective surface (Capture/Apply shape +
  static state-free helper); full suite **1151 green**.

See `docs/selfchecks/clone-face-presentation-selfcheck.md`.

## 48. Animal death presentation replay on remote kills (no protocol bump)

The last native-content presentation gap found in the creature domain was that
a remote death skipped the animal-specific death effects entirely: the remote
side's `BuildingEntityUpdatePatch` destroyed the entity before the game's
death branch could call `SendMessage("AnimalDeath")`, so peers of the attacker
saw only generic building-break particles/dust/rock sound.

- **Presentation-only replay.** New `AnimalDeathReplay.Replay(BuildingEntity)`
  covers the three known creature families:
  - `SpiderHandler` (incl. `SpiderHandlerTBE`): `gore` sound + `BloodExplosion`
    when `doDeathExplode` is true; the method OMITS the native
    `PlayerCamera.main.body.skills.AddExp` reward, which stays attacker-side.
  - `CrystalEnemy`: `crystalenemydeath` sound + `Utils.Create("Special/CrystalDistort")`
    with `CrystalDeathAnimation`.
  - `TraderScript`: `gore` + `BloodExplosion` at the trader's torso.
- **Live vs late-joiner distinction.** `RemoteEntityDeath` gains
  `ReplayAnimalDeath`; the live damage/open relay sets it true, the
  world-entry / 60 s health snapshot sets it false. The replay path therefore
  plays creature-specific effects only for deaths the current session actually
  observed arriving, not for pre-existing dead entities materialized from a
  snapshot.
- **No wire/protocol change.** The death fact already arrives through the
  existing building-damage/health channels; this is pure local presentation on
  the receiving side. `ProtocolVersion` stays 32.
- **Tests**: `AnimalDeathReplayPatchTests` (3 reflective rows: replay helper
  shape, marker flag shape, destruction-replay helper still present); full
  suite **1154 green** (was 1151).
- See `docs/selfchecks/animal-death-presentation-selfcheck.md`.

## 49. Mod entity spawn — permission-gated native prefab replication (no protocol bump)

The Phase 4 Mod API `SpawnEntity` permission gets its first live enforcement
point with a small, intentionally native-prefab-only spawn surface. The design
reuses the existing runtime entity channel instead of adding a parallel mod
entity wire/path:

- **Public surface**: `IModContext.EntitySpawn` / `IModEntitySpawn` in
  `CUO.Abstractions`. `CanSpawn` reflects the declared
  `ModPermission.SpawnEntity`; `TrySpawn(prefabId, x, y, rotation)` runs the
  full gate and forwards to the Runtime → Game Adapter boundary. A mod never
  sees Unity or game-assembly types.
- **Replication is not duplicated**: the Game Adapter creates the local
  `BuildingEntity` via `Utils.Create` and lets the normal
  `BuildingEntity.Start` report ride `EntitySpawnedMsg` (NetMsg 68). The host
  creates/relays, the guests create/replay — exactly the same path as a native
  runtime spawn (CaveTickSpawner, CrystalMimic, scripted spawns), including
  the existing creation-time data handling for geysers/keypads/crystal tint.
- **Gates**: permission + `SessionActive` + `LocalInWorld` + request-shape
  rails (`ModEntitySpawnPolicy`: valid prefab id, finite X/Y/rotation).
  Malformed calls are refused with a log before the adapter seam; an adapter
  rejection (unknown prefab / non-`BuildingEntity` prefab) is returned false
  and a non-entity local object is destroyed, never left unsynced.
- **Boundary recorded**: only native `BuildingEntity` prefabs are supported in
  this slice. It is a spawn/replication surface, not a generic custom
  component/state-injection mechanism; per-entity custom data still belongs to
  `IModNetwork` / `IModCommands` / `IModContent` coordination.
- **No wire change**: no new `NetMsg`, no direction-table row; `ProtocolVersion`
  stays 32. Mixed-version sessions remain compatible because the feature only
  calls an existing wire message.
- **Architecture**: `IModEntitySpawner` is the narrow Runtime → Game Adapter
  seam (registered in the plugin, disabled default in the Runtime/test
  composition); `DisabledModEntitySpawner` keeps the Runtime-only graph
  constructible without a game adapter.

Tests: `ModEntitySpawnTests` (6 tests: permission refusal, adapter delegation,
out-of-world refusal, malformed request refusal, adapter failure, policy
rails) + `FakeModEntitySpawner`; full suite **1160 green** (was 1154). See
`docs/selfchecks/mod-entity-spawn-selfcheck.md`.

## 50. AccessNativeApi — curated read-only native operation registry (no protocol bump)

The last open Phase 4 Mod API item was the `AccessNativeApi` permission flag:
declared and handshake-carried for a long time, but with no exposed surface and
no policy. This cycle closes it with a deliberately bounded design:

- **Policy decision**: AccessNativeApi is **not arbitrary reflection and not
  unrestricted game-assembly access**. It is a Game Adapter-curated operation
  registry: the Runtime exposes only named operation ids, and every id must be
  registered by the Game Adapter (the only layer allowed to know game-private
  types). The first slice is read-only; no write/native-mutation operation is
  registered until a concrete consumer exists and its sync/authority boundary
  is designed.
- **Public surface**: `IModContext.NativeApi` / `IModNativeApi` in
  `CUO.Abstractions`. `CanAccess` reflects `ModPermission.AccessNativeApi`;
  `CanInvoke` asks the provider registry; `TryInvoke` runs the full permission,
  operation-id, argument and result policy; `TryGetLocalPlayerState` is the
  typed convenience for the one registered operation.
- **Registered operation**: `local.player.state`
  (`ModNativeApiOperations.LocalPlayerState`) returns
  `IModNativeLocalPlayerState` — local body position, brain health, hunger,
  thirst, stamina, energy, temperature, consciousness, alive/conscious. The
  Game Adapter reads them directly from `Body` (Body.cs:3934-3965, 203/213)
  and never leaks the Unity object.
- **Safe value surface**: arguments/results are null, strings, numeric
  primitives, capped byte/primitive arrays, and framework DTOs
  (`IModNativeLocalPlayerState`). Any other object is refused before the seam
  (argument) or after it (result), logged, and never returned to a mod.
- **No wire change**: local read-only state only; no new `NetMsg`, no
  direction row, `ProtocolVersion` stays 32.
- **Architecture**: `IModNativeApiProvider` is the narrow Runtime → Game
  Adapter seam (registered in the plugin, disabled default in the Runtime/test
  composition); `DisabledModNativeApiProvider` keeps the Runtime-only graph
  constructible without a game adapter.

Tests: `ModNativeApiTests` (7 tests: permission refusal, provider delegation +
typed local state, unknown operation, malformed/unsafe-argument refusal,
unsafe-result refusal, argument cap, policy rails) +
`GameAdapterNativeApiContractTests` (3 reflective contract rows) +
`FakeModNativeApiProvider`; full suite **1170 green** (was 1160). See
`docs/selfchecks/mod-native-api-selfcheck.md`.

## 51. Gun state reports — persistent GunScript transitions ride the item-use fact path (no protocol bump)

The last recorded native-content narrative gap in `docs/item-features.md` was
"gun firing/racking has no reports": the `GunScript` persistent state
(`roundInChamber`, `roundsInMag`, `hasMag`, `safe`, `racked` and `condition`)
is already captured by the `[Saveable]` component digest, but the discrete
transitions only reached the host and peer clones on the next 1 Hz character
snapshot.

- **Domain owner**: new `GunStateSync` (Game Adapter/Items) owns the
  per-instance last-reported snapshot (`ConditionalWeakTable<GunScript, …>`)
  and reports only an actual persistent-state change through the existing
  `ItemUseSync.OnItemUsed` fact path. The Harmony patches are thin — they call
  `IPatchBridge.OnGunStateChanged` after `Fire`, `TryRack`, `ToggleSafety`,
  `LoadMag`, `UnloadMag` and `Update`; no cross-call state lives in a patch.
- **Why reuse `ItemUse`**: gun-state transitions are the same accept-with-
  correction shape as a use — the owner's local copy is the fact source; the
  host adopts the evidence unconditionally (`CheckUseEvidence`) and broadcasts
  the authoritative carried item to the other peers. No new wire message, no
  direction row, no `ProtocolVersion` bump.
- **Coverage**: user-facing fire/rack/safety/load/unload plus the
  Update-driven auto-rack/auto-unrack transitions. A remote render clone is
  excluded by the existing `RemoteBodyDriver` guard.
- **Deduplication**: `Fire` and `Update` both call the same report surface;
  the sync domain compares the persistent snapshot so one state change still
  produces exactly one item-use report.
- **Fallback unchanged**: the 1 Hz `CharacterDataMsg` remains the self-healing
  fallback if a report is lost; the dedicated report is the freshness layer,
  not a second authoritative channel.

Tests: `GunStatePatchTests` (2 reflective surface tests) plus the automatic
`PatchContractTests` resolution of the six new `[HarmonyPatch]` targets; full
suite **1172 green** (was 1170). See
`docs/selfchecks/gun-state-sync-selfcheck.md`.

## 52. Liquidcentrifuge cooldown — persistent `CustomItemBehaviour.data[0]` state (no protocol bump)

The last real gameplay state hidden in `CustomItemBehaviour.data` was the
liquidcentrifuge 60-second cooldown. The `object[]` payload itself is
unsupported by the generic `[Saveable]`-field codec, so the cooldown never
traveled with item state: a transferred or reconnect-restored centrifuge came
back with `data[0] = 0` and was immediately usable again, and peer clones could
not show the countdown sprite.

- **Explicit wire face, not a new wire message**: new `CustomItemDataState`
  (Game Adapter/Items) gives the cooldown a synthetic `cooldown` component
  field (kind float) inside the existing `CustomItemBehaviour` component
  digest. `ItemStateCodec` captures/restores it on every existing item-state
  path — carried sync, world correction, character snapshots, spawn/drop and
  reconnect restore. No new `NetMsg`, no direction row, `ProtocolVersion`
  stays 32.
- **Start lifecycle**: `CustomItemBehaviour.Start` initializes
  `data[0] = 0f` on every fresh prefab (CustomItemBehaviour.cs:9-17), after
  `ItemStateCodec.RestoreComponentStates` has applied a synced value. The
  restore path also adds a one-frame `LiquidCentrifugeCooldownRestore`
  marker, which reapplies the value from `Update` (after Start) and destroys
  itself.
- **Deterministic capture**: for a liquidcentrifuge, capture always emits the
  `cooldown` field (0 when the native Start has not run yet), so the wire face
  is stable regardless of capture timing.
- **Remaining payload entries stay non-synced by design**: jetpack throttle is
  a frame-level transient; dynamite detonation already rides
  `DynamiteExplosionMsg`, and its 5-second lit-fuse visual is now closed in #53
  (it rides the same synthetic component-field pattern).

Tests: `CustomItemDataStateTests` (9 L0 reflective tests: capture/restore
helper shape, default-zero capture, field matching, array creation/mutation,
game-field contract, marker contract); full suite **1181 green** (was 1172).
See `docs/selfchecks/custom-item-data-state-selfcheck.md`.

## 53. Dynamite lit-fuse presentation — synthetic fuse field rides item state (no protocol bump)

The last item-domain accepted presentation residual was the 5-second dynamite
lit-fuse visual/audio on remote clones. The native use action only enables a
child sprite and plays the item's AudioSource on the trigger side
(Item.cs:6678-6680); peers saw the dynamite unlit until the detonation message
arrived.

- **Explicit wire face, not a new wire message**: `CustomItemDataState` gains a
  synthetic `fuse` component field (bool) for `dynamite` inside the existing
  `CustomItemBehaviour` component digest, exactly like the liquidcentrifuge
  `cooldown` field. `ItemStateCodec` captures/restores it on every existing
  item-state path — carried sync, world correction, character snapshots,
  spawn/drop and reconnect restore. No new `NetMsg`, no direction row,
  `ProtocolVersion` stays 32.
- **Deterministic capture**: for a dynamite, capture always emits the `fuse`
  field (false when the native use action has not run), so the wire face is
  stable regardless of capture timing.
- **Clone/world presentation**: `RemoteItemPresentation.Apply` reads the
  synthetic field and enables the clone's child `SpriteRenderer` for the lit
  fuse; `ItemApplication.ApplyAuthoritativeState` calls the same
  `ApplyDynamiteFuse` for corrected world-item copies. A new
  `DynamiteFuseAudioReplay` marker plays the clone's fuse AudioSource once and
  persists for the fuse lifetime, so repeated 1 Hz snapshot refreshes never
  re-trigger the audio.
- **Detonation unchanged**: the existing `DynamiteExplosionMsg` (NetMsg 105)
  still carries the one-shot detonation; this slice only makes the preceding
  fuse presentation visible/audible on remote clones.
- **No protocol change**: synthetic component fields already ride
  `ComponentStateMsg`, so v32 peers stay compatible.

Tests: `CustomItemDataStateTests` gained 6 dynamite-fuse L0 reflective tests
(capture true/false/null, field matching, array creation/mutation, game-field
contract) and `RemoteItemPresentationTests` gained dynamite-fuse decision
tests plus the audio-marker contract row; full suite **1190 green** (was 1181).
See `docs/selfchecks/dynamite-fuse-presentation-selfcheck.md`.


## 54. WorldService / ItemService partial split (no protocol bump)

The architecture watchlist's two largest runtime cursors were at/near the
600-line gate, so they were split into message-flow partials before further
features land in them.

- **WorldService**: `WorldService.cs` keeps the world-defining state,
  constructor and start-gate lifecycle (600 → 233 lines). The new
  `WorldService.MessageFlow.cs` (385 lines) owns the block/building/world-state
  events, report/send/broadcast plumbing and the late-joiner block-difference
  snapshot. The existing `Channels.cs`, `BlockDamage.cs` and `SessionState.cs`
  partials are unchanged.
- **ItemService**: `ItemService.cs` keeps the constructor, events, position
  stream, carried facts, host-only snapshot/lifecycle and crafting seams plus
  interface forwards (597 → 309 lines). The new
  `ItemService.ReportReceive.cs` (306 lines) owns the report/receive message
  flow: spawn/drop/use/cook/destroy sends, wire receive events, block-drop
  registration, corrections and action/snapshot receive forwarding. The
  existing `PendingPickups.cs`, `PlayerInteraction.cs` and `Traffic.cs`
  partials are unchanged.
- **No behavior change**: methods were relocated, not rewritten; no new class,
  no new state bool, no DI change, no wire message, no protocol version bump.
- **Architecture**: partial type is the existing codebase pattern for
  cursor-level service decomposition; each new file remains one top-level type
  and under the 600-line gate.

Tests: existing full suite **1191 green**; build 0 warnings/0 errors;
architecture, event-replay and entity-event-dispatch gates all pass. See
`docs/selfchecks/world-item-service-partial-split-selfcheck.md`.


## 55. RadiationLine world-state sync (ProtocolVersion 33)

The original `RadiationLine` (public MonoBehaviour, `active` + private
`timeGone`) is world state, not per-player presentation. CUO previously
carried only the per-body `CharacterHealthMsg.RadiationSickness`; each side
ran its own layer-timer `Activate()` and its own `timeGone` advancement
(WorldGeneration.cs:859-863, RadiationLine.cs:Update), so late joiners and
guests whose layer clock or body consciousness diverged could see a different
radiation boundary.

- **Host authority**: a new host→guest `RadiationLineStateMsg` (NetMsg 106)
  carries `Active` + `TimeGone`. The host publishes the absolute state while
  the line is active (5 Hz idempotent self-heal; the line moves at most
  ~1.5 units/s, so the wire cost is tiny) and stores the current state for the
  world-entry/reconnect fan-out (`HandlerContext.SendWorldStateToMember`).
  Guest local activation is suppressed in `WorldGenerationUpdatePatch`
  (`layerTimeSpent` capped at `maxTimePerLayer` — the only consumer of that
  field is the line condition).
- **Guest side**: `RadiationLineSync.OnRadiationLineStateReceived` writes the
  host's absolute state onto the local `RadiationLine`. The guest still runs its
  own per-frame `RadiationLine.Update` between resends — that path drives the
  local player's radiation sickness / eye-scare / irradiation presentation —
  and re-aligns every 5 Hz. An inactive host state calls `Deactivate()`.
- **Solo→lobby**: `RadiationLineSync.Update` also snapshots the line state into
  `WorldService` while there is no active session, so a solo-turned-lobby host
  can immediately hand the current boundary to a joining/reconnecting guest
  without waiting for the first live broadcast frame.
- **ProtocolVersion 32→33** because a v32 peer would not understand NetMsg 106
  and would run its own local line.

Tests: `NetPacketTests.RadiationLineState_RoundTripsActiveAndTimeGone`,
`WorldEventRelayTests.RadiationLineState_HostBroadcast_ReachesEveryGuest`,
`WorldEntrySnapshotTests.MemberEntersWorld_ReceivesCurrentRadiationLineState`,
direction-table row + game-field contract row; full suite **1195 green**,
build 0 warnings/0 errors. See
`docs/selfchecks/radiation-line-state-sync-selfcheck.md`.


## 56. CrystalTeleport sync — repeatable teleport-laugh/flash event (ProtocolVersion 34)

The original `CrystalTeleport` (internal, extends `CrystalEffect`) teleports
the touching player's body to a random ground point and plays a 2D
`observerlaugh` + `FlashBrief` (CrystalTeleport.cs:14-38). It has no latch and
is repeatable. CUO previously had no entity-feature row and no dedicated
event: the body's new position/stats already ride the 20 Hz player stream, but
peers never heard/saw the laugh/flash — the remote player simply blinked away.

- **`CrystalTeleportTriggered` (EntityEventKind 33, repeatable)** — reported by a
  dynamic prefix/postfix pair on the internal `CrystalTeleport.Touched`. The
  prefix captures the touching body's position; the postfix reports only when
  the body actually moved (the method can silently return after a failed
  1000-point ground search), so no false event is emitted.
- **Replay** — `CrystalStateActions.ApplyCrystalTeleport` plays the same
  trigger-side calls (`observerlaugh` 2D with the original flags, then
  `FlashBrief`). Both the host executor (`TrapEffectApplier`) and the guest
  replay (`TrapVisualReplay`) route through it.
- **Body state intentionally not part of the event** — each player simulates
  their own body; the teleporting body already moved locally and the existing
  20 Hz player entity stream carries position/consciousness/shock/velocity to
  every peer. The event only carries the shared presentation.
- **Repeatable / no one-shot snapshot** — no crystal latch exists, so a late
  joiner must not replay an old laugh/flash. The event is classified
  repeatable in `EntityEventArchives`/`EntityEventProfiles`.
- **ProtocolVersion 33→34** because a v33 peer would receive the new enum value
  on the existing entity event message and silently drop the presentation.

Tests: archive/profile completeness, the combinatorial entity-event suite
automatically runs the new repeatable kind, a dedicated
`CrystalTeleportTriggered_RepeatableEvent_NotInLateJoinerSnapshot`,
`PatchContractTests.CrystalTeleportPatchSet_IsComplete` and the dynamic
contract count 8→9; full suite **1200 green**, build 0 warnings/0 errors, all
repo gates pass. See
`docs/selfchecks/crystal-teleport-sync-selfcheck.md`.


## 57. Owner-local body auto-events — clone suppression (no protocol change)

The exploration audit (2026-08-23) flagged Vomiter, SelfHarmer, PantSound,
MoodChangeSounds and SleepingBagUse as not part of the clone presentation
contract. Static review confirmed the first three are mounted on the Body
object (Body.cs:1074/1077/3434) and run in their OWN `Update` methods, which
the render-proxy `Body.Update`/`Limb.Update` skips do not cover.
`MoodChangeSounds` and `SleepingBagUse` read `PlayerCamera.main.body` (the
local player), so a clone copy would not even be operating on the remote
owner's body.

- **Change**: `RemoteBodyFactory.CreateRemoteBody` now disables each of these
  component types on every render clone. This is adapter-local clone
  construction — no new wire message, no `ProtocolVersion` bump.
- **Why not a dedicated event**: these are owner-local body/UI/sound effects
  (vomit warnings, self-harm minigames, pant/pain/yawn loops, mood-change
  sounds). A future remote-presentation design would need a dedicated,
  explicit event channel; disabling the clone copies is the correct minimal
  boundary until one exists.
- **No wire/protocol change**: only `RemoteBodyFactory.cs` touched.

See `docs/selfchecks/owner-local-body-auto-events-selfcheck.md`.


## 58. RadiationLine straggler pressure — multiplayer activation rule (no protocol change)

The vanilla radiation line is a single-player timer: it activates when the
local `layerTimeSpent > maxTimePerLayer` (WorldGeneration.cs:859-863). In a
co-op session that timer only reflects the host's progress. The KrokMP-inspired
exploration (2026-08-23 §2.3) asked for the missing host-side rule: start the
line when players have reached the layer bottom and stragglers remain. The
world-state half (NetMsg 106) had already landed (#55); this entry completes
the rule.

- **Pure policy**: `RadiationStragglerPolicy.ShouldActivateLine`
  (`Runtime.Session.EntitySync`) — given the local + remote entity-stream
  players, activate when **at least one living player has reached the layer
  bottom and at least one other living player is still above it**. Dead and
  absent players are ignored.
- **Boundary**: `bottomY = -world.halfHeight + 3.1f` with a strict `<`,
  matching the game's own next-layer trigger (WorldGeneration.cs:979).
- **Integration**: `RadiationLineSync` now takes `EntitySyncService` and calls
  the policy on the host before publishing while the line is inactive. The
  vanilla layer timer remains untouched and acts as the fallback.
- **One-way activation**: the vanilla line stays active until layer
  regeneration, so the policy only ever calls `Activate()`; no new
  deactivation/inactivity semantics were introduced.
- **No wire/protocol change**: the activation is a local world mutation that
  the existing `RadiationLineState` (NetMsg 106) already broadcasts, so
  `ProtocolVersion` stays 34.

Tests: `RadiationStragglerPolicyTests` (8 cases), full suite **1208 green**,
build 0 warnings/0 errors, architecture/event-replay gates pass. See
`docs/selfchecks/radiation-straggler-pressure-selfcheck.md`.


## 59. Trader Recruit — host-authoritative co-op revive (ProtocolVersion 35)

The KrokMP exploration (§2.1/§2.2) proposed a trader-recruit flow: a living
player at a trader can revive a dead teammate. CUO already had a strong
host-authoritative trade domain and the character save/restore snapshots, but
no revive lifecycle and no public UI for it.

- **Dedicated request/result pair, not a `TraderActionKind`** — ordinary trade
  actions run a vanilla game method locally first; recruit has no vanilla
  method, so the host owns the whole outcome. `TraderRecruitRequest`
  (NetMsg 107, guest→host) carries the target SteamId + the acting side's
  nearest-trader position; `TraderRecruitResult` (NetMsg 108, host→target)
  carries the authoritative post-revive `CharacterHealthMsg` + limbs.
- **Host-side policy** — `TraderRecruitPolicy` (`Runtime.Session.World`) locks
  the L0 rules in pure form: `CanRecruit` (`reputation >= 75`, `hostility <= 0`,
  `build.health > 200`, one recruit per trader instance), `IsDead`, and
  `PrepareRevive` (health baseline while preserving skills/items/limbs/
  position).
- **Unity shell** — `TraderRecruitCoordinator` (`GameAdapter.World`) finds the
  nearest trader for the acting side, re-validates on the host, saves the
  revived snapshot into `CharacterDataStore`, and delivers the revive:
  wire to a guest target, direct local apply for a host target.
- **Heal in place, not restore** — the target applies the result through the
  existing cross-player `CharacterDataSync.ApplyHealState` inside a RemoteApply
  scope. No inventory wipe, no position teleport; the death screen cancels when
  `body.alive` returns true (`PlayerCamera.HandleDeathScreen`, PlayerCamera.cs:
  2397-2410).
- **Peer visibility** — after applying, the target re-reports the full
  character snapshot (`ReportInventoryChanged`), so the host save and every
  peer clone refresh immediately.
- **Scope boundary** — no random trader items in this slice; the broader
  Permadeath/ReviveOnNextLevel/RespawnKeepInventory/RespawnKeepSkills/
  save-transition lifecycle landed later as #60, so the `Revive/respawn rules`
  backlog item is now closed.
- **ProtocolVersion 34→35** because a v34 peer has no NetMsg 107/108 handler and
  no revive flow.

Tests: `TraderRecruitPolicyTests` (7 cases), `TraderRecruitChannelTests`
(2 wire cases), direction-table rows + `EveryNetMsg_IsExplicitlyClassified`;
full suite **1219 green**, build 0 warnings/0 errors, architecture / event-replay
/ entity-event-dispatch gates pass. See
`docs/selfchecks/trader-recruit-selfcheck.md`.


## 60. Revive / respawn rules — next-level auto-respawn + host rules (no protocol bump)

The KrokMP exploration (§2.2) asked for the broader revive lifecycle after the
trader-recruit first slice: Permadeath, ReviveOnNextLevel,
RespawnKeepInventory, RespawnKeepSkills, save/level-transition integration and
revival for players who have already left the world. This entry lands that as
a small host-authoritative rule set, reusing the existing full character
restore path rather than inventing a new wire protocol.

- **Small rule surface** — `RespawnOptions`
  (`Runtime.Configuration`) with BepInEx `[Respawn]` config entries:
  `Permadeath`, `ReviveFromTrader`, `ReviveOnNextLevel`, `KeepInventory`,
  `KeepSkills`. The values are read through `IOptionsMonitor`, so a config edit
  hot-reloads.
- **Pure policy** — `RespawnPolicy` (`Runtime.Session.World`) locks the L0
  decisions: `CanUseTraderRecruit`, `CanAutoReviveOnNextLevel`, `IsDead`, and
  `PrepareRespawn` (physiological baseline via `TraderRecruitPolicy` +
  inventory/skills shaping + `Position=null` so the respawn lands at the
  current spawn, not the old layer).
- **Trader gate** — `TraderRecruitCoordinator` now refuses requests when the
  host rules disable trader revive; the existing trade gates remain unchanged.
- **Next-level trigger** — `RespawnCoordinator` (`GameAdapter.World`) watches
  the same `HarmonyTraverse.IsGenerating()` falling edge as the
  generation-item authority and runs one frame later. On the host, each dead
  handshaken player (including the host itself) is respawned from the latest
  authoritative snapshot.
- **Full restore on every side** — a guest receives the existing
  `CharacterData` restore (the same two-frame wipe path as reconnect), even
  while in-world; the host uses the new `CharacterDataSync.QueueRespawnRestore`
  local queue so there is no host-only apply path. This makes KeepInventory /
  KeepSkills real (a disabled keep flag actually wipes/zeros the restored
  state).
- **Left-world revival** — a dead guest whose `InWorld == false` is saved,
  then invited back with the new targeted `WorldService.SendWorldJoinTo`. The
  ordinary `SceneStateHandler` InWorld edge re-sends the saved character, so
  the guest resumes with the respawned snapshot.
- **No protocol change**: the respawn rides the existing `CharacterData`
  direction and the existing `WorldJoin` message; `ProtocolVersion` remains 35.
  Only the targeted world-join send-side helper is new.

Tests: `RespawnPolicyTests` (10 cases), full suite **1229 green**, build 0
warnings/0 errors, architecture / event-replay / entity-event-dispatch gates
pass, `dotnet format` passes (generated `obj` excluded). See
`docs/selfchecks/respawn-rules-selfcheck.md`.


## 61. Text chat — host-relayed co-op chat line (ProtocolVersion 36)

The 2026-08-23 exploration (§2.5) listed text chat as the first clear
communication feature: CUO currently only syncs in-world Talker bubbles via
`SpeechMsg`, and there is no text chat surface. This entry lands a single
simple chat line with a star relay, not a copy of KrokMP's full chat box.

- **One bidirectional wire shape** — `ChatMsg` (NetMsg 109) carries
  `SenderSteamId` + final `Text`. A guest reports its own line to the host;
  the host validates and broadcasts to every other member; a guest never
  sends peer-to-peer.
- **Host relay authority / anti-spoof** — `ChatHandler` drops a line whose
  claimed `SenderSteamId` does not match the transport sender, drops
  whitespace/empty/oversized text, and only then fires the local event and
  relays. `ChatPolicy` (`MaxLength = 200`) is shared by the send path and the
  receive path.
- **Pure Runtime domain** — `ChatService` owns a bounded 50-line recent buffer
  + `TrySend`; it depends only on `ISessionControl` + `IWorldControl`, so the
  same code is exercised by L0 fake-network tests. `ChatChannel` is the thin
  wire channel, following the `SpeechChannel` pattern.
- **UI** — `OnlineUiOverlay` draws a bottom-right IMGUI panel (last 7 lines +
  one input + Send button) while the session is active. Persona names come
  from `SteamService`; the buffer is session-scoped and clears on `SessionEnded`.
- **ProtocolVersion 35→36** because a v35 peer has no `Chat` handler and no
  text-chat relay.

Tests: `ChatServiceTests` (6 end-to-end/edge cases + direction theory), full
suite **1238 green**, build 0 warnings/0 errors, architecture / event-replay /
entity-event-dispatch gates pass, `dotnet format` clean. See
`docs/selfchecks/chat-selfcheck.md`.


## 62. Trader Recruit random trader-stock bonus items (ProtocolVersion 37)

The trader-recruit first slice (#59) intentionally left the KrokMP "$1–3
random trader items" part as a later increment. This entry closes that
remaining gameplay slice without expanding the revive lifecycle.

- **Gift pool** — the host reads the trader's current stock through the
  existing `TradeExecutor.Read` (`TradeStockState.Items`), so the reward comes
  from actual trader-sellable items. The stock is treated as a catalog, not a
  depletable inventory, in this increment.
- **Pure selection policy** — `TraderRecruitPolicy.SelectGiftItemIds(stock,
  count, randomIndex)` selects distinct stock item ids with an injected index
  function; `FindEmptySlots` caps grants to the target's real empty slots.
  Constants `MinGiftItems = 1`, `MaxGiftItems = 3`.
- **Host item fact without a temp object** — `TraderRecruitCoordinator`
  captures the fresh item wire fact directly from the prefab
  (`ItemStateCodec.CaptureItem` on `Resources.Load(id)`) and allocates a bare
  host instance id via `ItemIdAllocator.AllocateId`; no scene spawn/destroy
  report can fire. The host appends the gifts to the saved revived snapshot.
- **Guest ownership** — each gift for a remote target is registered through
  `ItemService.AdoptTransferredItem`, so later guest use/slot/drop reports
  arbitrate against the host's transfer table.
- **Delivery** — `TraderRecruitResultMsg` gains `Items` (protobuf member 4).
  The target applies each gift inside the existing RemoteApply heal scope via
  `ItemStateCodec.RestoreItem` (host-chosen slot first, `Body.FirstEmptySlot`
  fallback) and immediately re-reports the character snapshot.
- **ProtocolVersion 36→37** because a v36 peer can still connect but would not
  know about the result's new `Items` field and would silently revive without
  the bonus — a feature difference that should reject at handshake.

Tests: `TraderRecruitPolicyTests` (6 new pure cases), `TraderRecruitChannelTests`
(result round-trips `Items`), full suite **1244 green**, build 0 warnings/0
errors, architecture / event-replay / entity-event-dispatch gates pass. See
`docs/selfchecks/trader-recruit-gift-items-selfcheck.md`.

## 63. NetMsg direction registry — fail-closed protocol metadata (no protocol bump)

Backlog §3.2 identified the receive-side direction table as a manually
maintained, fail-open switch: `PacketReceiver.IsValidDirection` ended in
`_ => true`, so a new/unknown message id would silently become valid instead
of being rejected. This entry replaces that with a single immutable protocol
registry and makes both sides of the data plane fail closed.

- **One direction source** — every `[PacketHandler]` attribute now requires an
  explicit `NetMessageDirection` (`GuestToHost`, `HostToGuest`,
  `Bidirectional`). The old receiver switch is deleted.
- **Registry** — `NetMessageRegistry` (`Runtime/Session`) is built once from
  every Runtime `IPacketHandler` type. `NetMessageMetadata` carries the wire id,
  the locked direction and the payload type derived from the handler's
  `PacketHandlerBase<TPacket>` generic argument.
- **Receiver fail-closed** — `PacketReceiver.OnTransportMessage` first checks
  `NetMessageRegistry.TryGet`; an unregistered id is dropped with a warning
  before any handler can see it. Direction validation then comes from the
  registered metadata, not a switch.
- **Sender fail-closed** — `PacketSender.TrySend` / `SendToAll` refuse
  unregistered ids before encoding. This catches a programming error at the
  source instead of silently wasting a frame that the receiver would drop.
- **Dispatcher startup validation** — `PacketDispatcher` verifies each
  handler's id exists in the registry while building the route table.
- **Reliability stays a call-site decision** — the exploration proposed
  "reliability" in the registry, but several messages are genuinely sent both
  reliably and unreliably by path (e.g. `ItemSnapshot` one-shot reliable vs
  periodic unreliable; `CharacterData` reliable restore vs periodic snapshot).
  Baking a single boolean into the registry would be wrong and would require
  per-call override anyway, so it is deliberately not added.
- **Tests** — `DirectionTests` stays the independent 3-way contract and now
  exercises the registry-backed receiver; `NetMessageRegistryTests` locks
  every `NetMsg` registered, explicit direction + payload type, and unregistered
  ids rejected on both receive and send.

No wire/protocol change: same `NetMsg` ids, same payload classes,
`ProtocolVersion` remains 37. See
`docs/selfchecks/netmsg-registry-selfcheck.md`.

## 64. World-entry snapshot completion + fan-out ownership (ProtocolVersion 38)

Backlog §3.4 asked for an explicit end-of-backfill marker; §3.3 also called
out that `HandlerContext` owned a concrete world-entry fan-out. This entry
does both in one small domain change.

- **Dedicated fan-out service** — `WorldEntryFanout`
  (`Runtime/Session/World`) now owns the ordered world-entry snapshot group.
  It replaces `HandlerContext.SendWorldStateToMember` (removed), and
  `SceneStateHandler` / `HandshakeHandler` depend on it directly. This takes
  the concrete world-entry flow out of the handler context god object.
- **Completion marker** — the fan-out sends the whole snapshot group
  (`BlockState`, `BlockDamage`, `TrapState`, `OpenedEntities`,
  `BuildingEntityHealth`, `TrapLayout`, `RadiationLineState`, `ItemSnapshot`,
  `EnemySnapshot`) and then `WorldSnapshotComplete` (NetMsg 110,
  HostToGuest). The receiver raises `WorldSnapshotCompleteReceived`.
- **Why a new message** — `WorldReady` means "start playing", not "backfill
  complete"; the marker is also needed on reconnect-while-InWorld where no
  WorldReady is sent. A separate id keeps the two semantics independent.
- **ProtocolVersion 37→38** — a v37 peer has no `WorldSnapshotComplete`
  handler and cannot know when a join backfill is complete, so this is a
  breaking wire change.
- **No batch message** — the individual snapshot messages stay independent;
  the completion marker is the explicit "set complete" edge rather than
  inventing a new batched payload type.
- **Tests** — first InWorld edge and reconnect-while-InWorld both receive the
  marker; `DirectionTests` / `NetMessageRegistryTests` cover the new HostToGuest
  message and registry entry.

See `docs/selfchecks/world-entry-completion-selfcheck.md`.

## 65. Partial-aware architecture gate + debt ledger (no protocol change)

Backlog §3.1 found that the architecture gate counted per-file lines, so a
logical class split across partial files could exceed the 600-line rule without
being caught. This entry makes the gate logical-type-aware and makes existing
debt explicit instead of invisible.

- **Logical aggregation** — `tools/check-architecture.ps1` now extracts
  namespace + top-level type from every `src` `.cs` file and aggregates line
  count and expression-state bool fields across all partial files of the same
  complete type. A logical type over `MaxLines` / `MaxBoolFlags` is a failure,
  not just a physical file over the limit.
- **Debt ledger** — `docs/architecture-debt.json` records the current
  aggregate sizes of existing over-limit logical types. The gate allows
  recorded debt but:
  - fails any unrecorded over-limit type (new debt must not appear silently);
  - fails any growth beyond the recorded size (existing debt must not get
    worse);
  - prints the current debt as a visible warning on every run.
- **Strict mode** — `tools/check-architecture.ps1 -Strict` refuses all
  recorded debt. This is the switch to use once the flattening work is done.
- **First real split** — `WorldEventSync`'s building-entity half moved from a
  partial into a separate top-level `WorldBuildingEntitySync`; `WorldEventSync`
  delegates the patch entry points and wires the event subscriptions. This
  reduced the logical class from 643 aggregate lines to under 600 and removed
  it from the debt ledger. It is a responsibility split, not another physical
  partial split.
- **Remaining debt** — `ModService` (1590), `GameAdapter` (1397),
  `ItemService` (928), `WorldService` (899), `EnemySyncCoordinator` (750),
  `PlayerInteractionService` (716), `ItemApplication` (630) remain recorded in
  `docs/architecture-debt.json` and are listed as the follow-up flattening
  item in `docs/backlog.md`.

No wire/protocol change. See `docs/selfchecks/partial-aware-gate-selfcheck.md`.
