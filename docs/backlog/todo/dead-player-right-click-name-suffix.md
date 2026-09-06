# Dead-player right-click context menu should show a dead status suffix in the title

- Status: Todo
- Priority: Medium
- Category: Remote player UI / context menu polish
- Source: User report (2026-09-06) — right-clicking a dead character shows only the player's plain name in the context-menu title. The user expects the title to read `名称(死掉了)` (for example `Alice(死掉了)`), making the dead state visible at the moment of right-click.

## Goal

When the right-click in-world player context menu targets a dead remote player,
the context-menu title (the first line above the action list) should include a
dead-status suffix alongside the player's display name, e.g. `Name (died)` /
localized equivalent.

## Current behavior

- `OnlineUiMemberRow` already carries `IsDead` (`OnlineUiMemberProjection`,
  `vitals is not null && !vitals.Alive`).
- The Players-page row already renders a dead status (`member.status_dead`)
  through `OnlineUiMemberListDrawer.BuildStatus`.
- The right-click context menu title only draws `row.Name`
  (`OnlineUiPlayerContextMenu.Draw` line 93); it does not append any dead
  suffix, and the target selector buttons also draw only `ctx.DisplayName`.
- The dead state is available in the projected row, so the fix is expected to
  be a UI/label projection change plus localization, not a new wire fact.

## Acceptance criteria

- Right-clicking a dead remote player shows the dead suffix in the context-menu
  title (localized; Chinese user-visible example `名称(死掉了)`).
- Right-clicking a living/unconscious remote player does not show the dead
  suffix.
- The suffix is consistent with the existing dead-state projection/localization
  used by the Players list; no duplicated/divergent dead-detection logic.
- Both host and guest views, and third-party views, use the same row projection.
- No authority/protocol change.

## Non-goals

- Not changing the dead-status rendering for the Players page list.
- Not adding a new wire field or remote state: `IsDead` already exists on the
  projected row.
