# CUO Mod API — Phase 4, First Round (the Core Skeleton)

Status: **landed 2026-08-13**. This document is the binding contract for mod
authors AND for the framework's future rounds — the semantics below are locked
by tests (`tests/.../Mods/`, 455 total green at landing).

## 1. Scope of the first round

A complete, usable mod core: **discovery, lifecycle, manifest, mod network
messages, session events, handshake consistency**. Explicitly NOT in this
round (recorded TODO for the next ones): content registration, custom
entities, host commands, UI, mod-state saves, dependency ordering, and the
full permission model (architecture.md §5 — only the message routing, the
64 KiB cap and the handshake matrix landed).

The mod surface lives in **`CUO.Abstractions`** — the ONLY assembly mods may
reference (architecture.md §5.5). A mod never touches BepInEx, Steamworks,
the game assemblies, or CUO.Runtime.

## 2. How a mod is loaded (read this — the timing is deliberate)

BepInEx 5 loads plugins **one by one, load-then-Awake, in a single loop** —
verified by IL (`Chainloader.Start`: `Assembly.LoadFile → GetType →
instantiate → Awake → next`) and by the game's own log (the CUO plugin's Awake
lines appear BEFORE `Loading [HotRepl]`). Two hard rules follow:

1. **Discovery runs on the framework's FIRST UPDATE FRAME**, not in its Awake
   — a scan in Awake would miss every plugin loaded after it. The
   `ModService` scans `AppDomain.GetAssemblies()` once, finds every
   `[CuoMod]`-declared `ICuoMod` type, validates it (see §3) and binds it.
   The discovery frame runs Bind → Initialize → Start → Update in that same
   frame; Update per frame from then on.
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
else); every line of business logic references CUO.Abstractions only. The
shell declares no behaviour, so no mod logic ever runs through BepInEx APIs.

## 3. The mod surface

```csharp
[CuoMod("com.example.mymod", "My Mod", "1.0.0", NetworkMode = NetworkMode.Synchronized, Description = "...")]
public sealed class MyMod : ICuoMod   // ICuoService lifecycle + Bind
{
    public void Bind(IModContext context) { ... }   // once, before Initialize
    // ICuoService: Initialize / Start / Update / Stop / Dispose
}
```

- **`[CuoMod]`** is the single manifest source (id / displayName / version /
  `NetworkMode` / description). `NetworkMode` defaults to `Unspecified` and is
  **rejected at discovery** — a mod that does not state its network contract
  does not load (fail-closed: a forgotten declaration can never silently
  degrade to the most permissive mode). Other rejection causes: duplicated id
  across assemblies, abstract/non-public type, missing public parameterless
  constructor. One rejected mod never blocks the scan (per-mod fail-closed).
- **`ICuoMod : ICuoService`** — the standard lifecycle, driven by the
  framework's pump on the Unity main thread. Every stage is exception-isolated
  (a throwing mod is logged and skipped; the pump and its siblings survive).
  `Dispose` runs once even though the container may dispose the service
  several times (idempotent, per the ICuoService contract).
- **`IModContext`** — `Logger` (mod-scoped, `[Mod:<id>]`), `Network`
  (the message channel), `Session` and the events:

| Member | Semantics |
|---|---|
| `Session` | a **SNAPSHOT at bind time**, not a live view — the host never fires `SessionActivated` (it activated at lobby creation), and events fired before discovery are lost. The snapshot is the only reliable "current state". `MemberSteamIds` is the peer member set (the local peer is `LocalSteamId`, same semantics as the broadcast fan-out). |
| `SessionActivated` | the first member handshake completed. **Host side: never** — read the snapshot. |
| `PlayerJoined` / `PlayerLeft` | a member's handshake completed / a member was removed (host side). NOT the in-world entity join — that is the entity domain's roster broadcast. Each member exactly once, including yourself. |
| `SessionEnded` | the session tore down. A guest's `PlayerLeft` for the host is NOT fired on host exit — only `SessionEnded`. |

## 4. Mod messages

`IModNetwork` — report/定向 semantics, star topology, **NO auto-relay**:

| Call | Host | Guest | Notes |
|---|---|---|---|
| `SendToHost(payload)` | no-op | reports to the host's copy of the mod | outside a session: no-op |
| `SendToPeer(steamId, payload)` | sends to one member's copy | no-op | |
| `Broadcast(payload)` | every member INCLUDING the host's own copy (local fire with its own SteamId) | no-op | the "all sides run this" call |
| `MessageReceived` | `(senderSteamId, payload)` — a report (guest) or the host's own broadcast | a directed/broadcast frame | |

The payload is **opaque** (the mod owns its serialization — UTF-8/JSON/
hand-written; the framework never interprets it). One shared frame carries the
sending mod's id; the receiving side routes by id to the local copy of that
mod and **drops unknown ids with a log**. Frames are reliable (order +
delivery guaranteed while the connection lives); idempotency of a repeated
delivery is the mod's own responsibility.

**64 KiB payload cap** — framework policy, NOT a line limit (Steam's ceiling
is 1 MB): a reliable frame this size is the worst case for head-of-line
blocking on a congested link, so a single mod must not saturate it. Refused at
the sender (the mod learns immediately) and re-checked at the receiver.

## 5. Handshake consistency (how sessions stay coherent)

The guest's declared mod list rides the handshake (`HandshakeMsg.Mods`,
ProtocolVersion 3 — behaviorally breaking, so mixed versions are refused by
the version gate instead of silently skipping the check). The host validates
BEFORE the member is created:

| Host has | Guest has | Verdict |
|---|---|---|
| RequiresAllPlayers / Synchronized / Authoritative | missing or version-unequal | **reject** |
| HostOnly | missing | pass (host-side logic) |
| ClientOnly / Cosmetic | missing / different version | pass (local surfaces) |
| — | claims RequiresAllPlayers / Synchronized / Authoritative the host lacks | **reject** (the host cannot arbitrate it) |
| — | malformed list (empty/duplicated id, `Unspecified` or unknown NetworkMode) | **reject** |
| discovery not yet run | anything | **"pending" refusal** — the guest's 1 s handshake retry re-runs the check (production handshakes take seconds anyway; the window is practically unreachable) |

A null list (an old client's frame) is treated as empty — and the version
gate refuses cross-version sessions regardless.

## 6. Reference layout of a mod

```
BepInEx/plugins/MyMod/MyMod.dll        ← BepInEx loads this (the shell)
```

The shell (`[BepInPlugin]` + empty `BaseUnityPlugin`), the `[CuoMod]` class,
and the manifest metadata travel in ONE assembly (or the shell in a tiny
loader assembly — either way the `[CuoMod]` class is what CUO instantiates).
Copy the example: `src/CasualtiesUnknownOnline.ModExample/`.

## 7. Versioning and protocol discipline

- `ProtocolVersion.Current` is bumped on any behavioral wire change. The mod
  list in the handshake was such a change (v2 → v3): the field itself is
  wire-compatible (protobuf skips unknown fields), but the CHECK is
  behavioral — mixed-version sessions must be refused loudly, not silently
  unchecked (architecture.md pitfall #7).
- Mod versions are exact strings, compared by equality in this round;
  semantic-version comparison is a recorded TODO.
- The 64 KiB cap is a policy constant (`ModChannel.MaxPayloadBytes`); raising
  it is a protocol-adjacent decision, not a wire format change.

## 8. Tests and verification

All mod behavior is covered by pure-managed tests over the production stack
(`tests/.../Mods/`): discovery purity (`ModDiscoveryTests`), lifecycle +
exception isolation + snapshot semantics (`ModLifecycleTests`), three-node
message routing with role guards (`ModMessageTests`), the full handshake
matrix with stubbed control surfaces (`ModHandshakeTests`), the direction row
(`DirectionTests`) and the wire round-trips (`ModHandshakeProtocolTests`). A
test `[CuoMod]` lives in the test assembly itself (discovered by every
TestNode — the registry skipping the malformed candidates is itself locked).

The example mod doubles as the **two-process runtime verification target**:
deploy it to both machines, join, and the logs show `[Mods] discovered
cuo.example ...`, the handshake admitting the pair (Synchronized, equal
versions), and the echo round-trip (`[Example] echo from <steamId>`).
