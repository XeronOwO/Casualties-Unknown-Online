# AGENTS.md

Instructions for AI coding agents and contributors working in this repository.

## Project Overview

**Casualties Unknown: Online (CUO)** — a multiplayer mod framework for the game *Casualties Unknown*
(Demo), built on BepInEx. The base game ships with **no multiplayer support**; CUO adds Steam-based
Host + Guests co-op by injecting a new multiplayer runtime and reorganizing the local-only game state
into "host-authoritative simulation + guest input/state sync".

- Stable **CUO Runtime** (network protocol, host/guest state machine, mod loading, serialization,
  tick/snapshot, logging, version negotiation)
- Replaceable **Game Adapter** per game build (the only layer that knows the game's private types)
- The game updates will NOT adapt to us — the adapter layer must absorb that churn.

Full architecture blueprint: [`docs/architecture.md`](docs/architecture.md).
Landed binding decisions: [`docs/tech-decisions.md`](docs/tech-decisions.md).

## Repository Layout

```
src/CasualtiesUnknownOnline.Abstractions/  # CUO Abstractions (net48) — public API surface; the ONLY package mods may reference
src/CasualtiesUnknownOnline.Runtime/  # CUO Runtime (net48) — DI/Logging/BepInEx/Steam/session layer; never references game assemblies
src/CasualtiesUnknownOnline.GameAdapter/  # CUO Game Adapter (net48) — the ONLY project referencing game assemblies; HarmonyX patches
src/CasualtiesUnknownOnline.Plugin/  # BepInEx 5 plugin entry (net48) — thin lifecycle driver
CasualtiesUnknownOnline.slnx         # Solution (VS 2022)
references/                    # Game assemblies, gitignored, copied on demand (see references/README.md)
reversing/                     # Reverse-engineering workspace, gitignored (see reversing/README.md)
docs/architecture.md           # Architecture blueprint (full design, pitfalls, phases)
docs/tech-decisions.md         # Landed binding decisions (moved out of this file)
docs/mod-api.md                # Phase 4 Mod API contract
AGENTS.md                      # This file
AGENTS.local.md                # Local personal notes — gitignored, never commit
```

## Build

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx   # MUST pass before every commit (incl. the PATCH-CONTRACT game-update guard; after a game update, re-copy DLLs per references/README.md)
dotnet format CasualtiesUnknownOnline.slnx   # MUST run before every commit
powershell -File tools/check-architecture.ps1   # MUST pass before every commit (≤600-line classes, ≤5 state bools, one top-level type per file)
powershell -File tools/check-event-replay.ps1   # MUST pass before every commit (docs/event-replay-matrix.csv — touching an event mechanism means updating its row in the same commit)
powershell -File tools/check-entity-event-dispatch.ps1   # MUST pass before every commit (every EntityEventKind dispatched in TrapEntityScan + TrapEffectApplier + TrapVisualReplay — a new kind forgotten in one table fails this gate)
powershell -File tools/check-delivery.ps1   # MUST pass before the cycle's FINAL commit (docs/delivery-checklist.md; then -Reset for the next cycle)
```

- **`dotnet format` is mandatory before every commit** — `.editorconfig` is error-severity and enforced
  at build time (`EnforceCodeStyleInBuild`), so style violations fail the build.
- Target: `net48` (BepInEx 5 requirement), `LangVersion = preview`, nullable enabled, warnings-as-errors.
- NuGet sources: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (configured in csproj).
- Game assemblies (`references/`) are copyrighted and not in the repo — copy **on demand** from the
  game's `CasualtiesUnknown_Data\Managed\` per `references/README.md` (only the Game Adapter may reference them).
- Packaged plugin DLL deploys into the game's `BepInEx/plugins/` (path is machine-local — see `AGENTS.local.md`).

## Architecture in Brief

Layered, dependency flows downward only:

```
Mods → Mod Framework API → Multiplayer Runtime → Game Adapter → BepInEx/Unity/Steam
```

Non-negotiable design rules:

- **Local compute, remote verify/sync (user mandate)**: every player simulates its own actions locally
  with full single-player feel; results are exchanged as sync data for the peer to apply and verify. The
  host NEVER simulates the guest's per-frame behavior. "Host authority" is limited to global world-state
  ownership (seed, saves, rulings, anti-cheat). Never gate gameplay behind missing sync.
- **Accept-first sync arbitration (user mandate)**: the host trusts a guest's reports first — adopt and
  relay, never blocking the player's action. Correct only on an OBVIOUS conflict (a race: two guests claim
  the same item; an item/enemy was already picked up/killed before the report arrived; an entity vanished)
  — and a correction never blocks the player. No strict validation (collision-box / angle / distance
  checks) and no anti-cheat (a guest hiding damage or over-reporting it) — those are LOW priority; get the
  feature working first, and let loss/races self-heal via the 1 Hz snapshot and the next message. This is
  the existing `accept-with-correction` pattern (ItemArbitration), now the standard for EVERY sync domain.
- **Sync semantics, not Transforms**: synchronize game-semantic state, not raw Transform/GameObject state.
- **Custom network entity IDs**: Unity instance IDs are process-local; use `NetworkEntityId` (epoch + host
  allocation counter + generation).
- **Steam Lobby ≠ transport**: Lobby is discovery/roster; game data goes over Steam networking. Never
  expose Steam APIs to mods — abstract as `INetworkTransport` / `ISession` / `IPeer` / `INetworkChannel`.
- **Main-thread marshaling**: network/Steam callbacks never touch Unity objects.
- **No Host Migration in MVP**: host exit → session ends → guests return to lobby.
- **Safe degradation**: capability detection at startup; grades `Compatible` / `CompatibleWithWarnings` /
  `Unsupported` / `CriticalFailure`. Never let a failed patch silently run.
- **Prefer HarmonyX**; Mono.Cecil only when assembly structure must change. Feature-scan game APIs instead
  of hardcoding offsets/private fields.
- **Host is the only save authority**; guests keep local settings only.
- **KrokMP compatibility (reserved, not near-term)**: optional compat adapter later, API-level only,
  never pollutes CUO Core (see `docs/architecture.md` §5.4).

## Development Phases

Current: **Phase 3 native game-content follow-through** — finish the remaining base-game coverage (see `docs/backlog.md`) before more Mod API work, while reserving extension seams. **Phase 4 Mod API** second round has landed (permissions, host commands, dependency ordering, SemVer — see `docs/mod-api.md`) and its remainder is MEDIUM priority until the native content is covered.

1. **Phase 0 — Feasibility** — COMPLETE (BepInEx loads, Steam lobby/ping end-to-end; `k_EResultConnectFailed`
   is transient, persistent retry is correct).
2. **Phase 1 — Single player entity** — COMPLETE (local-compute/remote-verify sync, 20 Hz state stream,
   frozen render clones).
3. **Phase 2 — Entity lifecycle** — COMPLETE (scene-switch sync, snapshot resend, disconnect+reconnect,
   character save/restore).
4. **Phase 3 — Game core loop** — largely landed (star network, item/world/entity/crafting domains; see
   `docs/tech-decisions.md`).
5. **Phase 4 — Public Mod API** — first two rounds landed; remaining: content registration, custom
   entities, UI, mod-state saves.
6. **Phase 5 — Tooling & ecosystem** — future (mod manager, auto-install, crash reports, host migration,
   dedicated server).

MVP explicitly excludes: host migration, dedicated server, auto mod install, generic physics sync,
client prediction, full anti-cheat.

## Engineering Conventions (binding)

1. **English by default** — all code, comments, and committed docs are English (I18N artifacts excepted).
2. **Modern idiomatic C#** — `LangVersion = preview`, nullable enabled; use `!` assertions at BepInEx
   boundary calls. Prefer `var`; resolve name collisions with a `using` alias at the top of the file, never
   by fully-qualifying call sites. `is null` / `is not null` over `== null` (IDE0041, documented but not
   build-enforced). **Unity objects are the exception**: `Body`/`PlayerCamera`/`WorldGeneration`/cloned
   proxies MUST use `== null` / `!= null` (the operator detects scene-reload-destroyed objects; `is null`
   misses them and throws `MissingReferenceException`) — IDE0031 is disabled for this reason.
3. **Self-learning** — when valuable, *generalizable* knowledge emerges (reusable workflows, non-obvious
   game-internals findings, hard-won conventions), record it in `AGENTS.md`, `docs/`, or memory, and commit
   it. Be selective: real reusability value beyond the moment only.
4. **Clean git hygiene** — commits never contain build artifacts, personal preferences, machine/environment
   specifics, or secrets. Committed files never contain real machine paths — use placeholders (`<game-dir>`).
   Real paths live only in the gitignored `AGENTS.local.md`.
5. **Requirement triage** — judge whether a requirement is reusable/long-lived: personal/specific →
   `AGENTS.local.md` (never commit); shared/project-benefiting → commit (`AGENTS.md`, docs, memory);
   **ambiguous → ask the user**.
6. **Architecture & maintainability first** — fix root causes, not symptoms; for risky or
   architecture-affecting changes, propose and get user consent before acting.
7. **Evidence-based changes (user requirement)** — every change must be grounded in code: find the mechanism
   in the decompiled sources (`reversing/`, cite file:line) before touching it. Never fix by intuition. A
   proposed fix must explain ALL observed symptoms, not just the surface one.
8. **One top-level type per file** — a `.cs` file holds exactly one top-level type; file name matches type
   name. Nested types stay with their container.
9. **Patch hooks: report only verified writes** — a Prefix that swallows a write must not let the same
   patch's Postfix report it. Harmony runs the Postfix regardless of the Prefix's return value. Enforce by
   re-reading the written state or passing the Prefix verdict through — never by hook ordering. Same for
   Prefix scene-state inference: replace with explicit state passed between hooks.
10. **Sync-chain architecture: deep modules, explicit identity, verified commits** — each sync chain is ONE
    deep module owning its operation identity, cross-hook state, and message commit; Harmony patches are
    thin adapters. Seven binding rules: one operation = one owner; one hook = one business entry per domain;
    patches hold NO cross-call business state (`__state` only); call identity is explicit and scoped
    (`CallContext.Enter`); network reports happen only after a verified commit (`CommitReport`); CUO never
    mirrors game-authoritative state long-term; every operation is recoverable as a complete `OperationTrace`.
    Split a class only when the 600-line gate or a second consumer demands it.
11. **Triggers use dedicated events; sync is only fallback/replay** — a discrete trigger (a use, a slot
    move, a bite, a block break) travels as a DEDICATED event message (one operation = one message), never
    by riding the periodic snapshot stream (1 Hz character / 20 Hz state). The periodic stream has exactly
    two roles: (a) fallback — self-heal a lost event, and the divergence monitor warns when a change
    arrives only via the stream (the event chain missed it); (b) host→guest replay — the full
    world/character state on entry or reconnect. Never design a trigger to depend on the snapshot.

## Delivery Quality Gate (binding, user mandate 2026-08-10)

Repeated "paper review passes, runtime fails" cycles made "don't assume" a hard-stop process. Executable
form: `docs/delivery-checklist.md` + `tools/check-delivery.ps1`.

- **Principle 0 — No self-assumption**: "I think"/"should be"/"probably fine" are red flags. Every claim
  needs a source reference (`file:line`) OR runtime evidence. Adversarial self-check before touching
  anything: "in which situation does my plan break?" The runtime is the judge, never the plan.
- **Step 1 — Mechanism completeness inventory** (BEFORE any change): list EVERY touched mechanism —
  protocol/messages, game mechanics (with complete side-effect table), random stream/determinism, lifecycle,
  runtime behavior — each with evidence or explicit "unverified". Unverified items must be covered or
  explicitly degraded (recorded, not silent).
- **Step 2 — Complete plan delivery** (BEFORE deployment, user reviews): self-critical review BEFORE the
  user reviews. Deliverable = fact sheet + design + verification design + self-check table (mechanism ×
  change × evidence), every cell filled. The user approves before deployment.
- **Step 3 — One deployment, one verification target**: verification failure → back to Step 1, never a
  patch stacked on a patch. Deploy to the real game directory only (`tools/deploy.ps1` hard-rejects sandbox paths).
- **Step 4 — Post-delivery structure review**: every touched class (size/600-line gate/responsibility/state
  bools); dead mechanisms deleted in the same round — never left co-existing.

**Hard order**: understand → mechanism inventory → adversarial self-check → plan + self-check table →
user approval → implement → build + format + architecture gate → deploy → runtime verification → structure
review → commit. Skipping any step = non-conforming delivery.

## Known Pitfalls

Details in `docs/architecture.md` §10. Key ones:

- Treating Steam P2P as plain LAN UDP — the two modes are different; don't mix them.
- Syncing all Transforms — fails on physics, parenting, animation, nav, rigidbodies, scene loads.
- Over-reliance on hardcoded offsets/private fields — breaks on every game update.
- Premature Mono.Cecil — prefer HarmonyX patches.
- Harmony patch state leakage — a Prefix that clears an instance field must have its Postfix restore it.
- Ignoring the main thread — network/Steam callbacks must not touch Unity APIs.
- `System.Memory` hijacks `.Reverse()` — `array.Reverse()` binds to `MemoryExtensions.Reverse(Span<T>, void)`;
  use reverse-index loops or explicit `Enumerable.Reverse(...)`.
- Lobby identity must follow the ACTUAL lobby, not process history — a client that once hosted and
  later joins a friend's lobby must become a Guest (leave current lobby first; `LobbyLeft` tears the
  old session down before the new handshake).
- A Steam receive batch is all-or-nothing: one handler exception loses every later message in the
  same `ReceiveMessagesOnChannel` batch — catch per message, release in `finally` (WorldReady was
  lost behind a throwing enemy-snapshot packet for the full start-gate timeout).
- Late Steam init (F8 retry after a failed load-time init) must refresh downstream snapshots —
  the local entity's SteamId was captured as 0 and the self-activation PlayerJoin never matched.
- Undefined failure modes — define behavior for disconnect/dropout/mod-load-failure/version-mismatch/etc.
- Licensing/legal — verify game EULA, anti-cheat, Steamworks DLL redistribution, and dependency licenses
  before any distribution.
