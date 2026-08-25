# Player quick panel — self-check (2026-08-25)

Closes the backlog "Dedicated standalone player-interaction UI" design row by
implementing a compact, hotkey-docked panel that exposes the same co-op actions
as the Players page without opening the full Online window. The existing
in-world right-click context menu remains the transient cursor-based entry.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Existing interaction eligibility | `OnlineUiMemberProjection.Build` produces `OnlineUiMemberRow` action flags (carry/piggyback/drop/heal/use/push/recruit/take) from the runtime session surfaces; the Players page and right-click menu both consume it. |
| Existing row rendering | `OnlineUiMemberListDrawer.Draw` renders one member card with status, world actions, explicit heal/use selectors and inventory expansion. |
| Remote positions | `EntitySyncService.RemotePlayers` / `GetRemotePlayer` provide authoritative world positions; the OL overlay already uses them for nameplates/arrows and right-click hit-testing. |
| Local position | `EntitySyncService.LocalPlayer.Position` is the local player's authoritative position. |
| Hotkey pattern | `Plugin` already binds `[Session] CreateLobbyKey / JoinLobbyKey / PingPeerKey` and calls `HotkeyPressed` each frame; the new panel key follows the same pattern. |
| UI boundary | `OnlineUiOverlay.HandleContextMenuInput` already ignores right-clicks inside the modal Online window and the open context menu; the quick panel is added to that same boundary. |

## 2. Changes

- **Decision** — a dedicated panel is better than requiring the full modal
  window for frequent co-op interactions, while the transient right-click menu
  remains useful for cursor-proximate targets.
- **Panel** — `OnlineUiQuickPanel` is owned by `OnlineUiOverlay`, drawn at the
  bottom-right with `OnlineUiTheme`, and shows the selected in-world remote's
  name/status/vitals/inventory plus every eligible interaction button through
  the existing `OnlineUiMemberListDrawer`.
- **Target selection** — `QuickPanelTargetPicker.Resolve` is pure Runtime code:
  keeps the current target while it is still an in-world remote, otherwise
  picks the nearest remote (squared distance, SteamId tie-break). The panel
  draws a compact target selector when more than one remote is in-world.
- **Hotkey** — `[Session] InteractionPanelKey` (default `F6`, `KeyCode` name)
  toggles the panel; `ESC` closes it.
- **Input boundary** — right-clicks inside the quick panel are ignored by the
  world context-menu handler.
- **No protocol change** — no new `NetMsg`, no `ProtocolVersion` bump, no
  event/item/entity matrix row touched.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1499 passed |
| `QuickPanelTargetPickerTests` | 5 passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no event mechanism touched) |
| `dotnet format` | run |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `QuickPanelTargetPickerTests` locks empty input, current-target retention,
  nearest fallback, stale-target fallback, and SteamId tie-breaking.
- The action buttons themselves are not duplicated: the panel reuses
  `OnlineUiMemberListDrawer.Draw`, so every eligibility rule already has the
  existing L0 projection coverage.
- The hotkey/config path is a thin `Plugin.Update` branch; no new gameplay or
  network logic.

## 5. Structure review

- `OnlineUiQuickPanel` is a small UI-only class (presentation state plus
  rendering).
- `QuickPanelTargetCandidate` and `QuickPanelTargetPicker` are pure Runtime
  types, one file/type each.
- `OnlineUiOverlay` gains one field and one draw call; `Plugin.cs` gains one
  `ConfigEntry` and one hotkey branch.
- No class approached the 600-line gate as a result of this change.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选并完成"), so this cycle's plan is approved
without a separate interactive approval step.
