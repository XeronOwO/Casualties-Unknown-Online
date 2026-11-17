# Split the save archive reader at its seams

- Status: Done (2026-11-18) — the open seam is its own gate; the restore was re-read on the real game.
- Priority: Medium

## Design

The reader serves the restore contract in
`docs/design/cross-cutting/saves-and-recovery.md`; the archive layout is in
`docs/architecture/save-archive-format.md`.

## What

`CasualtiesUnknownOnline.Runtime.Persistence.SaveArchiveReader` was 619 lines. It owned three
seams that change independently:

- opening and validating the archive (manifest gate, checksums);
- decoding each domain entry and its per-entry salvage;
- the legacy `live/` fallback and its translation into the same in-memory shape.

The archive single-file ticket added the `live.cuoz` path without splitting the class, so the
ceiling was recorded in `docs/architecture-debt.json`. This ticket owns that debt.

## Must not change

- The manifest is the only hard gate; per-entry salvage keeps reporting skipped entries.
- An archive failure falls back to the legacy `live/` folder, then to a backup.
- Callers keep the same reader contract; this is an extraction, not a format change.

## Acceptance

1. The reader class falls below the 600-line ceiling; the debt entry is removed.
2. `SaveArchiveRoundTripTests`, `SaveArchiveDamageTests`, `SaveArchiveSalvageTests` and the
   legacy materialization path stay green.
3. The split names one owner per seam; no new pass-through type exists only to move lines.

## What changed

`SaveArchiveManifestGate` (new, `Runtime/Persistence`) now owns opening and validating a
snapshot: archive entry listing for a one-file archive or an unpacked `live/` folder, the
manifest gate, the checksum policy, the legacy folder's file reads and the
recoverable-failure predicate. It carries the reader's entry-name validation and its refusal of
an archive whose paths repeat, verbatim.

The reader keeps the decisions: which source to try, repair mode, the damage notes, the backup
fallback, and the hand-off of each entry to `SaveSalvagePass`. Decoding was already that
object's job, so the extraction added no pass-through type.

Both snapshot shapes go through the one gate on purpose: whether a snapshot is sound cannot
depend on where its bytes live.

## Acceptance reading (2026-11-18)

- Reader: 619 → 421 lines; the `CasualtiesUnknownOnline.Runtime.Persistence.SaveArchiveReader`
  entry is gone from `docs/architecture-debt.json`; the sweep row moved 60 → 61.
- Unit 6744/6744, gates 836/836, `dotnet format` exit 0 (the source-shape and code-sweep gates
  that the debt entry had been holding are in that 836).
- Real game: focused `save-continue` passed on deployed artifact `0.1.0+96b3f01d2`
  (`.acceptance/savejoin-readersplit`, row 33.8 s, setup 14.1 s, case 19.1 s, suite wall 59.9 s).
  The restore loaded its cut through the extracted gate: `[SaveFacts] restored the live world:
  228 block-state row(s) written, 0 partial-damage row(s) applied, 49 keypad code(s) applied
  (the archive carried 49), 111 geyser type(s) applied (the archive carried 111), 138 recipe
  unlock row(s) applied (the archive carried 138), radiation line applied, 4 applied / 0 refused
  world-entity row(s), 0 row(s) not taken, restore complete.`

## Run sheet

- [x] Extract the archive-open/manifest gate; run the round-trip and damaged-manifest suites.
- [x] Extract the legacy fallback; run the legacy-backed damage suite.
- [x] Remove the debt entry and run the source-shape gate.
- [x] Re-read the restore on the real game (focused save-continue) and record the audit line.

## Residual

- The decode seam stays inside `SaveSalvagePass`; a future per-domain decode change opens that
  class, not the reader.
