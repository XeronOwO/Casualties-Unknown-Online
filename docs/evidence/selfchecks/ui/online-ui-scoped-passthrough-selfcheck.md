# Online UI scoped passthrough + transport exclusivity — self-check

Date: 2026-08-26
Scope: the quick panel and right-click context menu are IMGUI surfaces that
were not covered by the existing full-screen modal input guard, so clicks inside
those small panels could still fall through to the game's UGUI/menu controls.
The Home page also presented Steam and IP-direct transport sections at the same
time despite the router being mutually exclusive.

## What landed

- **Scoped raycast blocker surface** — `IGameAdapter.SetOnlineUiScopedBlocks`
  accepts `OnlineUiBlockRect` values (GUI space, Y down). The adapter's
  `OnlineMenuInputGuard` maintains transparent full-canvas UGUI blockers with an
  `OnlineScopedRaycastFilter` component: the blocker only accepts raycasts
  inside the CUO rectangles, so clicks outside a panel still reach the game.
- **Plugin gives panel bounds** — `OnlineUiOverlay` collects the quick panel's
  and context menu's current `Rect` values after OnGUI and forwards them to the
  adapter; empty clears the scoped blockers.
- **Full-screen modal unchanged** — the Online window still uses the existing
  `SetOnlineUiModal` full-screen guard (AdaptiveButton disable + full-rect
  blockers). Scoped blockers are additive for non-modal surfaces.
- **Transport mode selector** — `OnlineUiWindowState.TransportMode` and a small
  Steam / IP-direct selector on the Home page show exactly one transport's
  host/join controls at a time. This is presentation-only; the router is still
  switched only by the actual IP-direct host/join/leave actions.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Plugin → adapter | `OnlineUiOverlay.Draw` calls `SetOnlineUiScopedBlocks` with quick-panel/menu rects | `OnlineUiOverlay.cs` |
| Runtime boundary | `OnlineUiBlockRect` (GUI coords) + `IGameAdapter` method | `Runtime/GameAdapter` |
| Adapter guard | `OnlineMenuInputGuard.SetScopedBlocks` creates/destroys filtered blockers | `OnlineMenuInputGuard.cs` |
| UGUI filtering | `OnlineScopedRaycastFilter.IsRaycastLocationValid` converts Y-up screen point to GUI Y-down and tests the rectangles | `OnlineScopedRaycastFilter.cs` |
| Home UI | mode selector hides the inactive transport section | `OnlineUiHomeDrawer.cs`, `OnlineUiTransportMode.cs` |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx` — full suite green (1536 tests after
  the new scoped-block tests).
- New tests: `OnlineUiBlockRectTests` (inside/edges/empty), 
  `OnlineMenuInputGuardContractTests` (interface + adapter + guard + filter
  reflection).
- No protocol/wire change, no `ProtocolVersion` bump, no event/item/entity
  matrix touched. Architecture/event gates unaffected.
- Manual acceptance: not requested for the developer cycle; L0 + static
  evidence, no manual acceptance.

## Accepted limitations

- The scoped blocker is UGUI-only; AdaptiveButtons behind non-modal panels are
  not globally disabled because that would also block the world outside the
  panel. The raycast image prevents standard UGUI button/pointer delivery in
  the covered rectangle.
- World-space canvases are still not blocked.
