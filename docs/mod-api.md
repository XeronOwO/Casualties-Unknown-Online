# CUO Mod API — Phase 4 (Core Skeleton + Host Commands & Permissions)

Status: first round **landed 2026-08-13**; second round (4b) **landed
2026-08-16**. This document is the binding contract for mod authors AND for
the framework's future rounds — the semantics below are locked by tests
(`tests/.../Mods/`, 761 total green at landing) and the two-process runtime
verification (host + sandbox guest, ProtocolVersion 10).

## 1. Scope

**First round**: discovery, lifecycle, manifest, mod network messages,
session events, handshake consistency.

**Second round (4b)**: the full permission model (declaration + enforcement
for the live surfaces), host-authoritative commands, dependency ordering,
SemVer versions, and per-sender rate limits.

**UI is landed (see §4e).** Still NOT landed (recorded TODO): content
registration, custom entities.
The mod surface lives in **`CUO.Abstractions`** — the ONLY
assembly mods may reference (architecture.md §5.5). A mod never touches
BepInEx, Steamworks, the game assemblies, or CUO.Runtime. **Mod-state saves
are landed (see §4d), and the local mod UI surface is landed (see §4e).**

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
  `WriteGameState` gates host-persistent mod-state writes (`IModState`).
  The remaining flags are carried through the handshake and are pre-declared
  for their future surfaces.
- **`Dependencies`** are mod ids loaded before the dependent; missing or
  cyclic dependencies reject the dependent (transitive failures propagate).
- **`ICuoMod : ICuoService`** — the standard lifecycle, driven by the
  framework's pump on the Unity main thread. Every stage is exception-isolated.
- **`IModContext`** — `Logger`, `Network`, `Commands`, `Session` and events:

| Member | Semantics |
|---|---|
| `Session` | a **SNAPSHOT at bind time**, not a live view — the host never fires `SessionActivated` (it activated at lobby creation), and events fired before discovery are lost. The snapshot is the only reliable "current state". `MemberSteamIds` is the peer member set (the local peer is `LocalSteamId`). |
| `Commands` | host-authoritative commands — see §4b. |
| `State` | host-persistent per-mod state — see §4d. |
| `Ui` | local immediate-mode mod UI windows — see §4e. |
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
- **No wire change**: this is local presentation, so ProtocolVersion stays 29.

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

- `ProtocolVersion.Current` is bumped on any behavioral wire change. Current:
  **29** (v29 adds `TutorialClawStateMsg` (104), the host→guest tutorial-claw
  20 Hz presentation stream; a v28 peer does not render the remote claw flow).
- Mod versions are strict SemVer strings, validated at discovery and compared
  by precedence for state-bearing modes.
- The 64 KiB cap is a policy constant (`ModChannel.MaxPayloadBytes`); raising
  it is a protocol-adjacent decision, not a wire format change.

## 8. Tests and verification

All mod behavior is covered by pure-managed tests over the production stack
(`tests/.../Mods/`): discovery + dependency ordering (`ModDiscoveryTests`),
permission policy (`ModPermissionPolicyTests`), SemVer (`SemanticVersionTests`),
lifecycle (`ModLifecycleTests`), message routing + permission/rate gates
(`ModMessageTests`), host commands (`ModCommandTests`), local mod UI (`ModUiTests`), handshake matrix
(`ModHandshakeTests`), rate limiter (`ModRateLimiterTests`), direction rows
(`DirectionTests`) and wire round-trips (`ModHandshakeProtocolTests`).

The example mod doubles as the **two-process runtime verification target**:
deploy it to both machines, join, and the logs show `[Mods] discovered
cuo.example ...`, the handshake admitting the pair (Synchronized, equal
version/permissions), guest→host command results (`ModCommandRequestHandler`/
`ModCommandResultHandler ... success True`), and the echo round-trip
(`[Example] echo from <steamId>`).
