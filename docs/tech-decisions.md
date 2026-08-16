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
