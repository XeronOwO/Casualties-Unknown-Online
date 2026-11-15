# Split the save archive reader at its seams

- Status: Todo
- Priority: Medium

## What

`CasualtiesUnknownOnline.Runtime.Persistence.SaveArchiveReader` is 619 lines. It owns three
seams that change independently:

- opening and validating the archive (manifest gate, checksums);
- decoding each domain entry and its per-entry salvage;
- the legacy `live/` fallback and its translation into the same in-memory shape.

The archive single-file ticket added the `live.cuoz` path without splitting the class, so the
ceiling is recorded in `docs/architecture-debt.json`. This ticket owns that debt.

## Must not change

- The manifest is the only hard gate; per-entry salvage keeps reporting skipped entries.
- An archive failure falls back to the legacy `live/` folder, then to a backup.
- Callers keep the same reader contract; this is an extraction, not a format change.

## Acceptance

1. The reader class falls below the 600-line ceiling; the debt entry is removed.
2. `SaveArchiveRoundTripTests`, `SaveArchiveDamageTests`, `SaveArchiveSalvageTests` and the
   legacy materialization path stay green.
3. The split names one owner per seam; no new pass-through type exists only to move lines.

## Run sheet

- [ ] Extract the archive-open/manifest gate; run the round-trip and damaged-manifest suites.
- [ ] Extract the legacy fallback; run the legacy-backed damage suite.
- [ ] Remove the debt entry and run the source-shape gate.
