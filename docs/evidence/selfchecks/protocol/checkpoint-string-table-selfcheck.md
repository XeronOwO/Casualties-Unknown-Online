# Checkpoint Item-Definition String Table — Self-Check

Owner cycle: autonomous backlog selection. Chosen item:
`docs/backlog/todo/snapshot-size-reduction.md`.

## 1. Problem evidence

A kernel checkpoint sends every authoritative item as a full
`WireItem` / `WireItemIdentity`. In a normal world most items share a small set
of game definition ids (shell, stone, wood, water bottle, ...), but the wire
encoded the same definition string once per item. The measured 600-item
checkpoint baseline from `NetworkTrafficBaselineTests` was **25,732 bytes** over
3 chunks before this cycle.

## 2. Design

- Add one checkpoint-local string table: `WireCheckpoint.ItemDefinitionTable`
  (chunk 0 only).
- Add `WireItemIdentity.DefinitionIndex` (1-based, 0 = direct string fallback).
- `WireCheckpointAssembler.Split` builds a unique definition table from the
  checkpoint items, writes each item's definition as an index, and leaves the
  per-item string empty.
- `WireCheckpointAssembler.Assemble` expands every indexed identity back to its
  string from chunk 0's table before restoring the kernel checkpoint.
- The table is optional/backward-compatible: an item with `DefinitionIndex == 0`
  still carries the direct `DefinitionId`, so old/mixed checkpoint data remains
  restorable.
- `CheckpointSchemaVersion` is bumped 1 → 2 because the wire checkpoint shape
  gains the table/index fields.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Repeated definition ids | one table entry per unique id instead of one string per item | `WireCheckpointAssembler.Split` definition-table build |
| Compact wire identity | `DefinitionIndex` rides the item; `DefinitionId` empty in checkpoint chunks | `WireItemIdentity.cs` |
| Restore | indexed identities are expanded before kernel mapping | `WireCheckpointAssembler.Assemble` + `ExpandItemDefinition` |
| Backward-compatible direct ids | index 0 leaves `DefinitionId` untouched | `ExpandItemDefinition` early return |
| Size regression | 600 repeated "shell" items: 25,732 → 23,939 bytes (-1,793 bytes, ~7%) | `NetworkTrafficBaselineTests.CheckpointSnapshotSize_RepeatedDefinitionIds_OverheadBudget` |

## 4. Red → green

- Red: the new size-budget test was run before the implementation; it failed with
  the 25,732-byte baseline exceeding the 24,000-byte budget.
- Green: after the string table landed, the same test passes at 23,939 bytes.
- Additional roundtrip coverage:
  `KernelWireMapperTests.CheckpointSplit_CompressesRepeatedDefinitionIdsIntoTable`
  verifies the table contents, per-item index, empty per-item string, and the
  restored definition id.

## 5. Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **2256 passed / 0 failed**.
- `dotnet format CasualtiesUnknownOnline.slnx` — clean.
- `tools/check-architecture.ps1` — passed.
- `tools/check-event-replay.ps1` — passed (33 events).
- `tools/check-entity-event-dispatch.ps1` — passed (33 kinds x 3 tables).
- `tools/check-delivery.ps1` — passed (7 boxes checked).

## 6. What was NOT changed

- No change to the item kernel model, command/event wire shape, or state-stream
  payloads. The table exists only inside checkpoint chunks.
- No change to checkpoint chunk batching or reliability semantics.
- No new NetMsg; no update to the envelope/protocol version paths beyond the
  checkpoint schema constant.
