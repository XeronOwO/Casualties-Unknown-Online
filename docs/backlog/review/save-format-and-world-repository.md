# S1 — Save format + world repository + backup I/O

- Status: Review (implemented 2026-09-10)
- Priority: High
- Category: Persistence / save system
- Source: Stage 1 of `docs/backlog/review/save-system-mid-run-and-layer-end.md` (design frozen 2026-09-10)
- Related: `docs/architecture/save-archive-format.md` (normative format contract), decisions 162–166 in `docs/decisions/active.md`, `review/save-layer-end-save-and-restore.md` (S2), `review/save-mid-run-consistent-cut.md` (S3)

## Scope

The format and the storage layer, with **no gameplay wiring**: no checkpoint capture, no run-state
read/write, no adapter hook, no native-UI change. After S1 the save system can be exercised end to end
by tests but the game does not save anything yet.

Deliverables:

1. `System.Text.Json` package reference on `CasualtiesUnknownOnline.Runtime` (net48).
2. `Runtime/Persistence/` types implementing `docs/architecture/save-archive-format.md`:
   - manifest / index / world-metadata DTOs with schema + build + protocol version fields and the
     per-file checksum list;
   - JSON read/write helpers (UTF-8 without BOM, indented, snake-free stable casing, float
     round-trip);
   - `SaveArchiveWriter` — the §5 write transaction: staging inside the world folder, manifest
     written last, verify by re-read, archive as `.cuoz`, atomic directory swap, crash-leftover
     handling;
   - `SaveArchiveReader` — manifest gate, per-file read, **per-entry salvage callback** so future
     domain decoders can skip a single unknown entry without dropping the domain, and damage
     reporting;
   - `WorldRepository` — root resolution, world creation (immutable `worldId`), enumeration driven
     by disk with `index.json` as a rebuildable cache, rename of the display name only, backup
     listing, retention pruning, and transport-scoped `<playerKey>` naming.
3. A machine-independent placeholder strategy for documentation and tests: no absolute machine path
   may enter tracked files (AGENTS.md red line); fixtures build their root under the test temp
   directory at runtime.

Explicitly out of S1: any domain payload capture/apply, the mid-run cut seam and transient policy
(S3), the adapter save/restore hook and the native continue entry (S2), guest restore (S4),
mod-state persistence, retention/interval config surface (S5/S6).

## API shape (decided here so S2–S4 only add payloads)

- `SaveArchiveWriter.WriteWorldSnapshot(worldId, kind, payload:, manifestMeta) → SaveWriteResult`
  owns the transaction; callers hand over `(relative path, byte[]/writer)` payloads and never touch
  staging, checksums, archives or rename ordering.
- `SaveArchiveReader.ReadManifest(...)`, `ReadFile(...)`, `ReadSalvage(...)` where the salvage path
  takes a per-entry decode callback and returns the accumulated damage report.
- `WorldRepository` exposes world listing/creation/rename, "load the newest valid snapshot for a
  world" with the backup fallback, and backup retention — filesystem and manifest concerns only.

## Red tests (defect-style only)

S1 is new-requirement work, so behaviour tests are written directly rather than as a red→green fix.
The cases that must exist:

| # | Case | Expected |
|---|---|---|
| 1 | Write → read round trip | Byte-identical payloads, manifest checksums verify, no leftover `.staging`/`.previous` |
| 2 | Payload changed after staging | Write fails before commit; `live/` still holds the previous snapshot |
| 3 | Crash simulation: leftover `.staging/` | Detected and discarded at load, previous snapshot intact, warning reported |
| 4 | Crash simulation: leftover `.previous/` | Restored at load with a warning |
| 5 | Corrupt manifest | Treated as damaged; no partial snapshot load; newest readable backup is offered |
| 6 | Corrupt domain file inside a readable manifest | Repair mode: the other files load, the damaged file is reported with its path |
| 7 | Unknown JSON entry inside a domain file | Per-entry salvage: the unknown entry is skipped and reported, the rest of that file applies |
| 8 | Newer `schemaVersion` | That file is skipped, not guessed; report names the schema version |
| 9 | `protocolVersion` mismatch | Opens in repair mode with a warning |
| 10 | Zip entry path traversal (`..`, absolute, drive-qualified) | Rejected loudly; nothing is written outside the world folder |
| 11 | `index.json` deleted / stale | The list still comes from disk; a missing world folder drops the stale entry with a warning |
| 12 | Display-name rename | Directory key unchanged, `world.json` and `index.json` updated |
| 13 | Backup retention | Keeps the newest N, never deletes the newest archive, prunes oldest-first |
| 14 | Backup archive contents | Directory entries preserved; a `.cuoz` unpacks to the same file set as the snapshot |

## Acceptance

- All cases above pass; full suite + gates green (`dotnet build`, `dotnet test`, `dotnet format`).
- No production behaviour change: the game does not write anything from this stage until S2 wires it.
- Independent adversarial review in a fresh context covering path safety, crash windows (every
  transaction step), retention edge cases (0/1/N archives, all-unreadable backups) and the
  no-leak/no-half-write contract.
- Deploy + hash verification is required only if S1 changes the deployed assemblies' behaviour; it
  does not add a runtime hook, so the stage closes on gates + review plus a deploy to keep the
  deployed DLLs identical to HEAD (the artifacts do change).

## Verification limits

The repository/format layer is fully machine-verifiable (real filesystem, real ZIP, simulated
crashes). Game-side saving and the native continue entry are **not** part of S1 and cannot be
claimed here; they belong to S2 and its dual-client pass.

## Delivery record (2026-09-10)

- **Code**: `src/CasualtiesUnknownOnline.Runtime/Persistence/` — format constants, DTOs, the JSON
  dialect (camelCase, kebab-case cut kinds, `checksumPolicy`), `SaveArchiveWriter` (§5 transaction:
  stage → verify by re-read → ZIP → verify the archive → atomic swap), `SaveArchiveReader` (manifest
  gate, backup fallback, per-entry salvage, damage report), `WorldFolderRecovery` (crash leftovers,
  abandoned archives), `WorldRepository` (world ids, `world.json`, rebuildable `index.json`,
  renames, backup listing/retention), `PlayerKey`, `ChecksumPolicy`.
- **Tests**: `tests/CasualtiesUnknownOnline.Tests/Persistence/` — 97 cases covering the 14-case
  table plus the boundary batches the review named (path escapes, both crash windows, retention
  0/1/N/negative, all-unreadable backups, duplicate manifest/ZIP paths, hostile entry names, the
  no-leak/no-half-write contract, actor-key distinctness).
- **Package**: `System.Text.Json` 8.0.5 on Runtime; `System.IO.Compression` referenced explicitly
  (net48 keeps it out of the default reference set). The deployed set therefore gains
  `System.Text.Json`, `System.Text.Encodings.Web` and `System.ValueTuple`, and upgrades
  `Microsoft.Bcl.AsyncInterfaces` to 8.0.0.0.
- **Gates**: `dotnet build` 0 warnings/0 errors; `dotnet test` 2778 + 32 gate cases green;
  `dotnet format` clean.
- **Deploy**: `tools/deploy.ps1` to the physical machine, all 32 deployed DLLs hash-identical to the
  build output; the deployed `System.Text.Json` assembly also loads and serializes outside Unity.
- **Independent adversarial review**: a fresh-context subagent reviewed the stage against the format
  contract and found six defects the first implementation missed — cross-kind backup ordering
  (retention/fallback picked the wrong archive), a manifest gate satisfied by DTO defaults, an
  unvalidated `worldId` that could create folders and prune archives outside the repository root,
  PascalCase/kebab-mismatched JSON keys, reader exceptions escaping on hostile archives, and
  non-Latin display names collapsing onto one player key. All six are fixed and each carries a
  permanent regression case; the reviewer's probes are folded into the suite
  (`SaveArchiveContractFixesTests`, `WorldRepositoryContractFixesTests`, plus cases in the existing
  path-safety, damage, salvage and player-key classes).
- **Not verified here**: no gameplay wiring exists in S1, so no in-game save/restore behaviour is
  claimed; the dual-client pass belongs to S2/S4.
