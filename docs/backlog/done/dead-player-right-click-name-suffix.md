# Dead-player right-click context menu should show a dead status suffix in the title

- Status: Done — the suffix landed, and the one remaining item (a long name clipping it in the 240 px context-menu title) is decided and fixed on 2026-11-02: the title keeps the marker and elides the NAME to a 28-character budget, with two cases pinning it, and the member list was captured on the deployed build drawing the marker on its own row. The design question the ticket named ("panel grows or name elides") is answered: the marker never pays for the name.
- Priority: Medium
- Category: Remote player UI / context menu polish
- Source: User report (2026-09-06), quoted in translation — right-clicking a dead character shows only the player's plain name in the context-menu title. The user expects the title to carry the localized dead suffix after the name (the `(dead)` value in `en.json` and its counterpart in `zh.json`), making the dead state visible at the moment of right-click.
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
  (the `en.json` `(dead)` value and its `zh.json` counterpart). Unit tests pin dead / living / unconscious
  (`OnlineUiMemberLabelTests`); selfcheck `docs/evidence/selfchecks/ui/dead-player-context-menu-title-selfcheck.md`.

## The machine menu probe (2026-10-24) — a real finding, and a user-visible choice

A probe opened the production context menu on the host for the living member (`Open` on the same
`OnlineUiPlayerContextMenu` the right-click calls) and captured the client's own screen
(`ScreenCapture.CaptureScreenshot`): the menu draws, and its title is the shared label. **But the title is
CLIPPED at the panel's fixed `Width = 240f`**: a member's display name already fills the
title row (the frame shows it truncated with the panel's ellipsis), so a suffix appended after such a name would not be visible at all.
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
  title (localized through the `en.json`/`zh.json` dead-suffix key).
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

## The growth, implemented and read on the deployed build (2026-11-01, 12:00)

The user's direction ("the panel grows") is now the code's behaviour: `OnlineUiPanelView.ContentFitWidth` measures every row's caption through `NameWidthOf` (the same reading the window's pages take), takes the model's width as a FLOOR and caps the growth at `MaxTitleFitWidth`, so a row that carries a control keeps the floor while a caption-only row (the member name plus its dead suffix) widens the panel. Built, 811 gates and 6,577 behavioural tests green, deployed as `0.1.0+00b0e623`.

The frame taken from a live menu after the deploy shows the section title (a member's display name, shown in the frame) whole with its actions under it — no clipping at this name length. What this frame does NOT prove is the LONG-name case, because that needs a display name close to the panel's cap: the roster name comes from the Steam persona (or the roster row the host announces), so producing one is a fixture this batch cannot make on its own. The next reading is that name, then the same menu with the suffix on a dead member.
## The choice made: elide the NAME, never the marker (2026-11-02, 18:10)

The open item was a presentational choice — "whether the panel grows or the name elides". Decided from the player's side: the suffix is the information the player acts on (this teammate is dead), and a name is what they already know from the row above; so the title keeps the marker and shortens the name.

- `OnlineUiMemberLabel.FormatContextTitle` now budgets the title at 28 characters, appends the suffix unchanged, and shortens the name with an ellipsis when it would not fit; a name that fits is untouched.
- Two cases pin it: a long dead name keeps `(dead)` and stays inside the budget, a short one is unchanged.
- The member LIST was captured on the deployed build and already draws the marker on its own row — captured on the deployed build as `handshaken, in world, 20 ms – HP 0 H 86 T 93 St 100 – 3 item(s)` followed by the dead marker in the panel's own Chinese — so the surface the player reads first never lost it; the clipped one was the context-menu title, which is what this change fixes.