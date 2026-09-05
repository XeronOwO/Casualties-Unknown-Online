# Tab opens backpack then closes immediately

- Status: Review
- Priority: Medium
- Category: Inventory / UI / input
- Source: User report — pressing Tab cannot open the backpack; it opens and closes immediately, as if Tab was pressed twice.

## Problem

Pressing Tab to toggle the backpack only flashes it open and then immediately
closes it. The observed behavior looks like a double Tab press: the inventory
never stays open.

## Root cause

The CUO remote-backpack focus static `RemoteBackpackView` was closed by `Close()`
from two per-frame paths even when no remote backpack was open:

1. `RemoteBackpackCoordinator.Update()` calls `RemoteBackpackView.ClearIfStale()`
   every CUO update; with no remote focus `IsOpen` is false and it called
   `Close()`.
2. `InvButtonBodyPatch.Postfix` called `Close()` whenever `IsOpen` was false —
   i.e. on the first render of the player's own local backpack.

`RemoteBackpackView.Close()` unconditionally wrote
`PlayerCamera.main.radialOpen = false`. A normal Tab press therefore opened the
native radial and the next CUO/render path immediately closed it, producing the
double-Tab appearance.

## Fix

- `RemoteBackpackView.Close()` is now a no-op when there has never been a remote
  focus (`_focusedBody == null && _focusedSteamId == 0`), so stale-cleanup and
  local inventory rendering can never close the player's own native radial.
- `InvButtonBodyPatch.Postfix` now returns immediately when there is no focused
  remote body; it no longer calls `Close()` for the local backpack.
- Existing remote-backpack close cleanup still runs when a real remote focus is
  stale or explicitly closed.

## Reproduction / expected behavior

- Press Tab while not in the backpack.
- The backpack opens and stays open.
- Press Tab again closes it normally.
- Remote backpack and container windows remain on the same remote-focus close
  rules.

## Regression evidence

- `tests/CasualtiesUnknownOnline.Tests/Patching/RemoteBackpackViewCloseTests.cs`:
  `Close_WithoutRemoteFocus_DoesNotCloseTheNativeLocalRadial` was observed to
  fail before the fix (native radial written to false) and passes after.
- Full suite: 2313 passed / 0 failed.
- Build, format, architecture/event-replay/entity-event-dispatch/delivery gates
  pass.
- Deployed to the physical game directory; deployed DLL SHA-256 matches the
  build output.

## Acceptance criteria

- A single Tab press opens the backpack and it remains open.
- The issue does not regress remote backpack or container windows.
- The regression test remains green.
