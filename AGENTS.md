# AGENTS.md

Instructions for AI coding agents and contributors working in this repository.

## Project Overview

**Casualties Unknown: Online (CUO)** is a BepInEx multiplayer mod framework for
*Casualties Unknown* (Demo). The game has no multiplayer; CUO adds Steam-based
Host + Guests co-op by reorganizing local-only game state into host-authoritative
simulation with guest input/state sync.

- Stable **CUO Runtime**: protocol, host/guest state machine, mod loading, serialization,
  tick/snapshot, logging, version negotiation.
- Replaceable **Game Adapter**: the only layer that knows the game's private types and
  absorbs game-update churn.

[REF] Active architecture: `docs/architecture/README.md` ·
Current design: `docs/architecture/current.md` ·
Decisions: `docs/decisions/active.md` · Evidence: `docs/evidence/verification.md`.

## Repository Layout

```text
src/CasualtiesUnknownOnline.Abstractions/  # public API; the ONLY package mods may reference
src/CasualtiesUnknownOnline.Runtime/       # DI/Logging/BepInEx/Steam/session; never game assemblies
src/CasualtiesUnknownOnline.GameAdapter/   # the ONLY project referencing game assemblies; HarmonyX
src/CasualtiesUnknownOnline.Plugin/        # BepInEx 5 entry; thin lifecycle driver
CasualtiesUnknownOnline.slnx               # solution
references/                                # game assemblies, gitignored, copied on demand
reversing/                                 # reverse-engineering workspace, gitignored
docs/                                      # architecture, decisions, backlog, feature matrices, selfchecks
AGENTS.local.md                            # gitignored local notes; never commit
```

See `docs/README.md` for the documentation index.

## Build / Commit Gates

```bash
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx          # must pass before every commit
dotnet format CasualtiesUnknownOnline.slnx        # mandatory before every commit
powershell -File tools/check-architecture.ps1     # ≤600-line classes, ≤5 state bools, one type/file
powershell -File tools/check-event-replay.ps1     # event mechanism changes update matrix row
powershell -File tools/check-entity-event-dispatch.ps1
powershell -File tools/check-delivery.ps1         # final commit of each delivery cycle
```

- `[GATE]` All of the above must pass before commit; `dotnet format` is build-enforced.
- `[RULE]` Pure documentation-only changes (no `src/`, `tests/`, or `tools/` modifications) skip build/test/gates; review the diff and commit directly. If docs describe a code change, commit them with the code change and run the gates in that same commit.
- Target: `net48`, `LangVersion = preview`, nullable enabled, warnings-as-errors.
- NuGet sources: nuget.org + nuget.bepinex.dev + nuget.samboy.dev.
- Game assemblies are copyrighted and only the Game Adapter may reference them.
- Packaged plugin deploys via `tools/deploy.ps1` (machine path in `AGENTS.local.md`).

## Non-Negotiable Architecture Rules

- `[CRITICAL]` **Local compute, remote verify/sync**: each player simulates its own
  actions with single-player feel; the host never simulates a guest's per-frame behavior.
  Host authority is limited to global world-state ownership (seed, saves, rulings).
- `[CRITICAL]` **Accept-first sync arbitration**: adopt and relay a guest's report first;
  correct only on an obvious conflict; a correction never blocks the player. Strict
  validation/anti-cheat are low priority until the feature set is stable.
- `[CRITICAL]` **Sync semantics, not Transforms**: synchronize game-semantic state, never
  raw Transform/GameObject state.
- `[CRITICAL]` **No host migration in MVP**: host exit → session ends → guests return to lobby.
- `[CRITICAL]` **Dedicated events, not snapshots**: discrete triggers travel as dedicated
  event messages; periodic streams are only fallback/replay.
- `[CRITICAL]` **Deep sync chains**: one operation = one owner; Harmony patches are thin
  adapters; no cross-call business state in patches; reports happen only after a verified
  commit; every operation is recoverable as a complete trace.
- `[CRITICAL]` **Injected state must be authority-safe**: mutable state belongs to its owner;
  DI services are behavior/mechanism, not global mutable state.
- Network/Steam callbacks never touch Unity objects; main-thread marshaling is mandatory.
- `[RULE]` Use `NetworkEntityId` (epoch + host allocation counter + generation), never
  Unity instance IDs.
- `[RULE]` Steam Lobby is discovery/roster only; game data goes through `INetworkTransport`
  / `ISession` / `IPeer` / `INetworkChannel`. Never expose Steam APIs to mods.
- `[RULE]` Prefer HarmonyX; Mono.Cecil only for assembly structure changes. Feature-scan
  game APIs instead of hardcoding offsets/private fields.
- `[RULE]` Host is the only save authority; guests keep local settings only.
- `[RULE]` Safe degradation at startup: `Compatible` / `CompatibleWithWarnings` /
  `Unsupported` / `CriticalFailure`; never let a failed patch silently run.

## Development Phases

Current: **Architecture evolution complete (Phases A–E).** Native game-content sync,
the Phase 4 Mod API, and the typed-deterministic-kernel migration are complete. The
typed kernel is the only supported architecture; see
`docs/architecture/README.md` for the active architecture and
`docs/backlog/README.md` for remaining/future work.

MVP explicitly excludes: host migration, dedicated server, auto mod install, generic
physics sync, client prediction, full anti-cheat. The generic Prediction Runtime is
a separate future architecture item, not part of the completed evolution.

## Engineering Conventions (binding)

1. `[RULE]` English by default in all code, comments, and committed docs.
2. `[RULE]` Modern idiomatic C#: `var`, nullable, `is null`/`is not null`, using aliases for
   name collisions. **Unity objects are the exception**: use `== null` / `!= null` because
   the overload detects scene-reload-destroyed objects.
3. `[RULE]` One top-level type per file; file name matches type name. Nested types stay with
   their container.
4. `[RULE]` Evidence-based changes: cite decompiled sources (`reversing/`, file:line) before
   touching code; fix root causes, not symptoms.
5. `[RULE]` Clean git hygiene: no artifacts, personal preferences, machine paths, or secrets
   in commits. Use placeholders like `<game-dir>`.
6. `[RULE]` Requirement triage: personal/specific → `AGENTS.local.md`; shared/beneficial → commit;
   ambiguous → ask the user.
7. `[RULE]` Self-learning: record reusable, generalizable knowledge in `AGENTS.md`, `docs/`,
   or memory; be selective.
8. `[RULE]` Architecture-first: for risky or architecture-affecting changes, propose a plan
   and get consent before acting. (Test-only hardening and behavior-preserving extraction may
   proceed without prior approval.)
9. `[RULE]` Patch hooks report only verified writes; a Prefix that swallows a write must not
   let the same Postfix report it. Re-read the written state or pass the verdict explicitly.
10. `[RULE]` Prefer `using` directives / `using` aliases over fully qualified type names;
    use fully qualified names only when unavoidable (e.g., HotRepl eval, where `using`
    is unavailable).

## Quality & Delivery (binding)

- `[CRITICAL]` No self-assumption: every claim needs source evidence (`file:line`) or runtime
  evidence. The runtime is the judge, not the plan.
- `[CRITICAL]` Root cause over patch stacking: prefer changing architecture to eliminate a
  cause; do not accumulate technical debt.
- `[CRITICAL]` Line-count / architecture gate escapes must be real responsibility splits:
  never delete comments/blank lines, shrink formatting, or move code between files just to
  pass the gate. Extract a single-responsibility type, preserve behavior, and keep tests/gates green.
- `[CRITICAL]` Bug reproduction before fix: add a regression test that fails on current code,
  record the failure, then change implementation.
- `[CRITICAL]` Red→green is a hard gate: if implementation was made without first seeing the
  regression test fail, stop and go back to the pre-fix code to record the red before
  presenting. "The test passes now" is not a substitute for having observed it fail.
- `[RULE]` The red step only needs the focused failing test to run (no full suite required);
  the full suite runs after the fix is in place.
- `[CRITICAL]` Tests must cover core scenarios plus edge/special/failure paths.
- `[CRITICAL]` Every key path and logical branch must be observable; choose log level by
  frequency. An unobservable key path is unfinished.
- `[CRITICAL]` Development-period verification is simulation/static-evidence based; no manual
  dual-client acceptance during feature development. User will do final acceptance later.
- `[GATE]` Follow `docs/evidence/delivery-checklist.md` + `tools/check-delivery.ps1` for each delivery.
- `[GATE]` Check off delivery checklist boxes one line at a time with the Edit tool; bulk
  checking is forbidden.
- `[RULE]` Hard order: understand → mechanism inventory → adversarial self-check → plan +
  self-check table → user approval (for large changes) → **add a regression test that fails
  on current code** → implement → build/gates → deploy → runtime verification → structure
  review → commit.

See `docs/evidence/delivery-checklist.md` for the executable gate.

## Known Pitfalls

`[REF]` Detailed pitfalls list (historical blueprint, still applicable):
`docs/history/architecture-blueprint.md` §10. Keep these in mind:

- After `dotnet format` (or any external tool) modifies a file, re-`read` that file before using Edit; the Edit tool tracks the last-read buffer and refuses stale edits as "file changed since it was read".
- Steam P2P is not plain LAN UDP; don't mix the two modes.
- Syncing Transforms fails on physics, parenting, animation, nav, rigidbodies, scene loads.
- Over-reliance on hardcoded offsets/private fields breaks on every game update.
- Harmony patch state leakage: a Prefix that clears an instance field must have its Postfix restore it.
- A Steam receive batch is all-or-nothing: catch per message and release in `finally`.
- Lobby identity must follow the actual lobby, not process history.
- Late Steam init must refresh downstream snapshots (SteamId captured as 0).
- Undefined failure modes are not acceptable: define disconnect/dropout/version-mismatch behavior.
- `System.Memory` hijacks `.Reverse()` on arrays; use reverse-index loops or `Enumerable.Reverse`.
