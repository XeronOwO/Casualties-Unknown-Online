# CLAUDE.md

Instructions for Claude Code and contributors working in this repository.

## Project Overview

**Casualties Unknown: Online (CUO)** — a multiplayer mod framework for the game *Casualties Unknown* (Demo), built on BepInEx. The base game ships with **no multiplayer support**; CUO adds Steam-based Host + Guests co-op (LAN / friends) by injecting a new multiplayer runtime and reorganizing the local-only game state into "host-authoritative simulation + guest input/state sync". Long-term ambition is a Forge-like mod ecosystem, but the immediate goal is a solid multiplayer core framework, not the full ecosystem.

- Stable **CUO Core** (network protocol, host/guest state machine, mod loading, serialization, tick/snapshot, logging, version negotiation)
- Replaceable **Game Adapter** per game build (the only layer that knows the game's private types)
- The game updates will NOT adapt to us — the adapter layer must absorb that churn.

Full architecture blueprint: [`docs/architecture.md`](docs/architecture.md)

## Repository Layout

```
src/CasualtiesUnknownOnline.Core/    # CUO Core class library (net452) — network, session, logging; never references game or BepInEx
src/CasualtiesUnknownOnline.Plugin/  # BepInEx 5 plugin entry (net452) — references Core, hosts the BepInEx log sink
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

- Target: `net452` (BepInEx 5 plugin requirement), `LangVersion` = `preview`, nullable enabled, warnings-as-errors
- NuGet sources: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (configured in csproj)
- Game assemblies (`references/`) are copyrighted and not in the repo — copy **on demand** from the game's `CasualtiesUnknown_Data\Managed\` per `references/README.md` (only the Game Adapter project may reference them; convention details in `docs/architecture.md`)
- Packaged plugin DLL is deployed into the game's `BepInEx/plugins/` folder (path is machine-local — see `CLAUDE.local.md`)

## Tech Decisions (binding)

- **BepInEx 5.x, not 6** — the game's installed community mod ecosystem (KrokMP-derived mods, JustUnknownCharacters, …) is BepInEx-5-only; BepInEx 6 cannot load 5.x plugins, so switching would break every existing mod. Revisit only if the ecosystem migrates.
- **net48 TFM** — the game's Mono runtime is .NET 4.x (mscorlib 4.0.0.0, `netstandard.dll` present); net48-targeted assemblies are verified to load in it (KrokMP's Steamworks.NET.dll is net48-targeted and runs fine). net48 was chosen over net452 because MSBuild drops references whose TFM exceeds the project's (MSB3274) — Steamworks.NET 2025.163 requires it. BepInEx 5 supports net48 plugins. Caveat: net48 target means we may only use BCL APIs the game's Mono actually implements — prefer conservative APIs.
- **UnityEngine via NuGet `UnityEngine.Modules 5.6.0`** (template default, matches the game's Unity 5.6 era); **game assemblies via `references/` on demand** (see `references/README.md`). Official docs warn against referencing assemblies directly from the game folder — copy them into the project first, which is exactly what the references/ convention does.
- **Plugin metadata**: `[BepInProcess("CasualtiesUnknown.exe")]` should be added once the plugin does anything game-specific; GUID is permanent — never change it after release. Logging tags (`[Network]` etc.) map to `Logger.CreateLogSource(tag)` sources, not string prefixes.
- **Microsoft.Extensions as CUO infrastructure (planned)**: DI (`Microsoft.Extensions.DependencyInjection`) + `ILogger<T>` bridged to BepInEx logging + `Options` as config abstraction. Never the Generic Host / `BackgroundService` — BepInEx/Unity own the lifecycle; CUO receives lifecycle notifications via a small `ICuoService` interface (Initialize/Start/Update/Stop/Dispose). Package layering: `CUO.Abstractions` (Abstractions/Options only — this is all Mods may reference) → `CUO.Runtime` (DI+Logging+BepInEx integration) → `CUO.GameAdapter` (game refs + Harmony). Mods never reference BepInEx/Steamworks/game assemblies directly. Pin conservative Microsoft.Extensions versions compatible with net452 (3.1.x line supports net452); CUO owns the DLLs, mods never ship their own. See `docs/architecture.md` §5.5.
- **Steamworks.NET**: referenced locally from `references/` (2025.163.0, taken from the KrokMP install — same DLL verified running in this game). Latest NuGet releases are netstandard2.1-only (incompatible with net452); direct DLL reference sidesteps the TFM check. The game has NO Steam integration of its own (verified by reversing) — CUO is the sole SteamAPI initializer, so no duplicate-init conflict.

## Architecture in Brief

Layered, dependency flows downward only:

```
Mods → Mod Framework API → Multiplayer Runtime → Game Adapter → BepInEx/Unity/Steam
```

Non-negotiable design rules:

- **Host authority**: the host runs the simulation; guests submit input/commands only. Guests never mutate game objects directly.
- **Sync semantics, not Transforms**: synchronize game-semantic state ("player is attacking", "door opened"), not raw Transform/GameObject state.
- **Custom network entity IDs**: Unity instance IDs are process-local; define `NetworkEntityId` (session epoch + host allocation counter + generation).
- **Steam Lobby ≠ transport**: Lobby is for discovery/roster; game data goes over Steam networking (MVP: `ISteamNetworkingMessages`; later: `ISteamNetworkingSockets`). Never expose Steam APIs to mods — abstract as `INetworkTransport` / `ISession` / `IPeer` / `INetworkChannel`.
- **Main-thread marshaling**: network/Steam callbacks never touch Unity objects; marshal to the Unity main thread.
- **No Host Migration in MVP**: host exit → session ends → guests return to lobby.
- **Safe degradation**: capability detection at startup; grades `Compatible` / `CompatibleWithWarnings` / `Unsupported` / `CriticalFailure`. Never let a failed patch silently run.
- **Prefer HarmonyX**; Mono.Cecil only when assembly structure must change. Feature-scan game APIs instead of hardcoding offsets/private fields.
- **Host is the only save authority**; guests keep local settings only. Mod save data requires mod id/version/schema version + migration policy.
- **KrokMP compatibility (reserved, not near-term)**: many community mods target the legacy KrokMP API; plan an optional compat adapter mapping KrokMP API → CUO API (API-level only, separate module, never pollutes CUO Core). Not before Phase 4 + real migration demand; catalog KrokMP's API surface during reversing (see `docs/architecture.md` §5.4).

## Development Phases (current: Phase 1 — single player entity)

**Phase 0 is COMPLETE** (verified 2026-08-05 with dual Steam accounts, host + sandboxed guest): BepInEx loads the plugin, Steam initializes at plugin load, lobby create/join works, ISteamNetworkingMessages ping/pong works end-to-end (RTT ~15-25ms on local loopback). Key finding: `k_EResultConnectFailed` is transient — the Steam P2P session establishes lazily (~30s); persistent retry is the correct strategy, not a transport failure. Phase-0 test keys live in Plugin.cs (F8/F9/F7) — remove when the real lobby UI lands.

1. **Phase 0 — Feasibility**: BepInEx loads → Steam Lobby create/join → network ping/pong → SteamID readout → safe disconnect.
2. **Phase 1 — Single player entity**: join/leave, position, heading, basic input, scene load state.
3. **Phase 2 — Entity lifecycle**: spawn/despawn/respawn, Entity IDs, scene switches, snapshot resend.
4. **Phase 3 — Game core loop**: interaction, inventory, combat, NPCs, quests, world time, saves — each with explicit host-authoritative vs guest-render logic.
5. **Phase 4 — Public Mod API**: content registration, custom entities, network messages, host commands, UI, mod-state saves, mod dependencies, manifest validation.
6. **Phase 5 — Tooling & ecosystem**: mod manager, auto-install, crash reports, network diagnostics, conflict detection, host migration, dedicated server.

Do not jump ahead: Phase 1 must not add inventory/combat/quests/saves. MVP explicitly excludes: host migration, dedicated server, auto mod install, generic physics sync, client prediction, full anti-cheat.

## Engineering Conventions (binding)

1. **English by default** — all code, comments, and committed docs are written in English. Exceptions: I18N artifacts (multi-language docs, resource files).
2. **Modern idiomatic C#** — use the latest C# language features and idiomatic C# style; `LangVersion = preview`, nullable reference types enabled. Note: `net452`/BepInEx APIs lack nullability annotations — use `!` assertions at boundary calls instead of disabling the feature.
3. **Self-learning mechanism** — when valuable, *generalizable* knowledge emerges during work (reusable workflows, non-obvious game-internals findings, hard-won conventions), record it in `CLAUDE.md`, `docs/`, or a project-level skill, and commit it with git. Be selective: the knowledge must have real reusability value beyond the moment — do not record random observations. Don't create project skills before actual workflows exist to distill them from.
4. **Clean git hygiene** — commits never contain build artifacts/binaries, personal preferences, machine/environment specifics, or secrets. Route those through `.gitignore` (`CLAUDE.local.md` holds local-only notes).
5. **Requirement triage** — when the user states a requirement, judge whether it is reusable and long-lived: personal/specific → record in `CLAUDE.local.md`, keep out of commits; shared/project-benefiting → record and commit (`CLAUDE.md`, docs, skills); **ambiguous → ask the user** whether it's personal or shared.
6. **Architecture & maintainability first** — fix root causes, not symptoms; when fixing a problem, consider whether a better design exists; for risky or architecture-affecting changes, propose and get user consent before acting.

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
- No mod consistency checks — implement handshake + manifest validation in the MVP.
- Undefined failure modes — define behavior for: Steam disconnect, guest/host dropout, mod load failure, scene-switch timeout, corrupt snapshot, version mismatch, mid-game join. Prefer "safe exit, no save corruption" over "try to recover".
- Licensing/legal — verify game EULA (injection allowed?), anti-cheat, Steamworks DLL redistribution, BepInEx (LGPL-2.1) and dependency licenses before any distribution.
