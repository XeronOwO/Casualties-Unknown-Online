# CUO world archive: package format and world repository

Normative format contract for the CUO save system. It defines the on-disk shape only;
the code that produces and consumes it lives in
`src/CasualtiesUnknownOnline.Runtime/Persistence/` and is staged by the tickets listed in
`docs/backlog/in-progress/save-system-mid-run-and-layer-end.md`.

Decisions 162–166 in `docs/decisions/active.md` record the user's choices that this document
implements. Where this document and the implementation disagree, the implementation is wrong.

## 1. Model

A **world** is the unit of identity. It owns one folder archive holding the current state and
N compressed archive backups — the Minecraft world layout, plus backups.

- The save system is **independent of the native `save.sv`**. CUO never writes `save.sv`, and a
  restored world never depends on it. The native file format cannot express a mid-run cut, so a
  coexistence scheme would only create two sources of truth.
- The **host is the only writer** (`AGENTS.md`: the host is the only save authority). Guests
  hold their own local settings only; they never write a world archive.
- The **world repository root** is `<CUO data root>/cuo/saves/`, where `<CUO data root>` is the
  game's `Application.persistentDataPath`. Being under the persistent-data root keeps the
  archive out of the game installation, so a game update or reinstall never touches it.
- Guests store nothing here; a guest's client-side copy would create a divergent second truth.

## 2. Directory layout

```text
<CUO data root>/cuo/saves/
  index.json                                  # world list + last-opened pointer
  <worldId>/                                  # one folder archive per world
    world.json                                # metadata: display name, times, kind, counters
    live/                                     # the current state, as JSON files
      manifest.json
      run.json
      players.json
      items.json
      world-entities.json
      enemies.json
      fluids.json
      world-blocks.json
      world-transients.json
      characters/<playerKey>.json
      mod-state/                              # reserved: populated by a later stage
    backups/
      layer-end-<yyyyMMdd-HHmmss>.cuoz        # ZIP archive: the same file set, one snapshot
      mid-run-<yyyyMMdd-HHmmss>.cuoz
      auto-<yyyyMMdd-HHmmss>.cuoz
```

- `live/` stays **unpacked** so a save is a fast file write, and a human can inspect or repair a
  world without unpacking anything.
- Every backup is a **single ZIP archive** (`.cuoz`) containing the same file set with directory
  entries preserved. A new domain adds a file inside the archive; it never rewrites one blob.
- `worldId` is immutable and is the directory key: `w-<yyyyMMdd>-<4 hex>`, generated once at
  world creation. `displayName` is player-facing and freely renameable; renames never move or
  rename a directory.
- `<playerKey>` is the player's **transport-scoped identity**, not a raw account id:
  - Steam transport: `steam-<steamId64>`.
  - IP-direct transport: `name-<sanitized display name>` — the mode has no account identity, so
    the display name is the only claim available.
  The two modes are distinct key spaces; a world written over Steam is never silently claimed by
  an IP-direct name collision, because the prefix differs.

## 3. File contracts

### 3.1 `index.json`

`{ schemaVersion, worlds: [{ worldId, displayName, lastSavedUtc, kind, layerIndex, playerCount }], lastOpenedWorldId }`

- Written after every successful save and after a rename.
- It is a **cache for the picker**, never the source of truth: a world folder that exists on disk
  but is missing from `index.json` is still listed (rebuilt from `world.json`). A world listed in
  `index.json` but missing from disk is dropped from the list with a warning.

### 3.2 `manifest.json`

The manifest is the **only hard gate** in the whole format (see §6). It carries:

- Schema identity: `schemaVersion`, `format` (constant `cuo-world-archive`).
- Provenance: `gameBuild`, `cuoBuild`, `protocolVersion`, `contentFingerprint`
  (the content-set fingerprint already used by world determinism; it is what lets a loader
  decide that a stored entry's content no longer exists).
- Cut identity: `worldId`, `displayName`, `kind` (`layer-end` | `mid-run` | `auto`),
  `runEpoch`, `globalRevision`, `layerIndex`, `biomeDepth`, `cutPhase`, `savedAtUtc`.
- Integrity: `files: [{ path, sha256, bytes }]` for every file in the snapshot, and
  `checksumPolicy` so the loader knows whether checksums are mandatory.
- Provenance of the cut: `cutPhase` names the host main-thread pump phase the cut was taken in
  (see §4); `saveReason` records what triggered it (`layer-advance`, `menu-return`, `command`,
  `auto-interval`, `pre-restore-backup`).

### 3.3 `world.json`

Player-facing metadata that must survive without reading the whole snapshot:
`worldId`, `displayName`, `createdAtUtc`, `lastSavedUtc`, `lastKind`, `layerIndex`,
`playerCount`, `runEpoch`, `saveCount`, `backupCount`.

### 3.4 Domain files

`run.json`, `players.json`, `items.json`, `world-entities.json`, `enemies.json`,
`fluids.json`, `world-blocks.json`, `world-transients.json` — one file per domain table. The DTOs
are Runtime types mirroring the typed kernel checkpoint
(`GameCheckpoint` → `KernelSaveFile` is the existing precedent) and are mapped in Runtime; the
`CasualtiesUnknownOnline.GameState` project stays dependency-free and never learns the format.

`characters/<playerKey>.json` holds one player character per file, in the native `SaveInfo`
shape plus CUO extensions, so the existing `CharacterDataFileStore` restore path stays usable.

`mod-state/` is reserved and empty until its own stage.

JSON is written with `System.Text.Json` (Runtime-owned package), UTF-8 without BOM, indented,
keys in a stable order, floats round-trip safe. Compression is ZIP (deflate); no extra runtime
package is needed because `System.IO.Compression` ships with net48.

## 4. Cut phases

A save is a **consistent cut**: it must not interleave with a command batch or a frame flush. The
capture seam runs on the host main-thread pump and reads one frozen revision; `cutPhase` in the
manifest records which phase the cut was taken in so a restore can prove what it holds. The
phase list is finalized in S3 together with the transient policy; until then `layer-end` cuts are
taken at the layer boundary, where no in-layer operation is in flight.

## 5. Write transactions

Every save is a transaction; a crash can only leave the previous snapshot intact.

```text
stage:   write <worldId>/.staging/  (manifest last, after all payload files)
verify:  re-read every file and compare against manifest checksums
backup:  ZIP .staging/ → <worldId>/backups/<kind>-<stamp>.cuoz.tmp → rename to .cuoz
commit:  rename <worldId>/live → <worldId>/.previous/ → rename .staging → live
         then delete .previous/ and refresh index.json
```

- The staging directory is always inside the target world folder, so the final rename is a
  same-volume metadata operation.
- `live/` is replaced only after the archive is safely written, so a crash between any two steps
  leaves either the old snapshot or the new one — never a half-written world.
- A leftover `.staging/` or `.previous/` from an interrupted run is detected at load: `.staging/`
  is discarded, `.previous/` is restored, both with a warning.

## 6. Restore and repair semantics

Decision 163: restore minimizes loss, and salvage is **per entry, not per domain**.

- The **manifest is the only hard gate**. If `manifest.json` cannot be read or parsed, the archive
  is *damaged*: it is never silently loaded. The loader falls back to the newest backup archive
  whose manifest reads, and reports the fallback loudly.
- If the manifest reads, the load proceeds in **repair mode**. Per domain file:
  - An unreadable domain file is skipped with a warning; the other domains still load.
  - A readable domain file is decoded **entry by entry**: an entry that cannot be materialized —
    unknown content id, prefab or template removed by a mod update, unmappable id — is skipped
    with a per-entry warning, and the remaining entries of that domain still apply.
- Repair mode never regenerates the layer, never changes `layerIndex`, and never writes over the
  player's current progress. It converges on the same world, minus the entries it names.
- Every skipped entry, every fallback and every mismatch is surfaced in-game (not only in the log):
  the count per domain, the reason, and the affected content id. Silent loss is forbidden.
- **Load twice = same world.** Restoring an already-restored snapshot is idempotent; validation
  applies the same dedup and exactly-once rules as the live restore path.

### 6.1 Version and build gating

- `protocolVersion` mismatch: the world is still opened in repair mode, with a loud warning that
  entities created by a newer protocol may not restore.
- `gameBuild` mismatch: repair mode with a warning.
- `schemaVersion` newer than the reader: the per-file/domain payload is skipped (unreadable file)
  rather than guessed; `schemaVersion` older than the reader needs an explicit reader-side
  mapping, never a silent assumption.
- Unknown JSON properties are preserved on rewrite when the writer has them, otherwise rejected
  explicitly — never silently dropped.

## 7. Backup policy

- Backups are written on: every save (the transaction in §5 always archives), layer transition,
  explicit player request, and the configurable interval (`auto` kind).
- Retention keeps the newest N archives (default 10), never deletes the newest archive, and never
  lets a prune failure corrupt a world.
- The interval and retention count are configuration, defaulting to an interval-based autosave
  that the host can turn off; the values are decided in S5 with the config surface.

## 8. Non-goals

- No cloud saves, no cross-machine save sharing, no anti-cheat on save contents, no host
  migration.
- No second archive format for backups — backups reuse this package format exactly.
- No wire-protocol change: this is a local disk/persistence surface.
- No coexistence with the native `save.sv` (decision 165).
