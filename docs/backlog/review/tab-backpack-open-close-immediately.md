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

## Machine-row attempt (2026-10-24) — the one row still owed

On artifact `0.1.0+7b02e9d3` the regression test is green (the full suite: 6357 behaviour + 779 gates), but
the physical first criterion could not be driven from this environment:

- The in-process `key-tap` route posts `WM_KEYDOWN`/`WM_KEYUP` to the client's own window (the tap reported
  `posted=true`), and it does NOT reach this feature: the game's toggle reads
  `Input.GetKeyDown(KeyBinds.GetBind("toggleinventory"))` in `PlayerCamera`'s frame update, and Unity's
  `Input` does not see a posted window message (it reads the OS input state). The probe after the tap read
  `radialOpen=false`, scale 0.01, both immediately and 2.5 s later — the backpack never opened.
- Sending a REAL key needs an interactive desktop, and this agent shell has none: `GetForegroundWindow`
  returns 0 after `SetForegroundWindow` on the client's own window (PID 36112, window 3739572), and
  `keybd_event` reaches no game.

So the remaining row is the user's physical Tab press in their own session (the goal's standing
keyboard/mouse authorization covers the acceptance, but the window station this shell runs in cannot
deliver it). The fix and its regression test are unaffected; the ticket stays in `review/` for that one
row.
