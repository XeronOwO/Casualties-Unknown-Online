# Save system: layer-end and mid-run saves

- Status: Todo
- Priority: High
- Category: Persistence / save system
- Source: User request (2026-09-07) — "添加存档系统，包括层级末尾存档、游戏中途存档。考虑到游戏本体的存档格式不支持中途存档，需要你设计全新的存档格式，建议以常见存储格式为基准（例如 Json），辅以文件压缩。考虑到拓展性，推荐使用目录级压缩格式，而不是单文件压缩。开发时，需要重点关注存档的中途性质，防止出现多生成、少生成内容的情况"
- Related: `todo/systemic-save-backup-management.md` (backup/restore layer on the same package format), `review/world-determinism-world-fingerprint.md`, `review/network-traffic-baseline.md`, `future/kernel-replication-namespace-relocation.md`

## Functional intent

Two save points, both host-authoritative (AGENTS.md: the host is the only save authority):

1. **Layer-end save** — at the boundary where the run descends to the next layer. The world is regenerable from the run baseline there, so this is the natural native-equivalent save point.
2. **Mid-run save** — at an arbitrary moment *inside* a layer: mutated terrain, world items, container trees, opened/damaged entities, consumed traps, fluids, enemies, and in-flight operations.

The native `save.sv` cannot express (2). CUO must define its own save format and restore path.

## Format direction (user-specified)

- Base format: JSON — human-inspectable, schema-versioned, forward-extensible.
- Compression: yes.
- **Directory-level packaging, not single-file compression.** The save is a directory tree of files (manifest + one file per domain + per-player character files) packed into one archive that preserves directory entries (ZIP, or tar + gzip/zstd). A new domain adds a file; it never rewrites one opaque blob. Keeping the unpacked directory form available for inspection/debug is desirable.
- Candidate layout (to be finalized in Stage 1):
  - `manifest.json` — schema version, save kind (`layer-end` / `mid-run`), game build, CUO build, run epoch, global revision, layer index / biome depth, cut phase, timestamps, per-file checksums, source (host/solo), session member list.
  - `run.json` — run identity + generation baseline (seed carrier / `RandomState`, run settings, biome depth, total traveled, layer index).
  - `players.json` — per-player kernel state (status, limbs, skills, carry, inventory-transfer facts).
  - `items.json` / `items.jsonl` — every item fact (identity, revision, location, data, liquids, components), container trees included.
  - `world-entities.json` — opened entities, building health, trap state/consumption facts.
  - `enemies.json` — enemy facts + removed tombstones.
  - `fluids.json` — fluid region state.
  - `world-blocks.json` — host block difference table (deviations from the generated baseline) + partial block damage.
  - `world-transients.json` — the explicitly chosen transient payload (item motion, keypad codes, geyser liquid types, radiation line, world time, earthquake timers).
  - `characters/<steamid>.json` — per-player character data in the native `SaveInfo` shape plus CUO extensions, so the existing `CharacterDataFileStore` restore path stays compatible.
  - `mod-state/` — per-mod opaque payloads (host-persistent).
  - `native/` — optional copy of the native `save.sv` artifact when the cut is at a layer boundary.

## Current state (evidence)

Native game save:

- `SaveSystem.SaveGame()` writes `save.sv` = GZip(JSON `SaveInfo`) under `Application.persistentDataPath` — `reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:35-187` (write at `:186`), compression at `:458-473`.
- It contains character/run state only: body/limb fields, carried/worn items + `[Saveable]` components, recipes, `runTime`, `biome = biomeDepth + 1` (`:165-170`), `totalTraveled`, rarity multipliers, calories, run settings (`:151-181`).
- It contains **no world state**: no blocks/tiles, no world items, no enemies, no world entities, no fluids.
- `TryLoadGame()` (`:196-455`) restores the character/run fields and then **deletes** `save.sv` (`:453`); the world is regenerated from the run baseline.
- Native saves are layer-boundary by design:
  - `ConsoleScript.cs:787-794` — the `saveandquit` command decrements `biomeDepth` before saving so that "Loading the save will put you at the beginning of the current layer".
  - `WorldGeneration.SaveAndExit()` (`WorldGeneration.cs:1023-1030`) advances the layer then saves; `ContinueRun()` (`:1011-1020`) regenerates the layer.
- CUO's only host save hook today: `RunMenuReturnCoordinator.Flush` calls `SaveSystem.SaveGame()` before returning to the menu when the host leaves a live world — `src/CasualtiesUnknownOnline.GameAdapter/Run/RunMenuReturnCoordinator.cs:42-68` (call at `:58`).

CUO persistence today:

- `CharacterDataFileStore` — per-SteamID host-side character snapshots (protobuf, atomic write, survives a host restart, cleared on a new run). Decision #27; `docs/evidence/selfchecks/players/character-data-persistence-selfcheck.md`.
- `ModStateFileStore`, `HostBanFileStore` — mod state and host bans.
- `GameCheckpoint` (`src/CasualtiesUnknownOnline.GameState/GameCheckpoint.cs`) covers items, run, world entities, players, enemies, fluids, and optional random streams.
- `KernelSaveFileStore` + `KernelSaveFile` + `SaveHeader` (`src/CasualtiesUnknownOnline.Runtime/Session/Items/`) exist and are unit-tested, **but have no production caller**: `CuoBootstrap.BuildServiceProvider` never registers the store and `Plugin.cs:82-96` passes only `characterDataFile` / `modStateFile` / `hostBanFile`. The authoritative checkpoint is memory-only today.
- `GameCheckpoint.RandomStreams` is never populated in production (`GameStateStore.CreateCheckpoint` passes `null`; `docs/architecture/protocol.md:176-178`).
- Kernel item location is X/Y only (`src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemLocation.cs`); velocity / rotation / angular velocity / fresh-drop ride the wire and transient paths (`IItemControl.cs:22,37,50,122-123`), not the checkpoint.

World state the kernel does not own today (a mid-run save must capture it or explicitly regenerate it):

- Host block difference table (`WorldStateMessageService._damagedBlocks`, `:35`; cap `:38`) and partial block damage (`BlockDamageRegistry`).
- Keypad codes (host-generated lazily, random), geyser liquid types (per-side public-stream roll, host authority), radiation line, world time, earthquake timers.
- Native `WorldGeneration.blockDamages` list (cap 128, `WorldGeneration.cs:732-737`).
- Item physics transients (velocity / rotation / angular velocity / fresh-drop).
- In-flight operation state: pending block break + drops (`BlockBreakPendingState`), pending trap drops (`TrapDropPendingState`), pending pickup queue (`PendingPickupQueue`, 500 ms hold), drop pending (`DropPendingState`), medical/shrapnel sessions (`MedicalOperationSessionService`, `ShrapnelOperationSessionService`, `OtherMedicalOperationSessionService`), craft batches, entity creation deferred reports.

Design constraints discovered (not decisions yet):

- `CasualtiesUnknownOnline.GameState` may not add a PackageReference/ProjectReference (architecture guard), so save-format DTOs live in Runtime; domain→DTO mapping stays in Runtime (`KernelDomainWireMapper` / `KernelWireMapper` are the precedent).
- Runtime has no JSON library today (`Runtime.csproj` references only DI/logging/BepInEx plus Protocol/GameState). The JSON serializer choice must be decided: add `Newtonsoft.Json` or `System.Text.Json`, or a controlled hand-written writer. The GameAdapter can see `Newtonsoft.Json` through `Assembly-CSharp`, but the format layer must not depend on game assemblies.
- net48 ships `System.IO.Compression` / `ZipFile`, so a directory-entry archive needs no new runtime dependency beyond the JSON choice.

## Mid-run semantics: the hard part

A mid-run save is a **consistent cut** of an actively simulating world; a restore must be exactly-once.

- **Cut atomicity** — the save must not interleave with a command batch or a frame flush. Define one save seam on the host main-thread pump that freezes the kernel revision, all domain tables, and the native world tables at one instant; document what is in/out per frame phase.
- **No over-generation** — same-id dedup on restore; generation-time items/entities must not be re-materialized; already-materialized drops must not be re-created; container children have exactly one parent; Terminal facts (consumed/destroyed items, removed enemies) never resurrect; restore is idempotent (load twice = same world).
- **No under-generation** — every fact in the save materializes exactly once: item identities, container trees, world entities, block diffs, partial block damage, and character data for every player.
- **Transient policy** — every in-flight state above needs an explicit decision: capture, resolve-before-save (flush the pending operation into its committed form), or drop-with-a-log. Silent loss is forbidden.
- **Determinism** — restore must be reproducible; the save must carry enough of the run baseline (`RandomState`, layer index, layer modifiers) that a regenerated layer plus the diff reproduces the same world. `RandomStreams` must be populated if any domain's decisions depend on them.
- **Crash safety** — atomic write (temp → verify → replace); never destroy the previous save before the new one is validated; corrupt/foreign saves fail loudly with no partial load.
- **Version/build gating** — schema version + game build + CUO build; no silent migration; unknown fields preserved or rejected explicitly.

## Design questions to confirm with the user before coding

1. Save authority and multiplayer: host-only mid-run save. What does a save taken with guests present mean later — portable to a session where the same guests are absent/present? Are guest characters restored by SteamID?
2. Restore semantics: a mid-run restore must resume the *same* layer with all mutations. Confirm we never silently fall back to "restart the layer" (native semantics) when a package is incomplete.
3. Save slots: single rolling slot, or named slots with a slot list. Reuse the native save/continue surface if one exists (native-UI reuse rule); otherwise document the blocker and get direction.
4. Native interop: should a mid-run save also write a native `save.sv`? Only a layer-end cut is expressible natively; a mid-run cut is not.
5. v1 scope: full kernel checkpoint + world diff + character data, or a narrower slice first? What is explicitly out of v1 (mod state, host bans, trade state, medical sessions)?
6. Where the package lives and how it is named (placeholders only; no machine paths in tracked files).

## Staged plan

This is a multi-stage requirement. Split it into stage tickets when implementation starts; do not attempt one large change. Each stage lands with its own tests/gates/deploy verification before the next begins.

- **S1 — Format + package skeleton (no gameplay wiring)**: manifest schema, per-domain file layout, JSON serialization choice, archive read/write with directory entries, checksums, atomic replace, version/build gating, corrupt-file degradation; round-trip, corruption, and version-rejection tests.
- **S2 — Layer-end save/restore**: hook the layer boundary (native `SaveAndExit` / `ContinueRun` / `RegenerateWorld` plus CUO `AdvanceLayerCommand` / `RunAdvancedEvent` / `RespawnCoordinator`), capture the kernel checkpoint + character data + run baseline, restore into a freshly generated layer. Acceptance: identical character/run state, no duplicate or missing items.
- **S3 — Mid-run capture**: the consistent-cut seam, all domain payloads, the world diff, and the transient policy; restore into a regenerated layer baseline plus the applied diff. Acceptance matrix covers over/under-generation.
- **S4 — Multiplayer restore + validation/recovery**: guest reconnect/character restore into the loaded world, missing/foreign/corrupt save behavior, observability (per-domain counts, revision, cut phase), failure/degradation semantics.
- **S5 — Migration/coexistence (if needed)**: old protobuf kernel saves and native `save.sv` interplay; never guess silently.
- **S6 — Backup layer**: owned by `todo/systemic-save-backup-management.md`, reusing this package format.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Layer-end save → quit → continue | Next layer starts with the saved character/run state; no duplicate/missing items; native continue still works |
| 2 | Mid-run save with mutated terrain (mined/placed/quaked blocks + partial damage) | Reload reproduces the same block diff exactly |
| 3 | Mid-run save with world items (ground/containers/carried/worn) | Same identities, locations, and container trees; no duplicates, no loss |
| 4 | Mid-run save with opened/damaged building entities, consumed traps, fluids, enemies | Same facts; no re-trigger; no resurrection of Terminal facts |
| 5 | Save during an in-flight operation (block-break pending drops, trap drop hold, pickup queue, drop flush, medical/shrapnel session, craft batch) | The chosen policy is applied and logged; no silent loss or duplication |
| 6 | Multiple players (host + guests) | Every player's character state restored by SteamID; guest reconnect path works |
| 7 | Crash/kill during save | Previous valid save intact; next start loads it |
| 8 | Corrupt or foreign-build save | Loud failure, no partial load, no crash |
| 9 | Load twice / load → save → load | Idempotent; world fingerprint stable |

## Non-goals

- Cloud saves, cross-machine save sharing, anti-cheat on save contents, host migration.
- A second archive format for backups (reuse this package).
- Any wire-protocol change; this is a local disk/persistence surface.
