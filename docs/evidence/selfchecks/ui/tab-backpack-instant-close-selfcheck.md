# Tab opens backpack then closes immediately — self-check

> Status: Review (waiting for unified acceptance). This selfcheck records the
> root cause and regression evidence for the local Tab instant-close report.

## 1. Problem evidence

User report: pressing Tab toggles the native radial backpack only as a flash;
the inventory opens and closes immediately, like a double Tab press.

## 2. Root cause (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Per-frame remote-focus cleanup | `RemoteBackpackCoordinator.Update()` → `RemoteBackpackView.ClearIfStale()` runs every CUO frame (`src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs`). |
| 2 | Local inventory button render | `InvButtonBodyPatch.Postfix` runs on every `InvButton.get_body`, including the player's own native backpack buttons. |
| 3 | Unconditional native close | The old `RemoteBackpackView.Close()` wrote `PlayerCamera.main.radialOpen = false` even when `_focusedBody` was null. |
| 4 | Result | A normal Tab press set `radialOpen = true`; the next CUO cleanup or inventory-button render reset it to `false`, closing the backpack within the same/next frame. |

## 3. Changes

- `RemoteBackpackView.Close()` now returns immediately when there has never been
  a remote focus (`_focusedBody == null && _focusedSteamId == 0`). A real or
  stale remote focus still clears metadata and the native radial state.
- `InvButtonBodyPatch.Postfix` now returns immediately when
  `RemoteBackpackView.FocusedBody` is null; it no longer calls `Close()` for the
  player's own local backpack.

## 4. Regression evidence

- Test:
  `tests/CasualtiesUnknownOnline.Tests/Patching/RemoteBackpackViewCloseTests.cs`.
- Before fix: `Close_WithoutRemoteFocus_DoesNotCloseTheNativeLocalRadial` failed
  (radial written to false). After fix: passes.
- Full suite: 2313 passed / 0 failed.
- Build: 0 warnings / 0 errors.
- Gates: `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1`, `check-delivery.ps1` pass.
- Deployed to the physical game directory; deployed DLL SHA-256 matches the
  build output.

## 5. Runtime verification boundary

The regression test proves the native radial state is no longer touched by the
no-focus close path at the managed level. The real in-game Tab behavior still
needs the user's final dual-client acceptance; no claim of having manually
pressed Tab in the game is made.
