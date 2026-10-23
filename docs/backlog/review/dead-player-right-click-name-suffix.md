# Dead-player right-click context menu should show a dead status suffix in the title

- Status: Review — the suffix landed (label helper, both call sites, en/zh localization, three unit cases,
  selfcheck). The machine menu probe (2026-10-24) shows the title drawn from the shared label but CLIPPED at the
  panel's fixed 240 px width, so a long display name hides the suffix; whether the panel grows or the name
  elides is a user-visible choice recorded below, and the dead-suffix row stays unverified for long names until
  it is made.
- Priority: Medium
- Category: Remote player UI / context menu polish
- Source: User report (2026-09-06) — right-clicking a dead character shows only the player's plain name in the context-menu title. The user expects the title to read `名称(死掉了)` (for example `Alice(死掉了)`), making the dead state visible at the moment of right-click.
- Landed: 2026-09-06 — context-menu title and duplicate-name target selector buttons now append the localized dead suffix from the existing `OnlineUiMemberRow.IsDead` projection; Players-list dead rendering and wire/protocol unchanged. Evidence: `docs/evidence/selfchecks/ui/dead-player-context-menu-title-selfcheck.md`.

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
- **The suffix landed**: `OnlineUiMemberLabel.FormatContextTitle(displayName, isDead, deadSuffix)` appends
  the caller's localized suffix only when the row is dead, and both the context-menu title and its target
  selector buttons build through it; `LocalizationCatalog` carries `member.context_dead` in both languages
  (en `(dead)`, zh `(死掉了)`). Unit tests pin dead / living / unconscious
  (`OnlineUiMemberLabelTests`); selfcheck `docs/evidence/selfchecks/ui/dead-player-context-menu-title-selfcheck.md`.

## The machine menu probe (2026-10-24) — a real finding, and a user-visible choice

A probe opened the production context menu on the host for the living member (`Open` on the same
`OnlineUiPlayerContextMenu` the right-click calls) and captured the client's own screen
(`ScreenCapture.CaptureScreenshot`): the menu draws, and its title is the shared label. **But the title is
CLIPPED at the panel's fixed `Width = 240f`**: the member's display name `Kagoya（小号）` already fills the
title row (`Kagoya（小…` in the frame), so a suffix appended after such a name would not be visible at all.
The clip is pre-existing for the name itself; with the suffix it decides whether the ticket's row can be
seen for long names.

**Decision point (user):** should the menu (a) grow its width to fit the title, or (b) elide the display
name when the title exceeds the panel and keep the dead suffix visible? The ticket's own row wants the dead
state visible at the moment of right-click, which (b) guarantees and (a) makes conditional on the screen
width. The agent did not choose: the panel's width is a layout decision with a visible trade-off, and the
frame also shows the fixed width matching the action buttons. Until the user picks, the row is unverified
for long names; a short-named player would render the suffix today.

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
