# Snapshot size reduction

- Status: Review
- Priority: Low
- Category: Networking observability / optimization
- Source: original backlog — Open work

## What landed

Checkpoint snapshots now use a checkpoint-local item-definition string table.
The dominant measured family in a correctness snapshot is the repeated game
item definition id on every item; instead of encoding the same string once per
item, `WireCheckpoint.ItemDefinitionTable` carries the unique ids once (chunk 0)
and each `WireItemIdentity` carries a 1-based index.

- `WireItemIdentity.DefinitionIndex` (0 = direct-string fallback) added.
- `WireCheckpoint.ItemDefinitionTable` added (chunk 0 only).
- `WireCheckpointAssembler.Split` builds the unique table and writes compact
  indexed identities; `Assemble` expands them before restoring.
- No change to item kernel model, command/event wire, state-stream payloads,
  chunk batching, or reliability semantics. `CheckpointSchemaVersion` bumped
  1 → 2 for the wire checkpoint shape change.
- Regression: repeated 600-item checkpoint encoded size drops from 25,732 bytes
  to 23,939 bytes (-1,793 bytes, ~7%); the size-budget test was red before the
  implementation and green after.

Selfcheck:
`docs/evidence/selfchecks/protocol/checkpoint-string-table-selfcheck.md`.

Verification: `dotnet build` clean, `dotnet format` clean, 2256 tests green,
architecture/event-replay/entity-event/delivery gates pass.

## Non-goals

- No delta encoding or per-field compression yet; this is the second
  measurement-driven, clearly-safe checkpoint reduction after the traffic
  baseline landed.
- No changes to the high-frequency state stream; if a future measurement shows
  that family dominates, a separate reduction ticket stays appropriate.
