# CUO Architecture Blueprint

> Companion document to `AGENTS.md`. This is the design reference: architecture, technical stack, sync model, specs, and pitfalls. It is a blueprint, not a code map — implement incrementally per the development phases.

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

> The "message envelope" sketched above predates the 2026-08-07 star-topology decision (pure star, **no envelope** — see docs/tech-decisions.md "Wire transport"); the section below is the live model.

### Star Topology & Arbitration Framework (landed 2026-08-08)

Topology is a pure star, host-authoritative. Every message flows guest → host; the host arbitrates and decides the fan-out. No envelope: the transport supplies the sender, `SendTo` expresses the destination, `EntityId` inside the message expresses ownership, `Seq` in the state stream handles reordering.

**Member table** (`SessionService._members`, key = SteamId — stable across reconnects): host holds one entry per guest; a guest holds the host plus roster entries for other guests, because the host broadcasts the full entity list (local + all synced members, `PlayerStateMsg.Entities`) and every side renders every member. Membership announcements are `PlayerJoin` in two modes on one wire shape — self-activation (`GuestSteamId` == receiver) and roster broadcast (`GuestSteamId` = another guest, with its spawn anchor). Members leave via `PlayerLeave` (host → guests) or the presence poll (host removes lobby-missing members individually).

**WorldJoin (host → guest, landed 2026-08-08)**: the host owns the enter-the-world timing. Sent *after* the world params (ordered, reliable) — at handshake time when the host is already in a world, and when the host enters the world — so the guest starts the run only with the params in hand (the guest-side follow pump and its params race are gone). A guest bound to a lobby may not enter a world on its own: the run-start gates (StartRun/LoadRun/StartTutorial) refuse unauthorised starts, and the start screen (`runSettingsScreen`) is host-only — the menu's AdaptiveButton Play/Tutorial entries are disabled (they are custom components, not UnityEngine.UI.Button), with the forced-close as a backstop.

**Late-joiner perspective — every sync object must answer "how does a late joiner get the history?" (landed 2026-08-08, commit bdedef6)**: entity buffers are created *only* by join announcements (`PlayerJoin`); the 20 Hz state stream only updates existing buffers, it never creates them. A member joining mid-session therefore misses every earlier member unless the host explicitly backfills: `StartMemberSync` re-sends each already-synced member's `PlayerJoin` to the newcomer after its self-activation (3-player repro: guest2 never saw guest1 — guest1's join predated guest2 and nothing re-announced it). Two generic answers, chosen by the object's semantics: **event replay** (member/roster-like objects — re-send the join announcements; cheap and idempotent) or **full snapshot** (world-state-like objects — items, NPCs, damage accumulations — send the current state once, never replay history). The host owns the backfill: late joiners do not pull, the host pushes on join. Apply this question to every new sync object in Phase 3 (items, NPCs, world damage) before designing its join protocol.

The session layer is domain-split (landed 2026-08-08): `SessionService` owns the presence table (`MemberPresence` — who is in the session, in which scene), `EntitySyncService` owns the entity side (buffers, entity ids, per-member sync state, the 20 Hz stream and the join/leave announcements — it reads the presence table for the sync decisions and is notified of removals via `SessionService.MemberRemoved`), `CharacterDataStore` owns the SteamID-keyed character save/restore. Dependency direction is entity/data domains → session → transport, acyclic, plain constructor injection; the split is the landing pattern for the Phase 3 world/inventory domains.

**World determinism & damage persistence (landed 2026-08-08)**: the game has no seed concept — every random call goes through the single global `UnityEngine.Random` stream (zero `InitState`/`state`/`seed` call sites), and generation is a cross-frame coroutine (WorldGeneration.cs:1534, yields every ~100 columns) whose suspension points let frame-rate-dependent consumers (Body.Update effects, earthquake timers, WorldGeneration.cs:857-901) pollute the stream. Host and guest already started from the same captured `Random.state` (`WorldStartParams.RandomState`), but diverged between every yield — the "world mostly matches, details differ" symptom. Fix: the `GenerateWorld` coroutine is **wrapped, never replaced** (the game's own body — loading UI, generatingWorld flag, step order — stays verbatim) by `WorldGenRandomIsolation`, which snapshots Random.state around every suspension (nested coroutines, e.g. Terrain → Structures, are driven recursively and need no wrapping of their own), so the generation stream advances purely from generation code. **The wrap applies unconditionally — solo or session**: a solo-generated world is a pure function of the captured Random.state (CaptureWorldParams runs for Role=None too), which is what makes mid-session joining work: a guest joining a solo-turned-lobby host restores that state and regenerates the identical world — no world-data transfer (state bytes vs MBs; a full-snapshot WorldData path was designed and rolled back in favor of this, user decision). Damage persistence answers the late-joiner question for world mutations: the host **or solo player** records every post-generation `SetBlock` (mining, remote damage application, earthquakes, building) against the generated baseline (captured at generation completion, per world/layer) in a difference table (WorldService, capped at 65536) — a block equal to its baseline entry (placed then mined away) is removed, anything else is upserted; the table is sent as a full snapshot (`WorldBlockState`) when a guest reports InWorld — the guest applies it to its regenerated seed world, so a mid-session joiner sees the host's accumulated mining/building and a reconnect no longer resurrects mined blocks. Partial (not-yet-broken) block damage has the same late-joiner shape: `BlockDamageRegistry` records the host's post-write `BlockDamage.damage`, `BlockDamageSnapshot` (NetMsg 89) backfills it on world entry/reconnect/60 s resend, and the guest applies it as an absolute set (never an additive delta); the live `BlockDamaged` relay also carries `MetalBonus` so the game's ×10 metallic multiplier applies identically everywhere. Placements in a live session additionally flow as live events (`BlockPlaced`, report → arbitrate → fan-out, target must be air — first-writer-wins). Guests never track; they only apply.

**Start gate — everyone loads together, starts together (landed 2026-08-08)**: `WorldJoin` is broadcast at generation *start* (all guests begin loading in parallel — previously only after the host finished, so guests trailed one full generation). When the host enters the world it arms a start gate (WorldService): every handshaken guest must report InWorld — or 30 s elapse — before `WorldReady` releases everyone at once; a late joiner (gate not armed) is passed through directly on its InWorld. While the gate holds the world is **truly paused** (Time.timeScale = 0; the game's PauseHandler, which force-restores Normal speed, is patched off during the wait) plus a full-screen overlay with a smart wait text (who we wait for + the force-start countdown; guest countdown anchored to the host's InWorld relay). The host is authoritative from lobby creation (`SessionActive` set in OnLobbyCreated — previously only on handshake, so a host generating before any guest connected ran *un*isolated and a later guest got a different world). Guests follow the host into the tutorial too: the WorldJoin handler picks `StartTutorial` when the params' biome is Tutorial.

**Lobby identity lifecycle (landed 2026-08-15)**: Role follows the actual lobby — `None` in no
lobby, `Host` on create/own-lobby entry, `Guest` on entering another player's lobby. Steam-level
create and join both leave the current lobby first and fire `LobbyLeft`; `SessionService` then tears
the old session down completely (per-member scene teardown + one `SessionEnded`) before binding the
new identity and re-handshaking. Lobby switches are menu-only: while a world is running or
generating the plugin guard refuses F8/F9/Steam-friend join with a visible reason, except the
solo-in-world -> host-lobby conversion, which the late-joiner snapshot design depends on.

**Behavior packets (local compute → report → arbitrate → fan-out)**: a guest performs actions locally with full single-player feel, then reports the action; the host validates against the authoritative world state, applies it, and relays to the other members **excluding the source** (it already applied locally). The host's own actions are broadcast directly. Echo protection is layered: the adapter's reentry guard suppresses the local application from generating a new report, and the relay excludes the source.

**Item state sync (complete, landed 2026-08-08)**: `CharacterItemMsg` is the wire form of the official save's `SavedItem` + `[Saveable]` component dictionaries (SaveSystem.SaveGame): condition/favourited/slot, recursive container contents (restored with the game's own semantics — a non-empty slot takes the item into its container, SaveSystem.cs:304-329), `WaterContainerItem` liquid stacks, and a generic `[Saveable]` component snapshot (public/SerializeField simple fields; Unity references never serialized). Restore is exact-rebuild, never additive — the prefab's Awake fills the defaults (WaterContainerItem.Awake), so adding on top reads "full" again.

**Arbitration feedback tiers** — chosen by one rule: *does a rejected action leave the local view diverged from the host's truth?* The host executes every accepted action with **its own authoritative data** (its world table is the entity's state; the guest's report only names the action and carries evidence — the host never adopts the guest's asserted payload wholesale). A correction is **not** the consequence of a rejection: it is a data-sync tool for "action valid, guest's stored data wrong", and it never blocks the action.

Three tiers, decided by two independent checks:

| Check | Question | Answer decides |
|---|---|---|
| **Action validity** | Does the action make sense against the host's world (entity exists, operation possible)? | **Accept vs Reject** |
| **Data correctness** | Does the guest's reported evidence match the host's state? | **Correction packet vs none** (never affects accept/reject) |

| Operation | Local execution | Host judgment | Divergence on rejection | Tier |
|---|---|---|---|---|
| Block damage / mining | Applied immediately | Target state (already-broken blocks absorb idempotently — `DamageBlock` is safe on air, health = 0) | None — the block already looks broken | **Silent** (landed) |
| Scene state | Applied immediately | State report; host only tracks the member | None | Silent (landed) |
| Character data (1 Hz reports) | Normal gameplay | Numeric sync; host keeps the latest per SteamID | None — guest is the local truth | Silent (landed) |
| Pickup / interaction (Phase 3) | Item enters the inventory immediately | **Unique ownership** (host world table, first-writer-wins) + evidence check | **Diverged and not self-healing** (the state stream carries no inventory) | **Accept; + correction when the guest's evidence differs** (wrong contents/state — the host executes from its own table entry and re-sends the true state). **Reject only for invalid actions** (unknown id, not a world item — item rollback + feedback sound) |
| Placement / construction (Phase 3) | Structure spawned locally | Generation conflict (occupied / not placeable) | Diverged, not self-healing | Reject + correction (remove/replace) |
| Combat damage (Phase 3) | Damage/anim applied | Target state + ownership mix | Partially diverged — health self-heals via the state stream | Silent + correction (health) / reject (ownership) |
| World time (Phase 3, landed ProtocolVersion 13) | Guest speed intent → `WorldTimeRequest`; local manual/sleep timeScale writes suppressed | **Authoritative write** — the host applies `WorldTimePolicy` (movement → Normal; all-unconscious → 25×/3.5×) and broadcasts `WorldTime` | Self-healing — world-entry fan-out + 5 s resend + direct-write adoption | Silent + correction (time packet) |

Anti-cheat is explicitly **out of scope** — arbitration is the host's natural job of owning a shared world, not policing. The silent tier is implemented; the pickup tier (accept-with-correction + reject-for-invalid) lands with the first real conflict scenario (pickup, Phase 3): the pickup report carries the full item evidence (`CaptureItem`, symmetric with the drop report), the host validates evidence against its world-table entry, executes the transfer (world table → the picker's character data) from **its own entry** — never waiting on the guest's later 1 Hz report to carry the data across — and sends a correction packet when the guest's evidence diverged. Each operation's packet shape, rejection reason and correction payload are per-operation messages (no generic rejection envelope — consistent with the no-envelope rule).

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

Mods do not get all permissions by default: `ReadGameState, WriteGameState, SpawnEntity, SendNetworkMessage, RegisterContent, RegisterCommand, ExecuteHostAction, AccessNativeApi`.

- Guests must not write host-authoritative state.
- Client-only mods must not register sync objects.
- Undeclared network messages are rejected.
- Mod messages are rate-limited.
- Host validates all mod command parameters.

### Phase 4 status (first round landed 2026-08-13; second round landed 2026-08-16)

The core skeleton is live: mod discovery (BepInEx plugin shell + `[CuoMod]`
scan on the first update frame — BepInEx 5 loads plugins one by one,
load-then-Awake, verified by IL and the game log), the `ICuoMod` lifecycle
with per-mod exception isolation, the manifest (`[CuoMod]` is the single
source; `NetworkMode` Unspecified is rejected — fail-closed), mod network
messages (NetMsg 75, opaque payload, report/定向 star semantics, 64 KiB
policy cap), session events (bind-time snapshot semantics), and the handshake
consistency check. The second round landed the full permission model
(`ModPermission` declaration + live enforcement + handshake equality), host
commands (NetMsg 86/87, host-authoritative execution, per-sender rate limits),
dependency ordering (topological load, missing/cycle/transitive rejection),
and strict SemVer versions (ProtocolVersion 10). Binding contract:
`docs/mod-api.md`.
Mod-state saves are landed (host-persistent `IModState`, see `docs/mod-api.md` §4d),
the local mod UI surface is landed (`IModUi`, see `docs/mod-api.md` §4e),
content registration is landed (`IModContent`, see `docs/mod-api.md` §4f),
ReadGameState is landed (`IModGameState`, see `docs/mod-api.md` §4g), entity
spawn is landed (`IModEntitySpawn`, see `docs/mod-api.md` §4h), and
AccessNativeApi is landed as a curated operation registry (`IModNativeApi`,
see `docs/mod-api.md` §4i). The Phase 4 Mod API surface is now complete except
for future extension points, which are addressed as concrete consumers appear.

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

### Microsoft.Extensions as CUO Infrastructure (landed 2026-08-06)

> Status: projects split (`CUO.Abstractions` + `CUO.Runtime`, Core renamed);
> DI + `ILogger<T>` + BepInEx/rolling-file providers live; `ICuoService` in
> `CUO.Abstractions`. Still future: Options' BepInEx ConfigFile adapter,
> `CUO.GameAdapter`, structured scopes.

Adopt the Microsoft.Extensions stack as CUO's internal infrastructure, with a
clear separation: *Microsoft.Extensions provides the plumbing; BepInEx/Unity
own the lifecycle and main loop.*

| Capability | Use | Notes |
|---|---|---|
| `DependencyInjection` | **yes — core architecture** | ServiceCollection → BuildServiceProvider at framework start |
| `Logging.Abstractions` (`ILogger<T>`) | **yes** | bridged to BepInEx logging via one `BepInExLoggerProvider` (level mapping: Trace/Debug/Information/Warning/Error/Critical); structured scopes: SessionId, SteamId, ModId, GameBuild, Tick, EntityId |
| `Options` | **yes** | BepInEx `ConfigFile` → configuration provider → `IOptions<T>`; users still see `BepInEx/config/<guid>.cfg` |
| `Configuration` | optional | only as the BepInEx adapter above |
| `Hosting` (Generic Host) | **no** | it would take over app lifecycle that Unity already owns |
| `BackgroundService` | **no** | must not manage the Unity game loop |

Lifecycle: BepInEx `Awake` → Initialize; game playable → Start; Unity
`Update`/`FixedUpdate` → Update; unload/exit → Stop/Dispose. Implement a small
`ICuoService` interface (Initialize/Start/Update/Stop/Dispose) and forward
BepInEx/Unity lifecycle notifications into it. Never create scopes per frame;
never register `MonoBehaviour`s as transient services.

Package layering (the "who references what" contract):

```
CUO.Abstractions  ← Microsoft.Extensions.{DependencyInjection,Logging}.Abstractions + Options
                       (the ONLY package mods may reference)
CUO.Runtime       ← DI + Logging + BepInEx integration
CUO.GameAdapter   ← game assembly references + HarmonyX
CUO.Mod           ← references CUO.Abstractions only
```

Mods never reference BepInEx, Steamworks, or the game's private assemblies
directly — change surface stays concentrated in CUO.GameAdapter.

Compatibility constraints (binding): pin conservative Microsoft.Extensions
versions that support net48 (the 3.1.x line does); never take the latest
.NET-era packages; CUO owns and centralizes the Extensions DLLs — mods never
ship their own; verify no clash with the game's bundled assemblies.

## 6. Compatibility & Version Negotiation

Handshake before play: Steam identity → framework protocol version → game version → mod ID list → mod versions → content hashes → network capability negotiation → admit to game.

Default policy: different framework major → reject; different game version → reject or warn; missing network mod → reject; inconsistent Client-only mod → allow; inconsistent Cosmetic mod → allow; different content-mod hash → reject; missing host-only mod → guest may join without it but must understand its sync protocol.

Lobby metadata stores only a digest: `frameworkVersion, gameVersion, modListHash, modCount, mapId, gameMode, hostSteamId`. Never write the full mod list into Lobby data repeatedly (Steam discourages high-frequency metadata updates).

## 7. Host Migration

**Not in the first version.** Host holds: world RNG state, quest state, unsaved item state, NPC internals, physics state, mod private state, temp coroutines, save-write authority.

MVP behavior: host exit → session terminates → guests return to lobby.

Later (requires a full snapshot system): periodic full world snapshots, mod-state saves, new-host election, snapshot restore, full entity resync, handling of old host's uncommitted operations. Do not claim host migration support without the snapshot system.

**Priority note (2026-08-25)**: host migration is promoted to a MEDIUM post-MVP item. It is not in the MVP, but it is now an intentional near-term target once the snapshot/mod-state foundations are in place. A dedicated server process is not planned for the friends/co-op model; that remains a future option only if public community hosting becomes a real goal.

## 8. Saves

- Host is the only save authority; guests never write the world save.
- Host saves world state periodically; guests keep local settings and personal display data only.
- Guest `Player State` restore data is disk-backed on the host (`CharacterDataFileStore`, see tech-decisions §27): memory stays session-scoped, the disk copy survives a host restart / continue-run, and a new run clears it.
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
- **Phase 5 — Tooling & ecosystem**: mod manager, auto-install, version checks, crash reports, network diagnostics, conflict detection, compatibility database, dedicated server. Host migration is a separate MEDIUM post-MVP item (see §7) rather than a Phase 5 future line.

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
