# Online UI polish + world-time sound fix — self-check (2026-08-23)

This cycle addresses several user-visible Online UI issues plus one repeated
world-time sound annoyance.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Online UI pages | `OnlineUiWindow` + `OnlineUiHomeDrawer` / `OnlineUiPlayersDrawer` own the tabbed shell and page content. |
| Lobby metadata | Players page previously showed lobby id / owner / member count / copy / leave; Home already owned the session section. |
| Top-left HUD | `OnlineUiOverlay.DrawNetworkHud` drew a 250x86 dark panel over the game's hand-item readout. |
| Modal input blocking | `OnlineMenuInputGuard` disables `AdaptiveButton` and adds transparent UGUI raycast blockers; `PlayerCamera.HandleInput` (PlayerCamera.cs:843-880) still handled pause/ESC keyboard behind it. |
| World-time resend | `WorldTimeSync` broadcasts the applied speed every 5 s; guests called `PlayerCamera.SetTimeScale(..., switchSound: true)` on every receipt, replaying the speed-change UI sound even when the speed had not changed. |

## 2. Changes

- **Home / Players split** — lobby identity, owner, member count, copy-id and
  leave/close now live in the Home session block; the Players page is now just
  the roster + direct interaction surface.
- **Top-left HUD** — removed the background panel and the mode/role/title
  lines; only live RTT and the latest delayed session event are shown, and the
  event hold is extended from 4.5 s to 15 s.
- **Modal background click + ESC** — `OnlineUiTheme.Window` now neutralizes
  hover/active/focused window backgrounds so clicking the UI does not tint it;
  `PlayerCamera.HandleInput` and `PauseHandler.TogglePause` are suppressed while
  the Online UI modal is open; ESC closes the modal in `OnGUI`, and the one-frame
  `CuoEscCloseSuppression` keeps the modal guard active on that closing frame so
  the same ESC cannot open the native pause menu regardless of Unity event order.
  The non-modal quick panel also suppresses `PauseHandler.TogglePause` while it
  is open through `IPatchBridge.IsNonModalEscapeSurfaceOpen`.
- **World-time sound** — `WorldTimeSync.OnTimeReceived` now ignores an
  authoritative speed that equals the already-applied speed, so the 5 s
  periodic resend no longer replays the switch sound. A real speed change still
  plays it once.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1309 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed |
| Patch-contract suite | the new `PlayerCamera.HandleInput` contract resolves against the game assembly |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof / static evidence

- The new `PlayerCamera.HandleInput` patch is auto-contracted by
  `PatchInventory` and validated by `PatchContractTests` against the game
  assembly; the `IsOnlineUiModalOpen` bridge flag comes from the same
  `OnlineMenuInputGuard` state that already drives the raycast/AdaptiveButton
  suppression.
- The world-time change is a pure guard against re-applying an unchanged speed;
  the existing `WorldTimeFlowTests` still lock the wire path.
- The UI changes are layout/style-only over existing runtime facts.

## 5. Structure review

- No new DI services or shared mutable state were added: the modal flag remains
  owned by `OnlineMenuInputGuard`, and the ESC close is local UI state.
- `PlayerCameraHandleInputPatch` is a thin one-condition Harmony adapter.
- `WorldTimeSync` remains one deep module; the change only skips an idempotent
  no-op call.
