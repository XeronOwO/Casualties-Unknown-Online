# Split the world message services at their seams

- Status: In progress — the debt is recorded by the file-size gate; this ticket owns the split.
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
