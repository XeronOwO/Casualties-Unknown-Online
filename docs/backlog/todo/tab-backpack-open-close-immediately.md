# Tab opens backpack then closes immediately

- Status: Todo
- Priority: Medium
- Category: Inventory / UI / input
- Source: User report — pressing Tab cannot open the backpack; it opens and closes immediately, as if Tab was pressed twice.

## Problem

Pressing Tab to toggle the backpack only flashes it open and then immediately
closes it. The observed behavior looks like a double Tab press: the inventory
never stays open.

## Reproduction

- Press Tab while not in the backpack.
- The backpack appears for a moment and then closes immediately.
- Repeated attempts have the same result.

## Expected behavior

- Pressing Tab once opens the backpack and keeps it open.
- Pressing Tab again closes it normally.
- No single Tab press is consumed twice by CUO or game input paths.

## Investigation directions

- Check whether CUO input interception / patch bridges consume the same Tab
  press or key-up event twice.
- Check whether the native backpack toggle is being opened and then immediately
  closed by another open/close path.
- Check whether the remote-backpack focus or Tab-switch transfer path is
  involved when the issue reproduces.
- Cover both local inventory and remote backpack views if applicable; record
  exact host/guest scenario during reproduction.

## Acceptance criteria (draft)

- A single Tab press opens the backpack and it remains open.
- The issue also does not regress remote backpack or container windows.
- Add regression evidence for the exact user reproduction before moving to
  `review/`.
