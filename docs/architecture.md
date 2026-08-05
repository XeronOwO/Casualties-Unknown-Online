# CUO Architecture Blueprint

> Companion document to `CLAUDE.md`. This is the design reference: architecture, technical stack, sync model, specs, and pitfalls. It is a blueprint, not a code map — implement incrementally per the development phases.

## 1. Overall Architecture

CUO is not "adding a network layer to a single-player game". It injects a new multiplayer runtime via BepInEx and reorganizes the locally-executed game state into a host-authoritative simulation with guest input/state sync.

```
┌─────────────────────────────────────┐
│              Mod Layer              │
│   content / gameplay / UI / API mods│
└─────────────────────────────────────┘
                 │
┌─────────────────────────────────────┐
│           Mod Framework API          │
│  registries, events, commands,       │
│  entities, sync, permissions         │
└─────────────────────────────────────┘
                 │
┌─────────────────────────────────────┐
│          Multiplayer Runtime         │
│  Host, Guest, Session, Tick,        │
│  Snapshot                            │
└─────────────────────────────────────┘
                 │
┌─────────────────────────────────────┐
│            Game Adapter              │
│  scene, entity, input, save,         │
│  physics, UI adaptation              │
└─────────────────────────────────────┘
                 │
┌─────────────────────────────────────┐
│          BepInEx / Unity Layer       │
│  Chainloader, HarmonyX, Unity API,  │
│  Steam                               │
└─────────────────────────────────────┘
```

Design principles:

- Mods must not depend heavily on the game's private classes.
- The network layer must not understand game objects.
- The Game Adapter converts "game-internal state" into "framework-syncable state".
- The host decides the final state; guests submit inputs or requests.
- Every network-enabled mod must declare its sync requirements and permissions.

### Stable Core + Replaceable Adapter

The game will never adapt to us; every game update can break class names, signatures, scene structure, save format, Unity version, Mono/IL2CPP mode. The goal is not "always compatible" but:

> After a game update, the core stays unchanged — only the Game Adapter needs replacement or additions.

```
CUO Core
├─ network protocol
├─ host/guest state machine
├─ mod loading & dependencies
├─ serialization
├─ tick/snapshot
├─ logging & diagnostics
└─ version negotiation

CUO Game Adapter
├─ CU-Demo-001
├─ CU-Demo-002
└─ CU-Release-001
```

The core must not reference Casualties Unknown types. Only the adapter finds players, scenes, entities, intercepts input, reads/writes game state, handles saves, and absorbs version changes.

## 2. Technical Stack

| Layer | Technology |
|---|---|
| Mod loading | BepInEx 5 (current project target) / BepInEx 6 when stable and migrated |
| Runtime patching | HarmonyX (Prefix / Postfix / Transpiler) |
| Finer-grained hooks | MonoMod.RuntimeDetour when needed |
| Assembly structure changes | Mono.Cecil (last resort only) |
| Config | BepInEx Configuration |
| Logging | BepInEx Logging |
| Networking (MVP) | `ISteamNetworkingMessages` (simple, reliable+unreliable, 2–8 players) |
| Networking (later) | `ISteamNetworkingSockets` (connection state, per-channel reliability/order/priority, SDR relay) |
| Discovery/roster | Steam Matchmaking & Lobbies; Lobby data holds only metadata digest |
| Identity | SteamID |

**Steam Lobby is not the transport.** Valve docs are explicit: Lobby handles party/matchmaking; game data flows through P2P networking / sockets / your own layer.

Do not put all logic in one entry plugin — split the framework:

```
Framework.Bootstrap
Framework.Core
Framework.GameAdapter
Framework.Network
Framework.Steam
Framework.ModApi
Framework.UI
Framework.Diagnostics
```

Mono Unity and IL2CPP Unity should be separated at the adapter layer (BepInEx treats them as distinct runtimes; Mono is generally more stable).

Rule of thumb:

| Scenario | Technology |
|---|---|
| Insert logic before/after a method | HarmonyX |
| Replace call paths | HarmonyX / RuntimeDetour |
| Must add types, fields, methods | Mono.Cecil |
| Read game state | adapter + reflection/public surface |
| Inject manager objects | BepInEx plugin lifecycle |
| Game version compatibility | feature scanning + version adapters |

Do not expose Steam APIs to mods directly — abstract as `INetworkTransport`, `ISession`, `IPeer`, `INetworkChannel`, `INetworkMessage` so it can be swapped for: local loopback, LAN UDP, Steam P2P, dedicated server, virtual test network.

## 3. Multiplayer Sync Model

**Host authority, guest input driven.**

```
Guest
  │  Input / Command
  ▼
Host Simulation
  │  Snapshot / Event
  ▼
Guests Render State
```

Host owns: game rules, RNG, NPC behavior, physics results, item spawning, damage calculation, quest progress, save writes, mod permission arbitration.

Guest owns: local input collection, operation requests, rendering host state, client prediction, local UI/effects.

Never run "each client's own world". Single-player games are not network-deterministic; independent client simulation diverges (physics, RNG, NPC state, object counts, save conflicts).

### Sync Object Categories

Do not "sync all GameObjects". Classify state:

1. **Input** (commands, not final positions): move, attack, interact, use item, dialog choice, open container, build request.
2. **Authoritative state**: health, position, current scene, item counts, quest state, door open/closed, NPC behavior, world time.
3. **One-shot events**: sound, VFX, animation, toasts, quest-complete notice. Events are not persistent state — late joiners get the final state from a snapshot, never by replaying history.
4. **Content definitions**: item defs, weapon stats, NPC types, recipes, skills, map defs. Provided by mod manifests/content registries — never synced per frame.

### Tick, Snapshot, Events

Use an internal Tick, not Unity `Update()` directly:

```
Frame Update
    ├─ collect input
    ├─ process network packets
    ├─ host: run simulation tick
    ├─ generate snapshot
    ├─ guest: apply snapshot
    └─ render-layer update
```

Early numbers: simulation tick 10–20/s; rendering follows Unity frame rate; positions not sent every frame; non-critical objects at low frequency; UI/audio via events; large objects chunked.

Message types at minimum: `Handshake, ModManifest, PlayerJoin, PlayerLeave, InputCommand, EntitySpawn, EntityDespawn, EntitySnapshot, WorldSnapshot, GameEvent, StateHash, DisconnectReason`.

Message envelope: protocol version, message type, session ID, sender SteamID, tick, sequence number, mod/game version, payload length, optional checksum.

**Never use Unity instance IDs as network entity IDs** (process-local). Define `NetworkEntityId` = session epoch + host allocation counter + entity type/generation.

## 4. Game Adapter — the Hard Part

The difficulty is not Steam communication; it is identifying and controlling the game's internal state. Design one adapter per game build:

```
IGameAdapter
├─ SceneAdapter
├─ PlayerAdapter
├─ EntityAdapter
├─ InputAdapter
├─ InventoryAdapter
├─ SaveAdapter
├─ PhysicsAdapter
├─ UiAdapter
└─ RandomAdapter
```

Adapter responsibilities: find spawn point, identify player entities, convert local input to framework input, pause/replace original local logic, read/write game state, intercept scene transitions, handle entity spawn/destroy, handle saves, adapt game UI, handle pause logic.

**Find the few authoritative functions first** — the sync entry points:

- game main loop
- scene-load-complete callback
- player spawn function
- entity spawn/destroy functions
- damage resolution
- item add/remove
- interaction functions
- quest-state-change functions
- save read/write functions
- RNG entry points
- time advancement

Don't attempt to sync every underlying Unity object; find the few authoritative functions that change game outcomes.

### Version Capability Strategy

1. **Bind to capabilities, not exact versions.** Probe: player spawn function exists? scene load callback? entity spawn interface? inventory system? quest system? Then emit a capability report: `PlayerSpawn: Supported`, `InventorySync: Unsupported`, `QuestSync: Experimental`.
2. **Per-build adapter configuration** — a "Game Build Profile": game version, Unity version, runtime type, player-locating rules, scene-identification rules, entity-spawn rules, input entry points, save rules. Some builds genuinely need their own code adapter on top of config.
3. **Safe degradation on failure** — never force-start multiplayer after an update. Grades: `Compatible` (enable), `CompatibleWithWarnings` (testable start), `Unsupported` (single-player only, no multiplayer), `CriticalFailure` (disable patches, do not break the game). Never "patch failed but keep running, then states diverge".
4. **Startup self-check** — key types/methods exist, patches succeeded, player findable, scene interception works, entity spawning observable, Steam initialized, protocol usable. Startup log must say exactly what failed:

```
CUO detected game build: ...
Game Adapter: ...
Player integration: OK
Scene integration: OK
Entity integration: Warning
Multiplayer status: Supported
```

## 5. Mod API Design

Modeled on Forge's ideas, not Forge's complexity.

### Mod Manifest

`id, displayName, version, author, frameworkApiVersion, gameVersionRange, dependencies, conflicts, loadBefore, loadAfter, networkMode, contentHash, capabilities`

`networkMode` values: `ClientOnly`, `HostOnly`, `Cosmetic`, `Synchronized`, `Authoritative`, `RequiresAllPlayers`. Examples: UI skin → ClientOnly; new weapon → RequiresAllPlayers; host admin tools → HostOnly; pure particles → Cosmetic; new rules system → Authoritative.

### Lifecycle

`FrameworkLoading, FrameworkReady, LobbyCreated, SessionStarting, SessionStarted, SceneChanging, SceneLoaded, Tick, SnapshotReceived, PlayerJoined, PlayerLeft, SessionStopping, FrameworkShutdown`

Define explicitly: which callbacks run on the Unity main thread; which may touch game objects; which are host-only; whether an exception disconnects a player; whether mod unload is supported.

### Permission Model

Mods do not get all permissions by default: `ReadGameState, WriteGameState, SpawnEntity, SendNetworkMessage, RegisterContent, RegisterCommand, ModifySave, ExecuteHostAction, AccessNativeApi`.

- Guests must not write host-authoritative state.
- Client-only mods must not register sync objects.
- Undeclared network messages are rejected.
- Mod messages are rate-limited.
- Host validates all mod command parameters.

### KrokMP Compatibility Layer (future, reserved space)

Many community mods target **KrokMP**, the legacy multiplayer mod. To let those
mods run on CUO without rewrites, plan an optional compatibility adapter that
maps the KrokMP public API onto the CUO Mod API. This is a reserved extension
point, not near-term work:

- **Trigger**: after Phase 4 stabilizes the native Mod API AND real migration
  demand exists. No mapping is possible against a moving target — CUO's API
  must exist first, and KrokMP's API surface must be reverse-engineered and
  documented before any mapping is designed.
- **Placement**: a separate optional module (`KrokMP.Compat`), never woven into
  CUO Core. Loaded only when a legacy-style API call is detected.
- **Scope**: API-level compatibility only — namespaces, types, method
  signatures, events. Mods that deep-couple to KrokMP internals (Harmony
  patches on its private implementation) are NOT a compat target.
- **Constraints**:
  - The compat adapter never changes the shape of the native CUO Mod API.
  - Mapping tables are documented (KrokMP API → CUO API), not reverse-guessed.
  - KrokMP design flaws are not inherited — mapping forwards the capability,
    not the bugs ("learn, don't copy").
  - A mod uses either the native API or the compat API, or declares mixed use
    explicitly in its manifest.
- **Feasibility input**: during Phase 0/1 reversing, catalog KrokMP's public
  API surface (the game dir contains community mod bundles) and record findings
  in `docs/` — this decides whether API-level compat is realistic at all.

## 6. Compatibility & Version Negotiation

Handshake before play: Steam identity → framework protocol version → game version → mod ID list → mod versions → content hashes → network capability negotiation → admit to game.

Default policy: different framework major → reject; different game version → reject or warn; missing network mod → reject; inconsistent Client-only mod → allow; inconsistent Cosmetic mod → allow; different content-mod hash → reject; missing host-only mod → guest may join without it but must understand its sync protocol.

Lobby metadata stores only a digest: `frameworkVersion, gameVersion, modListHash, modCount, mapId, gameMode, hostSteamId`. Never write the full mod list into Lobby data repeatedly (Steam discourages high-frequency metadata updates).

## 7. Host Migration

**Not in the first version.** Host holds: world RNG state, quest state, unsaved item state, NPC internals, physics state, mod private state, temp coroutines, save-write authority.

MVP behavior: host exit → session terminates → guests return to lobby.

Later (requires a full snapshot system): periodic full world snapshots, mod-state saves, new-host election, snapshot restore, full entity resync, handling of old host's uncommitted operations. Do not claim host migration support without the snapshot system.

## 8. Saves

- Host is the only save authority; guests never write the world save.
- Host saves world state periodically; guests keep local settings and personal display data only.
- Save partitions: `World State`, `Player State`, `Mod State`, `Framework Metadata`.
- Mod save data must carry: mod ID, mod version, schema version, migration policy, missing-mod handling policy, corruption degradation policy.
- Never let mods serialize arbitrary objects to binary in saves — unrecoverable after game/mod updates.

## 9. Technical Specifications

### Network
- Network threads never touch Unity objects; all Unity API work returns to the main thread.
- Max message length; every message has a protocol version; every client input has a sequence number; every entity state has a tick.
- Distinguish reliable / ordered / unreliable channels; critical state must be resendable; non-critical cosmetic events may drop; packets are rate-limited; large state is chunked; disconnects carry explicit reasons.

### Mods
- Unique mod ID; no overwriting other mods' config files; no mutating other mods' internal state; no silent DLL installation; no undeclared dependencies.
- Every Harmony patch is traceable and has an unload/disable strategy.
- No hardcoded object paths as identity; no exposing Unity private fields in public API; public API is version-compatible.

### Logging
Tags: `[Framework] [Network] [Steam] [GameAdapter] [Mod:<modId>] [Entity] [Save] [Compatibility]`. Every session connection generates a Session ID so host and guest logs can be correlated.

### Game DLL references (references/ convention)

Game assemblies are copyrighted and never committed. Keep them out of the repository:

- Add a DLL **on demand** — only when code starts referencing types from it (copy commands in `references/README.md`)
- `references/README.md` documents the origin of each DLL
- csproj references them via `<Reference><HintPath>..\references\...</HintPath></Reference>` with `<Private>False</Private>` (compile-time only, never copied to output)
- `.gitignore` excludes `references/*.dll` but keeps `references/README.md`

Only the **Game Adapter** layer may reference game assemblies — CUO Core never does. (Pattern proven in the earlier JustUnknownCharacters mod.)

## 10. Pitfalls

1. **Treating LAN as plain UDP.** Steam Lobby / P2P / Networking Sockets ≠ traditional LAN broadcast. Steam-friend play → Lobby + Steam Networking. Fully-offline same-LAN → separate design (LAN discovery, local IP, no-Steam mode, auth, firewall hints, NAT/IPv6). Don't mix the two.
2. **Guests mutating game objects** → state races, duplicate spawns, item duplication, quest forks, save conflicts. Guests send input; host executes.
3. **Syncing all Transforms** → fails on physics, collisions, triggers, parenting, animation, nav, rigidbodies, scene loads, destroy/respawn. Sync game-semantic state instead.
4. **Hardcoded offsets/private fields** → type renames, signature changes, field reordering, compiler changes, IL2CPP structure changes, AOT reflection failures. Feature-scan, per-version adapters, startup capability probes, patch-success verification, safe disable on failure, auto-generated compatibility reports.
5. **Premature Mono.Cecil** → hard version upgrades, mod conflicts, hard debugging, runtime assembly-load failures, lost dynamic compatibility. Prefer Harmony; Cecil only for structural changes.
6. **Ignoring the main thread** → network/Steam/background callbacks must not touch GameObject, Transform, UGUI, SceneManager, Animator, Rigidbody, etc. Use a main-thread queue.
7. **No mod consistency checks** → host items guests lack, mismatched entity type numbers, divergent serialization, version-skewed results, instant guest crashes. Handshake + rejection must exist in the MVP.
8. **Undefined failure modes** → define: Steam disconnect, guest dropout, host dropout, mod load failure, scene-switch timeout, corrupt snapshot, game version mismatch, mid-game join. Prefer "safe exit, no save pollution" over "best-effort recovery".
9. **Ignoring legal/distribution risk** → game EULA (injection allowed? modified assembly redistribution?), anti-cheat triggers, Steamworks DLL redistribution, BepInEx (LGPL-2.1) and dependency licenses, original game assets inside mods, Steam Workshop / third-party hosting policies.
10. **Harmony patch state leakage** → if a Prefix modifies the target instance's fields to bypass original logic (e.g. clears a filter to let the original method skip), the Postfix MUST restore the original values — even when empty — before returning. Otherwise the field stays corrupted for every other read (UI updates, later refreshes). Pattern: save in `__state` in Prefix, restore in Postfix, then run custom logic.

## 11. Development Phases

- **Phase 0 — Feasibility**: BepInEx loads; Steam Lobby create/join; network connection; ping/pong; SteamID readout; both sides show connection state; safe disconnect on exit.
- **Phase 1 — Single player entity**: join, leave, position, heading, basic input, scene-load state. No inventory/combat/quests/saves.
- **Phase 2 — Entity lifecycle**: host spawns, guests receive, destroy, respawn, entity IDs, scene switching, snapshot resend.
- **Phase 3 — Game core loop**: interaction, items, combat, NPCs, quests, world time, saves — one system at a time, each with explicit host-authority and guest-render logic.
- **Phase 4 — Public Mod API**: content registration, custom entities, network messages, host commands, UI, mod-state saves, mod dependencies, manifest validation.
- **Phase 5 — Tooling & ecosystem**: mod manager, auto-install, version checks, crash reports, network diagnostics, conflict detection, compatibility database, host migration, dedicated server.

## 12. MVP Scope (recommended)

```
BepInEx + HarmonyX + Steam Lobby + Steam Networking Messages
+ host authority + hand-written Game Adapter
+ player entity sync + simple scene switching
+ mod manifest validation + host-exit-dissolves-session
```

Explicitly out of MVP: host migration, dedicated server, auto mod install, generic physics sync, arbitrary-game compatibility, full anti-cheat, cross-game generic entity system, complex client prediction, auto save migration.

One sentence: build a "pluggable host-authoritative multiplayer runtime" first, then the "Forge-like mod ecosystem" — not the mod store, content systems, or generic sync magic first.

## 13. Naming & Branding

- Product name: **Casualties Unknown: Online**
- Official abbreviation: **CUO**
- Community nickname / in-joke: **CuO** ("copper oxide")
- Future internal components: `CUO Framework`, `CUO Multiplayer Runtime` — external product name stays `Casualties Unknown: Online`.
- Versioning: CUO `0.x.y`, always accompanied by the supported game build range (e.g. "CUO 0.1.0 for Casualties Unknown Demo 0.4.x"). When a game update isn't adapted yet, actively block multiplayer rather than risk corrupted saves/crashes.
