# Save system: layer-end and mid-run saves

- Status: In progress (design frozen 2026-09-10; staged — see the stage tickets below)
- Priority: High
- Category: Persistence / save system
- Source: User request (2026-09-07) — "添加存档系统，包括层级末尾存档、游戏中途存档。考虑到游戏本体的存档格式不支持中途存档，需要你设计全新的存档格式，建议以常见存储格式为基准（例如 Json），辅以文件压缩。考虑到拓展性，推荐使用目录级压缩格式，而不是单文件压缩。开发时，需要重点关注存档的中途性质，防止出现多生成、少生成内容的情况"
- Related: `docs/architecture/save-archive-format.md` (normative format contract), stage tickets in the "Stages" table below, `todo/systemic-save-backup-management.md` (backup/restore layer on this format), `review/world-determinism-world-fingerprint.md`, `future/kernel-replication-namespace-relocation.md`

## Functional intent

Host-authoritative saves (AGENTS.md: the host is the only save authority), as a world repository in
the Minecraft shape: one folder archive per world plus N compressed archive backups.

1. **Layer-end save** — at the boundary where the run descends to the next layer; the world is
   regenerable from the run baseline there.
2. **Mid-run save** — at an arbitrary moment *inside* a layer: mutated terrain, world items,
   container trees, opened/damaged entities, consumed traps, fluids, enemies, and in-flight
   operations.

The native `save.sv` cannot express (2), so CUO owns its own format and restore path.

## Design decisions (frozen with the user on 2026-09-10)

The six questions the ticket originally carried are answered; decisions 162–166 in
`docs/decisions/active.md` register them and `docs/architecture/save-archive-format.md` specifies
the format. Do not reopen them without the user.

| # | Question | Decision |
|---|---|---|
| 1 | Who is in the save, and how does a guest get their character back? | All members present at the cut. Identity is **transport-aware**: Steam → `steam-<steamId64>`, IP-direct → `name-<display name>` (that mode has no account identity). A player absent from the package is a **new player**: fresh character and starting supplies. |
| 2 | Restore semantics for an incomplete package | **Repair mode, minimal loss.** Salvage is per entry, not per domain: a removed/unknown content id is skipped with a warning, the remaining entries of that domain still apply. No silent "restart the layer". Only an unreadable manifest is a hard gate — then the newest readable backup is used and the fallback is reported loudly. |
| 3 | Slot shape | **Not native single-slot.** The native slot only expresses a layer-end cut. The system is a multi-world repository with a live snapshot plus N backups, plus a **configurable interval autosave/backup**; the storage design is CUO-owned. |
| 4 | Native `save.sv` coexistence | **Fully independent.** CUO never writes `save.sv` and never depends on it; the native format cannot express a mid-run cut, so coexistence would create two sources of truth. |
| 5 | v1 scope | Kernel checkpoint (all domains) + run baseline + character data + world diff + transient policy. **Out of v1**: mod state persistence, host bans, old protobuf kernel-save migration. |
| 6 | Package location and naming | `<CUO data root>/cuo/saves/<worldId>/` per world, `live/` folder archive (unpacked JSON) + `backups/*.cuoz` (ZIP, directory entries preserved). `worldId` is an immutable short id; the display name is renameable and is never the directory key. |

Additional decisions confirmed in the same session:

- JSON (`System.Text.Json`, Runtime-owned package) + ZIP from `System.IO.Compression`; no new
  format library, no single-file opaque blob.
- The native **Load button is the continue entry**: `PreRunScriptLoadRunPatch` already intercepts
  `PreRunScript.LoadRun`, and `WorldGeneration.SaveAndExit()` has no caller anywhere in the
  decompiled game, so "independent of native" costs nothing in reachability.
- The load/repair path must surface skipped entries in-game, not only in the log.

## Existing evidence (re-verified 2026-09-10)

Native game save:

- `SaveSystem.SaveGame()` writes `save.sv` = GZip(JSON `SaveInfo`) under `Application.persistentDataPath` —
  `reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:186`, compression `:458-473`; it carries
  character/run state only, no world state.
- `TryLoadGame()` restores the character/run fields and then **deletes** `save.sv` (`:453`); the world
  is regenerated from the run baseline.
- The native surface is a **single slot**: `SaveSystem.HasSave()` (`:190-193`) is an existence check on
  the one fixed path, and the main menu has one `loadButton` whose interactable state is that check
  (`PreRunScript.cs:65-92`). There is no slot list UI anywhere.
- `WorldGeneration.SaveAndExit()` (`WorldGeneration.cs:1023-1030`) advances the layer then saves, but
  **has no caller** in the decompiled game; `ConsoleScript`'s `saveandquit` path decrements `biomeDepth`
  before saving (`ConsoleScript.cs:787-794`).
- CUO's only host save hook today: `RunMenuReturnCoordinator.Flush` calls `SaveSystem.SaveGame()` on a
  live-world menu return — `src/CasualtiesUnknownOnline.GameAdapter/Run/RunMenuReturnCoordinator.cs:42-68`
  (call at `:58`). Making the save system independent retires this hook or re-points it at the CUO save.
- CUO already intercepts the three native world-entry paths (`Patches/PreRunScriptLoadRunPatch.cs`,
  `PreRunScriptStartRunPatch.cs`, `PreRunScriptStartTutorialPatch.cs`), so a CUO-owned continue path
  rides an existing seam instead of new UI.

CUO persistence today:

- `CharacterDataFileStore` — per-SteamID host-side character snapshots (protobuf, atomic write, cleared
  on a new run). Decision #27; `docs/evidence/selfchecks/players/character-data-persistence-selfcheck.md`.
- `ModStateFileStore`, `HostBanFileStore` — mod state and host bans.
- `GameCheckpoint` (`src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs`) covers items, run, world
  entities, players, enemies, fluids, and optional random streams.
- `KernelSaveFileStore` + `KernelSaveFile` + `SaveHeader` (`src/CasualtiesUnknownOnline.Runtime/Session/Items/`)
  exist and are unit-tested, **but have no production caller**: `CuoBootstrap.BuildServiceProvider` never
  registers the store and `Plugin.cs:82-96` passes only `characterDataFile` / `modStateFile` / `hostBanFile`.
- `GameCheckpoint.RandomStreams` is never populated in production (`GameStateStore.CreateCheckpoint` passes
  `null`; `docs/architecture/protocol.md:176-178`).
- Kernel item location is X/Y only (`src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemLocation.cs`);
  velocity / rotation / angular velocity / fresh-drop ride the wire and transient paths, not the checkpoint.

World state the kernel does not own today (a mid-run save must capture it or explicitly regenerate it):

- Host block difference table (`WorldStateMessageService._damagedBlocks`, `:35`; cap `:38`) and partial block
  damage (`BlockDamageRegistry`).
- Keypad codes (host-generated lazily, random), geyser liquid types (per-side public-stream roll, host
  authority), radiation line, world time, earthquake timers.
- Native `WorldGeneration.blockDamages` list (cap 128, `WorldGeneration.cs:732-737`).
- Item physics transients (velocity / rotation / angular velocity / fresh-drop).
- In-flight operation state: pending block break + drops (`BlockBreakPendingState`), pending trap drops
  (`TrapDropPendingState`), pending pickup queue (`PendingPickupQueue`, 500 ms hold), drop pending
  (`DropPendingState`), medical/shrapnel sessions (`MedicalOperationSessionService`,
  `ShrapnelOperationSessionService`, `OtherMedicalOperationSessionService`), craft batches, entity creation
  deferred reports.

Design constraints:

- `CasualtiesUnknownOnline.GameState` may not add a PackageReference/ProjectReference (architecture guard),
  so save-format DTOs live in Runtime; domain→DTO mapping stays in Runtime (`KernelDomainWireMapper` /
  `KernelWireMapper` are the precedent).
- Runtime has no JSON library today (`Runtime.csproj` references DI/logging/BepInEx plus Protocol/GameState).
  The chosen serializer is `System.Text.Json`, added as a Runtime package reference in S1.
- net48 ships `System.IO.Compression` / `ZipFile`, so the directory-entry archive needs no further
  runtime dependency.

## Stages

The ticket is split into stages; each lands with its own tests, gates, deploy-hash verification and
independent adversarial review before the next begins (AGENTS.md convention 11). This umbrella stays in
`in-progress/` as the roadmap and holds no independent implementation work.

| Stage | Ticket | Scope | State |
|---|---|---|---|
| S1 | `in-progress/save-format-and-world-repository.md` | Format + world repository + backup I/O, no gameplay wiring | starting |
| S2 | `todo/save-layer-end-save-and-restore.md` | Layer-end capture/restore via the native continue entry | blocked on S1 |
| S3 | `todo/save-mid-run-consistent-cut.md` | Mid-run consistent cut, all domains, world diff, transient policy | blocked on S1 |
| S4 | `todo/save-multiplayer-restore-and-backups.md` | Guest restore claim, validation/recovery, scheduled autosave + retention | blocked on S2/S3 |

## Mid-run semantics: the hard part

A mid-run save is a **consistent cut** of an actively simulating world; a restore must be exactly-once.

- **Cut atomicity** — one save seam on the host main-thread pump freezing the kernel revision, all domain
  tables and the native world tables at one instant; the manifest records `cutPhase`.
- **No over-generation** — same-id dedup on restore; generation-time items/entities never re-materialized;
  materialized drops never re-created; container children have exactly one parent; terminal facts
  (consumed/destroyed items, removed enemies) never resurrect; load twice = same world.
- **No under-generation** — every fact in the save materializes exactly once.
- **Transient policy** — every in-flight state above gets an explicit capture / resolve-before-save /
  drop-with-a-log decision. Silent loss is forbidden.
- **Determinism** — the save carries enough of the run baseline (`RandomState`, layer index, layer
  modifiers) to reproduce the world; `RandomStreams` must be populated if any domain depends on them.
- **Crash safety** — the write transaction in `docs/architecture/save-archive-format.md` §5.
- **Version/build gating** — schema version + game build + CUO build + protocol version, per §6.1.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Layer-end save → quit → continue | Next layer starts with the saved character/run state; no duplicate/missing items; the CUO continue entry reaches it |
| 2 | Mid-run save with mutated terrain (mined/placed/quaked blocks + partial damage) | Reload reproduces the same block diff exactly |
| 3 | Mid-run save with world items (ground/containers/carried/worn) | Same identities, locations, and container trees; no duplicates, no loss |
| 4 | Mid-run save with opened/damaged building entities, consumed traps, fluids, enemies | Same facts; no re-trigger; no resurrection of terminal facts |
| 5 | Save during an in-flight operation (block-break pending drops, trap drop hold, pickup queue, drop flush, medical/shrapnel session, craft batch) | The chosen policy is applied and logged; no silent loss or duplication |
| 6 | Multiple players (host + guests, Steam and IP-direct separately) | Every player's character restored under their transport-scoped key; a player absent from the package joins as a new player with starting supplies |
| 7 | Crash/kill during save | Previous valid save intact; next start loads it |
| 8 | Corrupt or foreign-build save | Repair mode per `docs/architecture/save-archive-format.md` §6: kept entries load, skipped entries are named in-game; an unreadable manifest falls back to the newest readable backup loudly |
| 9 | Load twice / load → save → load | Idempotent; world fingerprint stable |
| 10 | Content removed by a mod update | The affected entries are skipped with warnings; the rest of the world loads |

## Non-goals

- Cloud saves, cross-machine save sharing, anti-cheat on save contents, host migration.
- A second archive format for backups (reuse this package).
- Any wire-protocol change; this is a local disk/persistence surface.
