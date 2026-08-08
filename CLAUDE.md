# CLAUDE.md

Instructions for Claude Code and contributors working in this repository.

## Project Overview

**Casualties Unknown: Online (CUO)** — a multiplayer mod framework for the game *Casualties Unknown* (Demo), built on BepInEx. The base game ships with **no multiplayer support**; CUO adds Steam-based Host + Guests co-op (LAN / friends) by injecting a new multiplayer runtime and reorganizing the local-only game state into "host-authoritative simulation + guest input/state sync". Long-term ambition is a Forge-like mod ecosystem, but the immediate goal is a solid multiplayer core framework, not the full ecosystem.

- Stable **CUO Runtime** (network protocol, host/guest state machine, mod loading, serialization, tick/snapshot, logging, version negotiation)
- Replaceable **Game Adapter** per game build (the only layer that knows the game's private types)
- The game updates will NOT adapt to us — the adapter layer must absorb that churn.

Full architecture blueprint: [`docs/architecture.md`](docs/architecture.md)

## Repository Layout

```
src/CasualtiesUnknownOnline.Abstractions/  # CUO Abstractions (net48) — public API surface (ICuoService today, Mod API later); M.E. Abstractions/Options only; the ONLY package mods may reference
src/CasualtiesUnknownOnline.Runtime/  # CUO Runtime (net48) — DI + Logging + BepInEx integration + Steam networking + session layer (SessionService control plane, PacketGateway data plane, EntitySyncService + CharacterDataStore domain services, Session/Handlers/ per-message handlers); never references game assemblies
src/CasualtiesUnknownOnline.GameAdapter/  # CUO Game Adapter (net48) — the ONLY project referencing game assemblies; HarmonyX patches (input interception, body clones, world params)
src/CasualtiesUnknownOnline.Plugin/  # BepInEx 5 plugin entry (net48) — thin lifecycle driver: DI container assembly + ICuoService forwarding
CasualtiesUnknownOnline.slnx         # Solution (VS 2022)
references/                    # Game assemblies, gitignored, copied on demand (see references/README.md)
reversing/                     # Reverse-engineering workspace, gitignored (see reversing/README.md)
docs/architecture.md           # Architecture blueprint (full design, pitfalls, phases)
docs/krokmp-notes.md           # KrokMP reverse-engineering findings (API surface, feasibility)
CLAUDE.md                      # This file
CLAUDE.local.md                # Local personal notes — gitignored, never commit
```

## Build

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet format CasualtiesUnknownOnline.slnx   # MUST run before every commit
```

- **`dotnet format` is mandatory before every commit** — keeps code conforming to `.editorconfig` (user requirement; proven workflow in the JustUnknownCharacters mod). With `TreatWarningsAsErrors` enabled, style violations fail the build once fixed into the codebase.

- Target: `net48` (BepInEx 5 plugin requirement), `LangVersion` = `preview`, nullable enabled, warnings-as-errors
- NuGet sources: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (configured in csproj)
- Game assemblies (`references/`) are copyrighted and not in the repo — copy **on demand** from the game's `CasualtiesUnknown_Data\Managed\` per `references/README.md` (only the Game Adapter project may reference them; convention details in `docs/architecture.md`)
- Packaged plugin DLL is deployed into the game's `BepInEx/plugins/` folder (path is machine-local — see `CLAUDE.local.md`)

## Tech Decisions (binding)

- **BepInEx 5.x, not 6** — the game's installed community mod ecosystem (KrokMP-derived mods, JustUnknownCharacters, …) is BepInEx-5-only; BepInEx 6 cannot load 5.x plugins, so switching would break every existing mod. Revisit only if the ecosystem migrates.
- **net48 TFM** — the game's Mono runtime is .NET 4.x (mscorlib 4.0.0.0, `netstandard.dll` present); net48-targeted assemblies are verified to load in it (KrokMP's Steamworks.NET.dll is net48-targeted and runs fine). net48 was chosen over net452 because MSBuild drops references whose TFM exceeds the project's (MSB3274) — Steamworks.NET 2025.163 requires it. BepInEx 5 supports net48 plugins. Caveat: net48 target means we may only use BCL APIs the game's Mono actually implements — prefer conservative APIs.
- **UnityEngine via NuGet `UnityEngine.Modules 5.6.0`** (template default, matches the game's Unity 5.6 era); **game assemblies via `references/` on demand** (see `references/README.md`). Official docs warn against referencing assemblies directly from the game folder — copy them into the project first, which is exactly what the references/ convention does.
- **Plugin metadata**: `[BepInProcess("CasualtiesUnknown.exe")]` should be added once the plugin does anything game-specific; GUID is permanent — never change it after release. Logging tags (`[Network]` etc.) map to `Logger.CreateLogSource(tag)` sources, not string prefixes.
- **Microsoft.Extensions as CUO infrastructure (landed 2026-08-06, architecture.md §5.5)**: DI (`Microsoft.Extensions.DependencyInjection`) + `ILogger<T>` bridged to BepInEx logging + `Options` as config abstraction. Never the Generic Host / `BackgroundService` — BepInEx/Unity own the lifecycle; CUO receives lifecycle notifications via a small `ICuoService` interface (Initialize/Start/Update/Stop/Dispose) in `CUO.Abstractions`. Package layering: `CUO.Abstractions` (Abstractions/Options only — this is all Mods may reference) → `CUO.Runtime` (DI+Logging+BepInEx integration) → `CUO.GameAdapter` (game refs + Harmony, future). Mods never reference BepInEx/Steamworks/game assemblies directly. Pinned to **3.1.32** (net452-compatible line); verified transitive closure ships 6 `System.*` DLLs (Memory/Buffers/Numerics.Vectors/Unsafe/Tasks.Extensions/ComponentModel.Annotations) — CUO owns these DLLs centrally, mods never ship their own. Rolling file log at `<game>/BepInEx/logs/` (`latest.log` rotated to `yyyy-MM-dd-N.log.gz` on startup); `deploy.ps1` copies all build-output DLLs, never BepInEx-owned ones. Options' BepInEx ConfigFile adapter and `CUO.GameAdapter` still future.
- **Steamworks.NET**: referenced locally from `references/` (2025.163.0, taken from the KrokMP install — same DLL verified running in this game). Latest NuGet releases are netstandard2.1-only (incompatible with net48); direct DLL reference sidesteps the TFM check. The game has NO Steam integration of its own (verified by reversing) — CUO is the sole SteamAPI initializer, so no duplicate-init conflict.
- **HarmonyX (0Harmony 2.9.0)**: the game's BepInEx/core owns 0Harmony.dll 2.9.0 (BepInEx fork); nuget.org's `Lib.Harmony` stops at 2.4.2. Referenced directly from `references/` (same convention as Steamworks.NET) so compile-time = runtime version; never deployed (deploy.ps1 excludes it). Patches live in `CUO.GameAdapter` only.
- **protobuf-net 3.2.56 as the wire serialization layer (landed 2026-08-07)**: every session message is a `[ProtoContract]` class in `Runtime/Protocol/Messages/`; the frame stays `[msgId:1][protobuf payload]`. Evaluated Lagrange.Core's Proto module first (net48 port feasible but SIMD intrinsics were a runtime unknown on this game's Mono, GPL-3.0-or-later conflicts with CUO's MIT, upstream tracks net8/9/10) — dropped for protobuf-net: mature, net48-official (net462 asset), Apache-2.0 (MIT-compatible), and the 3.x line is span-based. All hand-written BinaryWriter layouts in SessionService are deleted.
- **Wire transport: reliable events, unreliable state stream (landed 2026-08-07)**: message channel choice follows "can the loss self-heal", not "event vs state". One-shot semantics (Handshake/HandshakeAck/PlayerJoin/SceneState/WorldStartParams/BlockDamaged/CharacterData) go reliable (`k_nSteamNetworkingSend_Reliable`) — guaranteed arrival + order while the connection lives (drops only happen when the connection itself dies, which is disconnect handling, not packet loss). The 20 Hz state stream (PlayerState/PlayerStateReport) goes unreliable with a snapshot `Seq` — the receiver drops stale/duplicate snapshots, and drops are harmless because the next tick overwrites (render layer already Lerp-interpolates). Rationale: reliable on the stream causes head-of-line blocking — the newest snapshot queues behind retransmissions of old ones on a congested link. Never blindly retry a non-idempotent event (a BlockDamaged retransmit double-hits the block); retries require idempotency keys. Sending a message while the lazy Steam P2P session has not established yet may silently fail — handshake retries cover that window. **Topology is pure star (host-authoritative), no envelope**: every message goes guest→host; the host validates, arbitrates and decides who (if anyone) gets it (per-recipient SendTo fan-out, excluding the source for echoed events like BlockDamaged — the source already applied it locally). No guest↔guest direct traffic, no src/dst/broadcast-flag envelope: the transport layer supplies the sender, the SendTo call expresses the destination, EntityId inside the message expresses ownership, Seq in the state stream handles reordering. Frame stays `[msgId:1][protobuf payload]`.
- **Session-layer architecture: per-message handlers + data/control-plane split + domain services (landed 2026-08-08)**: message handling is one class per message (`Runtime/Session/Handlers/`, `[PacketHandler(NetMsg.X)]` + `PacketHandlerBase<TPacket>` where T provides the default protobuf decode); CuoBootstrap reflects the Runtime assembly into DI and `PacketRouter` builds a read-only `Dictionary<NetMsg, IPacketHandler>` at startup — O(1) runtime dispatch, no switch. Data plane vs control plane: `PacketGateway` owns the transport binding, frame encode/decode, direction validation and dispatch; `SessionService` is the control plane (member **presence** table `MemberPresence` — SteamId/Handshaken/InWorld/ReportedSpawnPos/RttMs, handshake lifecycle, scene reports, world params, diagnostics, business-level send/receive APIs); `SessionIdentity` (lobby-bound role, never cleared by EndSession) keeps the dependency graph acyclic — **plain constructor injection everywhere, no post-build AttachXxx wiring** (constructor cycles are solved by abstract extraction or Lazy — user rule, see user-level CLAUDE.md Architecture Preferences). Entity/data domains hang off the session one-way: `EntitySyncService` (the entity table — buffers/ids/sync decisions, the 20 Hz state send/report throttling, PlayerJoin self+roster+first-snapshot fan-out; reads the presence table, notified of removals via the session's `MemberRemoved` event) and `CharacterDataStore` (SteamID-keyed character save/restore, no pump, not an ICuoService). The domain split is the Phase 3 landing pattern — world state and inventory extend the same shape. One top-level type per file (convention #8); protocol message classes split one-per-file under `Runtime/Protocol/Messages/`.

## Architecture in Brief

Layered, dependency flows downward only:

```
Mods → Mod Framework API → Multiplayer Runtime → Game Adapter → BepInEx/Unity/Steam
```

Non-negotiable design rules:

- **Local compute, remote verify/sync (user mandate)**: every player simulates its own actions locally with full single-player feel (movement, mining, aiming, posture); results are exchanged as sync data (state reports, block-damage messages) for the peer to apply and verify. The host NEVER simulates the guest's per-frame behavior, and guests DO mutate game objects locally — the mutation is then synced. "Host authority" is limited to global world-state ownership (world-gen seed, saves, authoritative rulings, anti-cheat verification). Never gate gameplay behind missing sync — solve divergence with sync mechanisms, not by intercepting the action.
- **Sync semantics, not Transforms**: synchronize game-semantic state ("player is attacking", "door opened"), not raw Transform/GameObject state.
- **Custom network entity IDs**: Unity instance IDs are process-local; define `NetworkEntityId` (session epoch + host allocation counter + generation).
- **Steam Lobby ≠ transport**: Lobby is for discovery/roster; game data goes over Steam networking (MVP: `ISteamNetworkingMessages`; later: `ISteamNetworkingSockets`). Never expose Steam APIs to mods — abstract as `INetworkTransport` / `ISession` / `IPeer` / `INetworkChannel`.
- **Main-thread marshaling**: network/Steam callbacks never touch Unity objects; marshal to the Unity main thread.
- **No Host Migration in MVP**: host exit → session ends → guests return to lobby.
- **Safe degradation**: capability detection at startup; grades `Compatible` / `CompatibleWithWarnings` / `Unsupported` / `CriticalFailure`. Never let a failed patch silently run.
- **Prefer HarmonyX**; Mono.Cecil only when assembly structure must change. Feature-scan game APIs instead of hardcoding offsets/private fields.
- **Host is the only save authority**; guests keep local settings only. Mod save data requires mod id/version/schema version + migration policy.
- **KrokMP compatibility (reserved, not near-term)**: many community mods target the legacy KrokMP API; plan an optional compat adapter mapping KrokMP API → CUO API (API-level only, separate module, never pollutes CUO Core). Not before Phase 4 + real migration demand; catalog KrokMP's API surface during reversing (see `docs/architecture.md` §5.4).

## Development Phases (current: Phase 3 — game core loop; star network landed 2026-08-08)

**Phase 0 is COMPLETE** (verified 2026-08-05 with dual Steam accounts, host + sandboxed guest): BepInEx loads the plugin, Steam initializes at plugin load, lobby create/join works, ISteamNetworkingMessages ping/pong works end-to-end (RTT ~15-25ms on local loopback). Key finding: `k_EResultConnectFailed` is transient — the Steam P2P session establishes lazily (~30s); persistent retry is the correct strategy, not a transport failure. Phase-0 test keys live in Plugin.cs (F8/F9/F7) — remove when the real lobby UI lands.

1. **Phase 0 — Feasibility**: BepInEx loads → Steam Lobby create/join → network ping/pong → SteamID readout → safe disconnect.
2. **Phase 1 — Single player entity**: join/leave, position, heading, basic input, scene load state. Sync model landed (2026-08-06, user mandate "本地计算,远程校验/同步"): **each player simulates ONLY its own body locally** (input is never intercepted — the original HandleInput runs unchanged on the guest); peer state exchanged at 20 Hz (`PlayerState` host→guest, `PlayerStateReport` guest→host) plus pose flags (sitting/sleeping/lying/climbing); world mutations are local-first (`BlockDamaged` messages); the remote player is a frozen render clone (physics off, animations on) fed by the state stream. Host authority covers only global world-state ownership (world-gen seed, saves, authoritative rulings), never per-frame player simulation. Details in `docs/game-internals.md` §Sync Model. **COMPLETE** (verified 2026-08-06/07, incl. Steam friends "Join Game" auto-join via `+connect_lobby` + `GameLobbyJoinRequested_t`).
3. **Phase 2 — Entity lifecycle**: spawn/despawn/respawn, Entity IDs, scene switches, snapshot resend. **COMPLETE** (verified 2026-08-08): scene-switch sync, snapshot resend, disconnect+reconnect (session outlives disconnect via lobby-bound Role; character save/restore per SteamID), death pose sync, entity-id validation, full SaveSystem-aligned character data (Body 61 + Limb 19 fields, Mapster-mapped).
4. **Phase 3 — Game core loop**: interaction, inventory, combat, NPCs, quests, world time, saves — each with explicit host-authoritative vs guest-render logic. **Star network landed (2026-08-08, groundwork)**: member table (SteamId-keyed, both sides — guests render every member from the host's broadcast entity list), roster PlayerJoin + PlayerLeave, per-member state streams (seq per member), behavior packets (BlockDamaged/SceneState = report → host arbitrate → fan-out excluding the source), GameAdapter per-member clone map (lazy per-frame ensure), world-defining params consumed (biome override/depth, total traveled), arbitration feedback tiers defined in `docs/architecture.md` §3 (silent tier implemented; reject+correction lands with pickup). A session-layer domain refactor followed (per-message handlers, data/control-plane split, entity-sync + character-data domains — see Tech Decisions "Session-layer architecture"). Remaining modules build on this pattern; pickup arbitration needs item instance IDs first.
5. **Phase 4 — Public Mod API**: content registration, custom entities, network messages, host commands, UI, mod-state saves, mod dependencies, manifest validation.
6. **Phase 5 — Tooling & ecosystem**: mod manager, auto-install, crash reports, network diagnostics, conflict detection, host migration, dedicated server.

Do not jump ahead: build Phase 3 modules one at a time on the star-network pattern; each needs explicit host-authoritative vs guest-render logic. MVP explicitly excludes: host migration, dedicated server, auto mod install, generic physics sync, client prediction, full anti-cheat.

## Engineering Conventions (binding)

1. **English by default** — all code, comments, and committed docs are written in English. Exceptions: I18N artifacts (multi-language docs, resource files).
2. **Modern idiomatic C#** — use the latest C# language features and idiomatic C# style; `LangVersion = preview`, nullable reference types enabled. Note: BepInEx APIs lack nullability annotations — use `!` assertions at boundary calls instead of disabling the feature. Prefer `var` unless the type must be written out (declaration-then-assignment, collection initializers); avoid fully-qualified names unless necessary (use `using` aliases instead). Code style is **strict mode**: `.editorconfig` rules are error-severity and enforced at build time (`EnforceCodeStyleInBuild`), so imperfect code does not build — `dotnet format` fixes what it can, remaining violations must be fixed manually. One convention is documented but NOT build-enforced because the CLI analyzers cannot see it: `is null` / `is not null` over `== null` / `!= null` (IDE0041 is IDE-only) — write it that way in new code and fix it when touching old code. **Unity objects are the exception**: `Body`/`PlayerCamera`/`WorldGeneration`/cloned proxies MUST use `== null` / `!= null` — UnityEngine.Object overloads the operators to detect scene-reload-destroyed objects, while `is null`/`?.` (reference comparison) misses them and access then throws `MissingReferenceException`. IDE0031 (which would rewrite those checks into `?.`) is disabled in `.editorconfig` for this reason.
3. **Self-learning mechanism** — when valuable, *generalizable* knowledge emerges during work (reusable workflows, non-obvious game-internals findings, hard-won conventions), record it in `CLAUDE.md`, `docs/`, or a project-level skill, and commit it with git. Be selective: the knowledge must have real reusability value beyond the moment — do not record random observations. Don't create project skills before actual workflows exist to distill them from.
4. **Clean git hygiene** — commits never contain build artifacts/binaries, personal preferences, machine/environment specifics, or secrets. Committed files (code, comments, docs, examples) must never contain real machine paths — no drive letters, usernames, or install roots like `E:\SteamLibrary\...`; use placeholders (`C:\path\to\game`, `<game-dir>`). Real paths live only in the gitignored `CLAUDE.local.md`. Route everything else through `.gitignore`.
5. **Requirement triage** — when the user states a requirement, judge whether it is reusable and long-lived: personal/specific → record in `CLAUDE.local.md`, keep out of commits; shared/project-benefiting → record and commit (`CLAUDE.md`, docs, skills); **ambiguous → ask the user** whether it's personal or shared.
6. **Architecture & maintainability first** — fix root causes, not symptoms; when fixing a problem, consider whether a better design exists; for risky or architecture-affecting changes, propose and get user consent before acting.
7. **Evidence-based changes (user requirement)** — every change must be grounded in code: find the mechanism in the decompiled sources (`reversing/`, cite file:line) before touching it. Never fix by intuition/guesswork. When a mechanism is not understood, isolate and verify it (e.g. test a clone in single-player) instead of stacking network-test variables. A proposed fix must explain ALL observed symptoms, not just the surface one.
8. **One top-level type per file (user requirement, 2026-08-08)** — a `.cs` file holds exactly one top-level type; the file name matches the type name. Nested types (private helpers like loggers, `MemberState`, Harmony patch classes nested in their container) are part of the container and stay. This was enforced by splitting the protocol message classes, session enums, packet handlers and world-gen patches into one-file-one-type.

## Context Compression Protocol

When the user says "compress", do this before the context is compressed:

1. Review the conversation for durable knowledge and persist it: update memory files (`~/.claude/projects/.../memory/`) and/or `CLAUDE.md`/`docs/` per the self-learning rule (convention 3).
2. Produce a **compression prompt** whose core is a session-cut instruction: which conversation turns to **keep** (recent operational turns, decision turns whose rationale isn't fully captured in files) and which to **drop** (turns fully persisted to memory/docs — files are the truth; repetitive debugging round-trips; documentation drafting). Add only a short pointer block (project state, current task, pointers) so the next round can continue.
3. Hand the prompt to the user — they run the compression and development continues in the next round.

## Known Pitfalls (details in docs/architecture.md)

- Treating Steam P2P as plain LAN UDP — the two modes are different; don't mix them.
- Syncing all Transforms — fails on physics, parenting, animation, nav, rigidbodies, scene loads.
- Over-reliance on hardcoded offsets/private fields — breaks on every game update; use feature scanning + per-version adapters.
- Premature Mono.Cecil — prefer HarmonyX patches; preloader changes are hard to maintain and conflict-prone.
- Harmony patch state leakage — a Prefix that clears an instance field to bypass logic must have its Postfix restore it (even when empty), or the game field stays corrupted.
- Ignoring the main thread — network/Steam callbacks must not touch Unity APIs.
- `System.Memory` hijacks `.Reverse()` — with `using System;`, `array.Reverse()` binds to `MemoryExtensions.Reverse(Span<T>, void)` instead of LINQ's `Enumerable.Reverse` (arrays convert implicitly to `Span<T>` and Span wins overload resolution). Symptom: CS1579 "foreach cannot operate on void". Use reverse-index loops or explicit `Enumerable.Reverse(...)`.
- No mod consistency checks — implement handshake + manifest validation in the MVP.
- Undefined failure modes — define behavior for: Steam disconnect, guest/host dropout, mod load failure, scene-switch timeout, corrupt snapshot, version mismatch, mid-game join. Prefer "safe exit, no save corruption" over "try to recover".
- Licensing/legal — verify game EULA (injection allowed?), anti-cheat, Steamworks DLL redistribution, BepInEx (LGPL-2.1) and dependency licenses before any distribution.
