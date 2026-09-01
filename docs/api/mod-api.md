# CUO Mod API — Phase 4 (Core Skeleton + Host Commands & Permissions)

Status: first round **landed 2026-08-13**; second round (4b) **landed
2026-08-16**. This document is the binding contract for mod authors AND for
the framework's future rounds — the semantics below are locked by tests
(`tests/.../Mods/`, 761 total green at landing) and the two-process runtime
verification (host + sandbox guest; the pre-release ProtocolVersion sequence was
later reset to `ProtocolVersion.Current = 1` — see tech-decisions #137).

## 1. Scope

**First round**: discovery, lifecycle, manifest, mod network messages,
session events, handshake consistency.

**Second round (4b)**: the full permission model (declaration + enforcement
for the live surfaces), host-authoritative commands, dependency ordering,
SemVer versions, and per-sender rate limits.

**UI is landed (see §4e), content registration is landed (see §4f),
ReadGameState is landed (see §4g), entity spawn is landed (see §4h), and
AccessNativeApi is landed (see §4i).**
The mod surface lives in **`CUO.Abstractions`** — the ONLY
assembly mods may reference (architecture.md §5.5). A mod never touches
BepInEx, Steamworks, the game assemblies, or CUO.Runtime. **Mod-state saves
are landed (see §4d), the local mod UI surface is landed (see §4e), and
content registration is landed (see §4f).**

## 2. How a mod is loaded (read this — the timing is deliberate)

BepInEx 5 loads plugins **one by one, load-then-Awake, in a single loop** —
verified by IL (`Chainloader.Start`: `Assembly.LoadFile → GetType →
instantiate → Awake → next`) and by the game's own log (the CUO plugin's Awake
lines appear BEFORE `Loading [HotRepl]`). Two hard rules follow:

1. **Discovery runs on the framework's FIRST UPDATE FRAME**, not in its Awake
   — a scan in Awake would miss every plugin loaded after it. The
   `ModService` scans `AppDomain.GetAssemblies()` once, finds every
   `[CuoMod]`-declared `ICuoMod` type, validates it (see §3), orders the
   accepted set topologically by dependencies, and binds it.
   The discovery frame runs Bind → Initialize → Start → Update in that same
   frame; Update per frame from then on. Stop/Dispose run in reverse load order.
2. **A mod's BepInEx shell Awake must stay EMPTY** — it may run before or
   after the CUO plugin's Awake and must not touch any CUO API.

**The double-instance trap**: BepInEx instantiates the shell (the
`BaseUnityPlugin`), CUO instantiates the `[CuoMod]` class — a single type
playing both roles yields TWO instances with independent state. The shell and
the mod class are always separate types (`src/CasualtiesUnknownOnline.ModExample/`
is the canonical layout).

**The BepInEx-reference reconciliation**: §1 says "never reference BepInEx",
the shell is a BepInEx plugin. The reconciliation: BepInEx.Core is referenced
ONLY as the loading mechanism (the shell derives `BaseUnityPlugin` and nothing
else); every line of business logic references CUO.Abstractions only.

## 3. The mod surface

```csharp
[CuoMod("com.example.mymod", "My Mod", "1.0.0", NetworkMode = NetworkMode.Synchronized,
        Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand,
        Dependencies = new[] { "com.example.dependency" }, Description = "...")]
public sealed class MyMod : ICuoMod   // ICuoService lifecycle + Bind
{
    public void Bind(IModContext context) { ... }   // once, before Initialize
    // ICuoService: Initialize / Start / Update / Stop / Dispose
}
```

- **`[CuoMod]`** is the single manifest source (id / displayName / version /
  `NetworkMode` / `Permissions` / `Dependencies` / description). `NetworkMode`
  defaults to `Unspecified` and is **rejected at discovery**. Other rejection
  causes: duplicated id, abstract/non-public type, missing public parameterless
  constructor, non-SemVer version, unknown permission bits or host/state
  permissions on `ClientOnly`/`Cosmetic`, and malformed/unsatisfiable
  dependencies (missing target, self, duplicate, cycle). One rejected mod never
  blocks the scan (per-mod fail-closed).
- **Permissions** are never implicit: the default is `ModPermission.None`.
  The eight declared flags are `ReadGameState, WriteGameState, SpawnEntity,
  SendNetworkMessage, RegisterContent, RegisterCommand, ExecuteHostAction,
  AccessNativeApi`. Live enforcement today: `SendNetworkMessage` gates
  `IModNetwork` send AND receive; `RegisterCommand` gates command
  registration; `ExecuteHostAction` additionally gates `ModCommand.IsHostAction`;
  `WriteGameState` gates host-persistent mod-state writes (`IModState`);
  `RegisterContent` gates mod content registration (`IModContent`);
  `ReadGameState` gates the read-only game-state projection (`IModGameState`);
  `SpawnEntity` gates the world entity-spawn surface (`IModEntitySpawn`);
  `AccessNativeApi` gates the curated native/game-private operation registry
  (`IModNativeApi`).
- **`Dependencies`** are mod ids loaded before the dependent; missing or
  cyclic dependencies reject the dependent (transitive failures propagate).
- **`ICuoMod : ICuoService`** — the standard lifecycle, driven by the
  framework's pump on the Unity main thread. Every stage is exception-isolated.
- **`IModContext`** — `Logger`, `Network`, `Commands`, `Session`, `State`,
  `Ui`, `Content` and events:

| Member | Semantics |
|---|---|
| `Session` | a **SNAPSHOT at bind time**, not a live view — the host never fires `SessionActivated` (it activated at lobby creation), and events fired before discovery are lost. The snapshot is the only reliable "current state". `MemberSteamIds` is the peer member set (the local peer is `LocalSteamId`). |
| `Commands` | host-authoritative commands — see §4b. |
| `State` | host-persistent per-mod state — see §4d. |
| `Ui` | local immediate-mode mod UI windows — see §4e. |
| `Content` | mod content registration — see §4f. |
| `GameState` | read-only player-state projection — see §4g. |
| `EntitySpawn` | world entity spawn — see §4h. |
| `NativeApi` | curated native/game-private operation registry — see §4i. |
| `SessionActivated` | the first member handshake completed. **Host side: never** — read the snapshot. |
| `PlayerJoined` / `PlayerLeft` | a member's handshake completed / a member was removed (host side). NOT the in-world entity join. Each member exactly once, including yourself. |
| `SessionEnded` | the session tore down. A guest's `PlayerLeft` for the host is NOT fired on host exit — only `SessionEnded`. |

## 4. Mod messages

`IModNetwork` — report/定向 semantics, star topology, **NO auto-relay**:

| Call | Host | Guest | Notes |
|---|---|---|---|
| `SendToHost(payload)` | no-op | reports to the host's copy of the mod | outside a session: no-op |
| `SendToPeer(steamId, payload)` | sends to one member's copy | no-op | |
| `Broadcast(payload)` | every member INCLUDING the host's own copy (local fire with its own SteamId) | no-op | the "all sides run this" call |
| `MessageReceived` | `(senderSteamId, payload)` — a report (guest) or the host's own broadcast | a directed/broadcast frame | |

The mod must declare `SendNetworkMessage`; undeclared sends are refused at the
sender (no-op + log) and undeclared receives are dropped. The payload is
**opaque**; unknown ids are dropped with a log. Frames are reliable; per-sender
rate limit: 20/s sustained with a 40-frame burst (`ModRateLimitPolicy`).

**64 KiB payload cap** — framework policy, NOT a line limit: refused at the
sender and re-checked at the receiver.

## 4b. Host commands

```csharp
context.Commands.Register(new ModCommand("heal", ctx =>
    $"healed {string.Join(" ", ctx.Arguments)} for {ctx.RequesterSteamId}",
    description: "Heal a member", isHostAction: true));
context.Commands.TryExecute("heal", new[] { "alice" }, result => { /* ... */ });
```

- **Host-authoritative execution**: the handler runs ONLY on the host's copy
  of the mod. A host call completes synchronously (callback before return); a
  guest call sends `ModCommandRequest` (NetMsg 86), the host validates and
  executes its own copy, and answers with a directed `ModCommandResult`
  (NetMsg 87) that settles the guest's pending callback by request id.
- Framework checks before execution: request shape caps (name ≤64, ≤16 args,
  each ≤256, total ≤4 KiB), sender is a handshaken member, mod id/command
  registered, permission flags, per-guest request rate limit (4/s, burst 8).
  The mod's handler remains the semantic validator and can authorize per-guest
  behavior from `ctx.RequesterSteamId`.
- Handler exceptions become `Success=false` results with the exception
  message; output is capped at 32 KiB (error 4 KiB). Pending guest callbacks
  are settled with a failure when the session ends or the framework shuts down.
- Guest-side result callbacks are reliable and directed to the requester only;
  unknown request ids are dropped with a log.

## 4d. Mod state (host-persistent saves)

```csharp
context.State.TrySet("loadout", bytes);       // host-only + WriteGameState
context.State.TryGet("loadout", out var bytes);
context.State.TrySetSchemaVersion(2);
```

- **Scope**: `IModState` is scoped to the mod id — a mod can only read/write
  its own entry. Values are opaque `byte[]`; the framework never interprets or
  serializes the mod payload, so the mod owns its schema/migration.
- **Host-only save authority**: `TrySet` / `TrySetSchemaVersion` / `TryRemove` /
  `TryClear` require the host role AND `ModPermission.WriteGameState`. A guest
  copy sees `CanWrite = false` and cannot read the host's table; a synchronized
  mod that needs host state coordinates through `IModNetwork` / `IModCommands`.
- **Persistence**: the host writes a versioned protobuf file under
  `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` (atomic temp+replace).
  Each write persists the full table; the in-memory table is process-scoped
  and loaded once before discovery/Bind. A missing file is empty; a corrupt or
  unknown-version file degrades to empty with a warning (never a startup
  crash, never a guessed migration).
- **Metadata**: the file carries mod id, mod version (last writer) and the
  mod-declared schema version. `SchemaVersion` defaults to 1; the framework
  stores it verbatim and does not migrate — migration policy belongs to the mod.
- **Missing-mod policy**: an entry for a mod that is not currently loaded is
  preserved untouched, so the data is still there if the mod returns.
- **Safety rails**: key length ≤128, ≤1024 keys per mod, value ≤64 KiB.
  Errors are refused with a log, never silently truncated.

## 4e. Mod UI (local windows)

```csharp
context.Ui.Register("status", "My Mod Status", window =>
{
    window.Label($"session active: {context.Session.SessionActive}");
    if (window.Button("ping"))
    {
        context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"));
    }
    var text = window.TextField(_lastText);
    _lastText = text; // the mod owns persistent UI state
});
context.Ui.Unregister("status");
```

- **Scope**: `IModUi` is a per-mod, local-only immediate-mode window registry.
  A mod registers an id + title + draw callback in `Bind`; CUO invokes the
  callback every frame and the Unity plugin owns all IMGUI/Unity details.
- **No permission**: a UI window cannot touch network, session, or
  game-authoritative state by itself, so every network mode may use it. Shared
  UI state still flows through `IModNetwork` / `IModCommands`; the window only
  projects that state locally.
- **Control alphabet**: `Label`, `Button` (returns true on click),
  `TextField` (returns the edited value; the mod owns persistence), and
  `Separator`. The set is deliberately tiny — no Unity types leak into
  `CUO.Abstractions`.
- **Rules**: empty id/title or a null draw callback is refused; a duplicate id
  within the same mod is refused; `Unregister` removes the window.
- **Failure isolation**: a mod draw callback that throws shows an inline error
  in the window and is logged by the plugin; it never breaks the UI frame.
- **No wire change**: local presentation only.

## 4f. Mod content (registration)

```csharp
if (context.Content.CanRegister)
{
    context.Content.TryRegister("wooden.sword", "item", myItemDefinitionBytes);
    context.Content.TryRegister("healing.recipe", "recipe", myRecipeDefinitionBytes, schemaVersion: 2);
}
var itemDefs = context.Content.Definitions; // snapshot; payloads are copied on read
context.Content.TryUnregister("wooden.sword");
```

- **Scope**: `IModContent` is a per-mod registry of opaque content
  definitions (item defs, weapon stats, NPC types, recipes, skills, map
  entries, etc.). A mod registers an id + kind + opaque payload in `Bind`;
  it may also declare a positive `schemaVersion` (defaults to 1). The
  framework never interprets, serializes, or migrates the payload, so the mod
  owns its own content schema/versioning; the stored version is carried
  verbatim on every definition read.
- **Permission**: registration requires `ModPermission.RegisterContent`.
  `CanRegister` reflects whether this mod copy declared the flag; every
  `TryRegister` call also enforces it. The permission policy already refuses
  that flag on `ClientOnly`/`Cosmetic` modes, so only state-bearing mods may
  register content.
- **Process-local**: content bytes do not travel over the wire. Content is
  part of the mod itself, so the existing Mod API handshake (mod id /
  SemVer / permissions / network mode) is the consistency boundary; a mod
  that needs client-specific dynamic content must coordinate through
  `IModNetwork` / `IModCommands` instead.
- **Rules**: empty id/kind, a null or over-cap payload, a non-positive
  schema version, or a duplicate id within the same mod is refused;
  `TryUnregister` removes a definition. Safety rails: id ≤128 chars,
  kind ≤64 chars, schema version must be positive, payload ≤64 KiB, ≤1024
  definitions per mod. Errors are refused with a log, never silently truncated.
- **Framework read view**: `IModContentControl.Entries` exposes every mod's
  registered definitions to other CUO layers (plugin / future native-content
  consumers) as a read-only snapshot. The runtime content catalog
  (`IModContentCatalog`) adds kind filtering, unique kind+id resolution, and
  cross-mod duplicate/schema-version conflict diagnostics without
  interpreting payloads; it is the intended base for a future native-content
  binder.
- **No wire change**: no content bytes or new NetMsg.

## 4g. Read game state (read-only projection)

```csharp
if (context.GameState.CanRead)
{
    if (context.GameState.TryGetPlayer(steamId, out var player))
    {
        var hp = player.Vitals?.BrainHealth;
        var items = player.Inventory?.Items;
    }
}
```

- **Scope**: a read-only projection of the latest framework-held **player
  character state** already arriving on the 1 Hz character stream. It is the
  same data source the built-in Online UI uses; a mod never sees Unity objects
  or game-assembly types.
- **Permission**: reading requires `ModPermission.ReadGameState`.
  `CanRead` reflects whether this mod copy declared the flag, and every
  `TryGetPlayer` call also enforces it (returns false with a log otherwise).
- **Exposed shape**: `IModPlayerState` carries `SteamId`, `InWorld`,
  `Vitals` (`BrainHealth`, `Hunger`, `Thirst`, `Stamina`, `Energy`,
  `Temperature`, `Alive`, `Conscious`) and `Inventory` (recursive
  `IModInventoryEntry` tree: instance id, item id, slot/wear index, condition,
  favourite flag, container contents). A missing half is null until its
  snapshot arrives.
- **Live read, immutable snapshot**: each `TryGetPlayer` call returns the
  latest cached facts at that moment; the returned objects are copies and can
  be held safely. A remote leaving the world or the session ending clears the
  cache.
- **Not in this slice**: the local player's own character state is not exposed
  through `IModGameState`; it is available through the native API's read-only
  local-player projection (see §4i). World/item/block/entity global state is
  not exposed yet. The same projection pattern is the forward path for those
  slices.
- **No wire change**: this surface only projects data that already arrives.

## 4h. Entity spawn

```csharp
if (context.EntitySpawn.CanSpawn)
{
    context.EntitySpawn.TrySpawn("landmine", 10f, 20f, 45f);
}
```

- **Scope**: `IModEntitySpawn` lets a synchronized/authoritative mod create a
  runtime world entity by the game's prefab id (`BuildingEntity.id`, the same
  id `Utils.Create` accepts). The method signature is the full public surface
  for this slice: prefab id + world X/Y + z rotation. No Unity or
  game-assembly type crosses the boundary.
- **Permission**: spawning requires `ModPermission.SpawnEntity`.
  `CanSpawn` reflects the declared flag; every `TrySpawn` call also enforces it
  (false + log otherwise). The permission policy already refuses that flag on
  `ClientOnly`/`Cosmetic`, so only state-bearing mods may spawn entities.
- **Runtime gates**: `TrySpawn` additionally requires an active session and the
  local player to be in-world (`LocalInWorld`); a spawn outside a live world is
  refused, not silently ignored.
- **Replication reuses the runtime-entity channel**: the Game Adapter creates
  the local `BuildingEntity` copy via `Utils.Create`; the normal
  `BuildingEntity.Start` report path then sends the existing
  `EntitySpawned` message (guest → host or host → broadcast), so every side
  creates the same prefab at the same position/rotation with the same
  creation-time data handling (geyser liquid type, keypad code, crystal tint)
  as any native runtime spawn. No new `NetMsg`; the existing `EntitySpawned` path is used.
- **Failure isolation**: invalid prefab ids, non-finite positions, an out-of-
  world session, a missing permission, or an adapter rejection (unknown prefab
  / non-`BuildingEntity` prefab) return false and are logged; a non-entity
  prefab created by the adapter is destroyed, never left as a local-only ghost.
- **Boundary (this slice)**: only existing game `BuildingEntity` prefabs are
  supported. This is a spawn/replication surface, not a generic custom-
  component/state-injection mechanism — a mod that needs per-entity custom
  data still coordinates through `IModNetwork` / `IModCommands` or registers
  it as static content (`IModContent`).
- **No wire change**: no new `NetMsg`.

## 4i. AccessNativeApi (curated native operation registry)

```csharp
if (context.NativeApi.CanAccess)
{
    // Generic operation registry (Game Adapter-curated; not arbitrary reflection)
    if (context.NativeApi.TryInvoke("local.player.state", [], out var raw))
    {
        var state = (IModNativeLocalPlayerState)raw;
        var hp = state.BrainHealth;
    }

    // Typed convenience for the same registered operation
    if (context.NativeApi.TryGetLocalPlayerState(out var local))
    {
        var x = local.X;
        var y = local.Y;
    }
}
```

- **Scope**: `IModNativeApi` is a permission-gated registry of named native
  operations. The Runtime never exposes arbitrary reflection or direct access
  to game assemblies; only the Game Adapter registers operations, and only
  those operation ids are invokable.
- **Permission**: invoking requires `ModPermission.AccessNativeApi`.
  `CanAccess` reflects the declared flag; every invoke method also enforces it
  (false + log otherwise).
- **Safe value surface**: arguments and results are restricted to `null`,
  strings, numeric primitives, capped `byte[]` / primitive arrays, and
  framework DTO types (currently `IModNativeLocalPlayerState`). Unity objects,
  game-assembly objects, and arbitrary object graphs are refused before and
  after the Game Adapter seam — they never cross to a mod.
- **Registered operation (this slice)**: `local.player.state`
  (`ModNativeApiOperations.LocalPlayerState`) returns the local player body's
  position, vitals, consciousness, and derived alive/conscious flags as
  `IModNativeLocalPlayerState`. It is read-only and local-only; no wire
  message, no authority change.
- **Policy boundary**: the first slice is deliberately read-only. Write /
  native-mutation operations are not registered until a concrete consumer
  exists and its sync/authority boundary is designed. This is the explicit
  escape-hatch policy decision: a curated allowlist, never open reflection.
- **No wire change**: this surface only reads local game state.

## 5. Handshake consistency (how sessions stay coherent)

The guest's declared mod list rides the handshake (`HandshakeMsg.Mods`). The
host validates BEFORE the member is created:

| Host has | Guest has | Verdict |
|---|---|---|
| RequiresAllPlayers / Synchronized / Authoritative | missing, SemVer-precedence-unequal, or permission-unequal | **reject** |
| any mode | same id but a different NetworkMode while either side is state-bearing | **reject** |
| HostOnly | missing | pass (host-side logic) |
| ClientOnly / Cosmetic | missing / different version | pass (local surfaces) |
| — | claims RequiresAllPlayers / Synchronized / Authoritative the host lacks | **reject** |
| — | malformed list (empty/duplicated id, invalid mode/permissions, unparseable state-bearing version) | **reject** |
| discovery not yet run | anything | **"pending" refusal** — the guest's 1 s retry re-runs the check |

Versions are strict SemVer. For state-bearing modes the comparison is
**precedence equality** (build metadata ignored); compatibility ranges are
deliberately not inferred until a formal API-compatibility contract exists.

## 6. Reference layout of a mod

```
BepInEx/plugins/MyMod/MyMod.dll        ← BepInEx loads this (the shell)
```

The shell (`[BepInPlugin]` + empty `BaseUnityPlugin`), the `[CuoMod]` class,
and the manifest metadata travel in ONE assembly. Copy the example:
`src/CasualtiesUnknownOnline.ModExample/` (it registers `echo`/`whoami`
commands and remains the two-process verification target).

## 7. Versioning and protocol discipline

- `ProtocolVersion.Current` is `1`. The pre-release protocol-version sequence
  was deliberately reset before first release (tech-decisions #137); earlier
  numbers such as 10/29/34 in this document are historical and must not be used
  as current wire versions.
- Behavioral wire changes after the first release will bump
  `ProtocolVersion.Current`; local-only/read-only mod surfaces that add no wire
  change do not bump it.
- Mod versions are strict SemVer strings, validated at discovery and compared
  by precedence for state-bearing modes.
- The 64 KiB cap is a policy constant (`ModChannel.MaxPayloadBytes`); raising
  it is a protocol-adjacent decision, not a wire format change.

## 8. Tests and verification

All mod behavior is covered by pure-managed tests over the production stack
(`tests/.../Mods/`): discovery + dependency ordering (`ModDiscoveryTests`),
permission policy (`ModPermissionPolicyTests`), SemVer (`SemanticVersionTests`),
lifecycle (`ModLifecycleTests`), message routing + permission/rate gates
(`ModMessageTests`), host commands (`ModCommandTests`), mod-state saves
(`ModStateTests`), local mod UI (`ModUiTests`), mod content registration
(`ModContentTests`, `ModContentCatalogTests`), read game state (`ModGameStateTests`), entity spawn
(`ModEntitySpawnTests`), native API (`ModNativeApiTests` +
`GameAdapterNativeApiContractTests`), handshake matrix
(`ModHandshakeTests`), rate limiter (`ModRateLimiterTests`), direction rows
(`DirectionTests`) and wire round-trips (`ModHandshakeProtocolTests`).

The example mod doubles as the **two-process runtime verification target**:
deploy it to both machines, join, and the logs show `[Mods] discovered
cuo.example ...`, the handshake admitting the pair (Synchronized, equal
version/permissions), guest→host command results (`ModCommandRequestHandler`/
`ModCommandResultHandler ... success True`), and the echo round-trip
(`[Example] echo from <steamId>`).
