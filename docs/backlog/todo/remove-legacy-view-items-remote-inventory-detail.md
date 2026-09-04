# Remove legacy "View items" remote-inventory detail path

- Status: Todo
- Priority: Low
- Category: UI cleanup / legacy removal
- Source: User observation during right-click player menu review (2026-09-04). Record only; no code action taken yet.

## Goal

Investigate and, if confirmed, remove the legacy "View items" (`member.view_items`, `查看物品`) remote-inventory detail path that predates the current native remote-backpack + full right-click player interaction menu. The expected outcome is that the native "Open backpack" path and the existing interaction menu/quick panel remain the only supported remote-inventory surfaces.

## Background

The in-world right-click player context menu currently exposes both:

- **Open backpack** — opens the game's native radial backpack for the focused remote render clone via `IGameAdapter.OpenRemoteBackpack` (`OnlineUiPlayerContextMenu.cs:180-183`).
- **View items** — a fallback that either opens the standalone quick panel pinned to that player (`OnlineUiPlayerContextMenu.cs:185`, `OpenPlayerDetails` at `OnlineUiPlayerContextMenu.cs:245-256`) or opens the full Online window/Players page.

The native remote-backpack path is documented as the current supported way to inspect/take a remote player's inventory:

- `docs/evidence/selfchecks/players/native-remote-backpack-and-door-sound-selfcheck.md:51-54` — "Open backpack" appears in Players page, quick panel and right-click context menu; "the custom item list remains available via the existing 'View items' detail path."
- `docs/evidence/selfchecks/items/remote-backpack-container-take-selfcheck.md` — the native remote backpack now supports recursive container contents and cross-player take through the same host-authoritative decision surface.
- `docs/evidence/selfchecks/players/player-quick-panel-selfcheck.md:36-39` — the right-click "View items" path currently calls `OnlineUiOverlay.OpenQuickPanelFor`, pinning the quick panel to the target and expanding its inventory.

Relevant code:

- `src/CasualtiesUnknownOnline.Plugin/OnlineUiPlayerContextMenu.cs:185` — unconditional "View items" action in the right-click context menu.
- `src/CasualtiesUnknownOnline.Plugin/OnlineUiPlayerContextMenu.cs:245-256` — `OpenPlayerDetails` fallback.
- `src/CasualtiesUnknownOnline.Plugin/OnlineUiMemberListDrawer.cs:258-295` — Players page and quick panel row also expose "View items"/"Hide items" as a custom inline inventory expansion, alongside "Open backpack".
- `src/CasualtiesUnknownOnline.Plugin/OnlineUiOverlay.cs:186-192` — `OpenQuickPanelFor` is currently only used by the right-click "View items" path.
- `src/CasualtiesUnknownOnline.Plugin/OnlineUiContext.cs:120-121` — `OpenQuickPanel` delegate, currently only consumed by the right-click menu.
- `src/CasualtiesUnknownOnline.Plugin/OnlineUiQuickPanel.cs:37-42` — `Open(ulong target)` pins the quick panel to a specific remote; this is the "View items" entry path, while the quick panel itself also remains available through its session hotkey.
- `src/CasualtiesUnknownOnline.Runtime/Localization/LocalizationCatalog.cs:172,368` — `member.view_items` localization keys.

## Open questions before removal

1. Does "remove the whole feature" mean only the right-click context-menu "View items" entry, or also the custom inline `member.view_items` / `member.hide_items` inventory expansion shown on the Players page and inside the quick panel?
2. Is the native remote-backpack path sufficient for all current use cases? Verify especially:
   - recursive container contents and nested take;
   - host `AllowRemoteInventoryTake` visibility;
   - line-of-sight / `CanSee` gating;
   - cases where `IGameAdapter` or `OpenRemoteBackpack` may be unavailable/fail (custom UI is currently an independent fallback).
3. Should the quick panel's pinned-open API (`OpenQuickPanelFor`, `OnlineUiQuickPanel.Open(ulong)`) be removed as dead code if the context-menu entry is removed, or kept for a future entry point?

## Proposed acceptance criteria (for the later implementation cycle)

- The right-click menu no longer shows `查看物品` / "View items"; "Open backpack" and all interaction actions remain.
- If the broader scope is chosen, the custom inline inventory expansion is removed from the Players page and quick panel as well, leaving the native remote-backpack and quick-panel interaction buttons.
- Dead UI delegates/state and stale localization keys are removed with the feature (no dead code left behind).
- No wire/protocol/event/entity changes.
- Documentation that currently describes the custom "View items" path as current is updated (selfchecks/decisions/backlog references).
- `dotnet build`, `dotnet test`, `dotnet format`, and the repo gates pass.

## Non-goals

- Not removing the quick panel as a hotkey-docked interaction surface unless the user explicitly asks for it; the quick panel is broader than "View items".
- Not changing remote-inventory host authority or wire behavior.
- No implementation in this cycle — this ticket is a backlog record only.
