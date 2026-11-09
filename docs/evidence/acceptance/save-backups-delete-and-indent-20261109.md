# The save page: delete and the block indent (batch 20261109-a)

## What was read

The deployed build `0.1.0+93e92dde`, one host client, the Worlds page (the save tab, translated from the catalogue) opened through the window's own tab.

## The reading

- Every run row carries the "Delete this run" control (translated from the catalogue) beside its save-points control; the
  expanded run carries "Delete this save point" under its save point, next to "Continue from this save point".
- The save points' block steps in: the save-point line and its two buttons start at x≈330 px, while the run's own rows
  start at x≈313 px — the block indent is the row's (a layout spacer), not spaces in a label.
- The frame is `worlds-delete-20261109.png`; the crop below it is the save-point block.

## Limits

- The two-step confirm and the refusals were not driven live in this batch: the click path (a first click arming, a second
  confirming) is the same control path the restore's confirmation already used, and the refusals are unit-covered
  (`SaveArchiveDeleteTests`: an unknown name, the last save point, an invalid world id).
- The list's frame and the in-window editor for the retention ceiling remain open on the ticket.
