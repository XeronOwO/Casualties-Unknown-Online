# Remove legacy "View items" remote-inventory detail path

- Status: Review
- Priority: Low (completed)
- Category: UI cleanup / legacy removal
- Source: User observation during right-click player menu review (2026-09-04).

## Goal

Remove the legacy "View items" (`member.view_items`, `查看物品`) remote-inventory
detail path and its supporting inline inventory expansion state, leaving the
native remote backpack and the existing interaction menu/quick panel as the only
remote-inventory surfaces.

## Outcome

Implemented 2026-09-04. The custom inline inventory expansion was removed from
the Players page and the quick panel; the right-click context menu no longer
offers "View items". The native "Open backpack" button remains available in all
three surfaces. The dead pinned-open quick-panel path and stale localization
keys were removed as part of the same cleanup.

## Changes

- `OnlineUiPlayerContextMenu`
  - Removed the unconditional "View items" action.
  - Removed the `OpenPlayerDetails` fallback (quick-panel pin or full Players page).
- `OnlineUiMemberListDrawer`
  - Replaced the inline inventory toggle/expanded item list with a single
    native "Open backpack" action.
  - Removed recursive container entry drawing, per-depth take buttons and
    container-expansion state.
- `OnlineUiWindowState`
  - Removed `ExpandedMember` and `ExpandedContainers`.
- `OnlineUiOverlay`
  - Removed `OpenQuickPanelFor` and its `OnlineUiContext` delegate wiring.
- `OnlineUiQuickPanel`
  - Removed the now-unused `Open(ulong)` pinned-target entry point.
- `OnlineUiMemberRow` / `OnlineUiMemberProjection`
  - Removed the full inventory collection from the UI row (the summary text and
    takeable-item projection remain).
- `RemoteInventorySnapshot`
  - Removed the dead `ToDisplayLines` formatter used only by the removed
    inline detail view.
- `LocalizationCatalog`
  - Removed the `member.view_items`, `member.hide_items`, and the other
    custom-item-detail-only keys (`member.empty`, `member.slot`, `member.worn`,
    `member.inside`, `member.open_container`, `member.close_container`).
- Docs: updated the relevant selfchecks and this backlog ticket.

Selfcheck: `docs/evidence/selfchecks/players/remove-legacy-view-items-remote-inventory-selfcheck.md`.

## Acceptance criteria

- The right-click menu no longer shows `查看物品` / "View items"; "Open backpack"
  and all interaction actions remain.
- The custom inline inventory expansion is removed from the Players page and
  quick panel as well, leaving the native remote-backpack and quick-panel
  interaction buttons.
- Dead UI delegates/state and stale localization keys are removed with the
  feature (no dead code left behind).
- No wire/protocol/event/entity changes.
- Documentation that currently describes the custom "View items" path as
  current is updated.
- `dotnet build`, `dotnet test`, `dotnet format`, and the repo gates pass.

## Non-goals

- Not removing the quick panel as a hotkey-docked interaction surface; the quick
  panel is broader than "View items".
- Not changing remote-inventory host authority or wire behavior.
