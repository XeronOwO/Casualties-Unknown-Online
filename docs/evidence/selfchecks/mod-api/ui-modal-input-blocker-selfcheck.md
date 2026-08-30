# UI modal input blocker — stop clicks leaking through the Online window (no protocol bump)

Date: 2026-08-23
Scope: the CUO Online window is IMGUI, which does not participate in Unity's
UGUI EventSystem. Clicks on the window's non-control areas were reaching the
game menu/world controls behind it.

## What landed

- **`OnlineMenuInputGuard`** (GameAdapter): a modal input guard owned by the
  adapter. When the Online window is open it:
  - disables all game custom `AdaptiveButton` components (their `Update` uses
    raw `Input.GetMouseButtonDown`, so a UGUI raycast blocker alone cannot stop
    them);
  - adds a transparent full-rect `Image` raycast blocker to every active
    screen-space `Canvas`, so standard UGUI buttons behind the window are not
    clickable;
  - restores the captured `AdaptiveButton.enabled` states (guest menu-lock
    rules preserved) and destroys the blockers when the window closes.
- **`IGameAdapter.SetOnlineUiModal(bool)`**: the Plugin tells the adapter
  whether the Online UI modal is open, once per frame from `Update`.
- **`UnityEngine.UIModule.dll`** is now an on-demand GameAdapter reference
  (Canvas/RenderMode types); `references/README.md` updated.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Plugin → adapter | `SetOnlineUiModal` called with `OnlineUiOverlay.IsWindowVisible` | `Plugin.Update`, `OnlineUiOverlay` |
| AdaptiveButton | Disabled while modal open; restored with guest rules | `OnlineMenuInputGuard` |
| UGUI buttons | Transparent full-screen raycast blockers on active screen-space canvases | `OnlineMenuInputGuard.CreateRaycastBlockers` |
| Restore | Captured enabled states restored on close; blockers destroyed | `OnlineMenuInputGuard.EndModal` |

## Verification design

- **Build + gates**: `dotnet build` 0 warnings/errors; `dotnet format`;
  `check-architecture.ps1` pass; full suite 1293 tests green.
- **Runtime smoke**: launched the real game after deploy — plugin loaded and
  Game Adapter installed, no CUO-originated exception.
- The actual click-suppression is a Unity input behavior, so it is verified by
  the implementation + runtime smoke; no automated input simulation is present
  in the current test suite.

## Accepted limitations

- The guard activates only while the CUO Online window is open; outside the
  window clicks remain normal.
- World-space canvases are not given raycast blockers (the Online window is
  screen-space and world-space UI is not in the click-through path reported).
