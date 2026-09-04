# Remove legacy "View items" remote-inventory detail path — self-check (2026-09-04)

Closes the backlog item that recorded the removal of the pre-native-backpack
custom remote-inventory detail path. The native remote backpack and the
existing player interaction menu/quick panel are now the only remote-inventory
surfaces.

## 1. Mechanism inventory

| Mechanism | Evidence |
|---|---|
| Right-click "View items" fallback | Removed `member.view_items` action and `OpenPlayerDetails` from `OnlineUiPlayerContextMenu` |
| Players page/quick panel inline inventory expansion | `OnlineUiMemberListDrawer` now draws only the native "Open backpack" action; recursive container rows and per-depth take buttons removed |
| Dead pinned quick-panel entry | `OnlineUiOverlay.OpenQuickPanelFor`, `OnlineUiContext.OpenQuickPanel`, and `OnlineUiQuickPanel.Open(ulong)` removed |
| UI expansion state | `OnlineUiWindowState.ExpandedMember` / `ExpandedContainers` removed |
| Dead formatting helper | `RemoteInventorySnapshot.ToDisplayLines` removed (recursive projection and the mod API view remain) |
| Stale localization | `member.view_items`, `member.hide_items`, and the custom-item-detail-only keys removed from `LocalizationCatalog` |

## 2. Verification

- `dotnet build CasualtiesUnknownOnline.slnx --no-restore` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-restore` — 2181 passed / 0 failed.
- `dotnet format CasualtiesUnknownOnline.slnx --no-restore` — run.
- `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1`, `tools/check-delivery.ps1` — pass.
- No wire/protocol/event/entity changes: the diff is confined to Online UI
  presentation code, runtime UI projection/localization, tests, and docs.

## 3. Structure review

- Dead UI delegates/state and stale localization keys are removed instead of
  being left as alternate paths.
- `OnlineUiQuickPanel` still exists as the hotkey-docked interaction surface,
  but no longer has a pinned-open entry for the removed context-menu fallback.
- Native remote backpack authority/wire behavior is unchanged.
