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
- **Accepted residual**: the 5-second lit-fuse visual (child sprite + fuse
  audio) on remote clones stays local-only — short-lived presentation, not
  persistent state (same family as the crystal-unstable pre-explosion ticking).
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
