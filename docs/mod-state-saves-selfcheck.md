# Mod-state saves — mechanism inventory and self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "mod-state saves". Decision:
implement the first Mod-API save surface as a **host-persistent, per-mod opaque
key/value store** — the host is the only save authority, mods own their payload
schema, and guests coordinate through the existing message/command surfaces
rather than writing a local save file. No wire change, no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Save authority | Architecture.md §8: host is the only save authority; guests keep local settings only. |
| 2 | Permission | `ModPermission.WriteGameState` is the existing state-write flag; this slice gives it its first live enforcement point (`ModService.State`). |
| 3 | Persistence shape | Mirrors `CharacterDataFileStore` (versioned protobuf wrapper + atomic temp/replace) — same corruption/version degradation contract. |
| 4 | Mod state isolation | Each mod reads/writes only its own id-scoped entry; the framework never interprets mod bytes. |
| 5 | Wire | No new NetMsg — this is local host persistence, not a sync channel. ProtocolVersion stays 29. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContext` | Added `State` property (new public API surface). |
| `IModState` | New binding contract (host-only writes, schema version, defensive copies, caps). |
| `ModService` | Loads the state table once in `Initialize` (before discovery/Bind), owns `ModStateAdapter` and persistence. |
| `ModStateFileStore` / `ModStateFile` | New disk store (atomic, versioned, degrade-to-empty). |
| `ModStatePolicy` | New pure safety rails (key length/count, value size). |
| `CuoBootstrap` / Plugin | New optional `modStateFile` path; production writes `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin`. |
| Guest / permissionless mods | Explicitly refused write access; guest reads are also refused (host-only table). |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host-only writes | `ModStateAdapter.TrySet/Remove/Clear/Schema` call `EnsureHostStateWrite` (host role + `WriteGameState`) | `ModStateTests.Guest_CannotWriteOrReadHostState`, `HostWithoutWriteGameState_StateWritesAreRefused`. |
| Per-mod isolation | State is keyed by manifest id in `_modState`; `IModState` only exposes the caller's own id | Code path (`ModService.State.cs`); no cross-mod accessor exists. |
| Defensive copies | Store clones on write and read | `ModStateTests.ValuesAreDefensivelyCopied_OnWriteAndRead`. |
| Persistence across process | File load once at `Initialize`; every mutation writes the full table atomically | `ModStateTests.Persistence_SurvivesANewHostProcess`. |
| Corrupt/unknown file | `ModStateFileStore.TryLoad` returns false → empty table + warning, next write replaces | `ModStateTests.CorruptFile_DegradesToEmptyAndNextWriteReplacesIt`. |
| Caps / no silent truncation | `ModStatePolicy` rejects invalid keys/over-cap values/too many keys | `ModStateTests.InvalidKeysAndValues_AreRefusedWithoutSilentTruncation`. |
| No wire/protocol regression | No new NetMsg; state is host-local | `docs/mod-api.md` §7 still says ProtocolVersion 29; full suite green. |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real `ModService` / `TestNode` stack: host write/read,
  guest refusal, permission refusal, defensive copies, persistence across a new
  node process, corrupt-file degradation.
- Static evidence: the host-only save-authority rule (architecture.md §8), the
  file-store degradation contract mirrors `CharacterDataFileStore`, and the
  API/permission docs updated in `docs/mod-api.md`.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1079 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean (source files; the ignored generated `obj/MyPluginInfo.cs` is outside git) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | passed (32 events / 32 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| No manual acceptance | per development-period rule |
