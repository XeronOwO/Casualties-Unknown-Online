# Split the world message services at their seams

- Status: In progress — slice 1 landed (the block-state family is its own channel, `WorldStateMessageService` 613 → 450,
  its debt entry deleted); slice 2 (`WorldService`, 603 lines) is what remains.
- Priority: Medium
- Design: `docs/design/mechanics/world-interaction.md`, `docs/design/game/worldgen-and-layers.md`

## Why

`docs/architecture-debt.json` records two services over the 600-line aggregate ceiling:

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

## Why this is not done in the same slice

The split touches every world channel and its tests; it is its own reviewable change. The debt entry
keeps the ceiling honest until it lands.

## Slice 1 (2026-11-18) — the block-state family

`WorldStateMessageService` 613 → 450 lines. The block difference table, its cap, the late-join snapshot, the drift
reconcile and the save layer's capture/apply surface moved into
`src/CasualtiesUnknownOnline.Runtime/Session/World/BlockStateChannel.cs`; the service keeps every public member as a thin
delegate, so `WorldService` and its callers are unchanged and the wire ids, payload shapes and log lines are identical.

- The `WorldStateMessageService` entry is deleted from `docs/architecture-debt.json`; the `WorldService` entry stays at
  603 for slice 2. `docs/design/audit/sweep.md` counts the new file (131 → 132).
- Two sync-coverage quotes were repointed at the file that now owns the code (the `_sender.Send(... WorldBlockState ...)`
  call and the table-cap guard) rather than loosened.
- Gates `836/836`, unit `6747/6747`, `dotnet format` exit 0 on the committed tree.
- Real-game reading on `0.1.0+664a2157306b9a5dca0cf854d34343ef16d7d1e5`:
  - `block-sync` **PASS** (`.acceptance/channel-block-sync`, case 1.0 s, total 1.5 s): a host break at (3,489) read back
    as air on the member, a member break at (5,489) read back as air on the host — the table still crosses both ways.
  - `save-continue` **PASS** (`.acceptance/channel-save-continue`, setup 14.8 s, case 18.8 s, total 33.8 s): the restore
    audit read `10 block-state row(s) written … restore complete`, so the save capture/apply surface still writes the
    table.
- Remaining (slice 2): `WorldService` 603 → under 600, one delegating member per channel, the flag bookkeeping moved into
  the channels that own those flags, and its debt entry deleted in the same change.
