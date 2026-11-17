# Split the world message services at their seams

- Status: Done (2026-11-18) — both debt entries are gone: `WorldStateMessageService` 613 → 450 and `WorldService`
  600 measured (603 when the ticket was written), the ceiling met without raising the record.
- Priority: Medium
- Design: `docs/design/mechanics/world-interaction.md`, `docs/design/game/worldgen-and-layers.md`

## Why

`docs/architecture-debt.json` recorded two services over the 600-line aggregate ceiling:

- `CasualtiesUnknownOnline.Runtime.Session.World.WorldService` — 603 lines.
- `CasualtiesUnknownOnline.Runtime.Session.World.WorldStateMessageService` — 613 lines.

Both grew by hand as world messages were added one channel at a time; the run-end broadcast
(2026-11-14) was the last drop. A service that both owns a channel and holds the domain's state is the
smell the architecture rules name: the message plumbing belongs in a channel object, the decision in the
service.

## What the split looks like

The same shape `LayerDescentChannel` already proves in this folder:

1. One channel type per message family (`RunEndChannel`, `BlockChannel`, `EntryChannel`), each owning its
   send + receive + event, constructed with the sender and session only.
2. `WorldStateMessageService` keeps the state and delegates each channel through a lazily built property,
   exactly as `Descent` does today.
3. `WorldService` keeps one delegating member per channel; the bool/flag bookkeeping moves into the
   channels that own those flags.

## What must not change

- Every wire id, direction and payload shape.
- The dispatch path (`WorldService` → channel → event) and the events' names.
- `IWorldControl`'s surface: callers keep calling the same members.

## Acceptance rows

1. The gate `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` reads green with
   both debt entries deleted.
2. The unit suite and the normative gates stay at their current counts (no test deleted).
3. A live pair still reads: a host leaving the world pulls its member out (`role None`, page Home) and a
   member's own leave stays local.

## Why this was not done in one slice

The split touches every world channel and its tests; it is its own reviewable change. The debt entry kept
the ceiling honest until it landed.

## Slice 1 (2026-11-18) — the block-state family

`WorldStateMessageService` 613 → 450 lines. The block difference table, its cap, the late-join snapshot, the drift
reconcile and the save layer's capture/apply surface moved into
`src/CasualtiesUnknownOnline.Runtime/Session/World/BlockStateChannel.cs`; the service keeps every public member as a thin
delegate, so `WorldService` and its callers are unchanged and the wire ids, payload shapes and log lines are identical.

- The `WorldStateMessageService` entry is deleted from `docs/architecture-debt.json`. `docs/design/audit/sweep.md`
  counts the new file (131 → 132).
- Two sync-coverage quotes were repointed at the file that now owns the code (the `_sender.Send(... WorldBlockState ...)`
  call and the table-cap guard) rather than loosened.
- Gates `836/836`, unit `6747/6747`, `dotnet format` exit 0 on the committed tree.
- Real-game reading on `0.1.0+664a2157306b9a5dca0cf854d34343ef16d7d1e5`:
  - `block-sync` **PASS** (`.acceptance/channel-block-sync`, case 1.0 s, total 1.5 s): a host break at (3,489) read back
    as air on the member, a member break at (5,489) read back as air on the host — the table still crosses both ways.
  - `save-continue` **PASS** (`.acceptance/channel-save-continue`, setup 14.8 s, case 18.8 s, total 33.8 s): the restore
    audit read `10 block-state row(s) written … restore complete`, so the save capture/apply surface still writes the
    table.

## Slice 2 (2026-11-18) — the service tail closed at the ceiling

Slice 1 moved the block-state constants and the family's members out of `WorldService` as well, which is what took it
from 603 to the measured **600** — the over-ceiling lines were the last block-state members it still held. The shape gate
now reads green with the `WorldService` entry deleted from `docs/architecture-debt.json`; no further extraction was
needed, and no line was removed by contraction (the class keeps every `IWorldControl` member as the delegating seam).

- `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`: green with both entries deleted
  (acceptance row 1).
- Unit `6747/6747`, gates `836/836` after the entry's removal — no test deleted (acceptance row 2).
- Live pair on the same deployed artifact `0.1.0+664a21573`: `run-end` **PASS** (`.acceptance/channel-run-end`, case
  3.6 s, total 4.1 s, shared setup 23.2 s) — the host ends the run and the member is pulled out to the Home page
  (acceptance row 3).
- Residual: the `CharacterDataStore` debt entry (639 lines) stays with the transport-handover ticket, untouched here.
