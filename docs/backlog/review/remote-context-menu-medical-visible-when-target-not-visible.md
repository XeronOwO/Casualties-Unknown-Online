# Remote context menu Medical visible when target is not visible

- Status: Review
- Priority: Medium
- Category: Remote player context menu / interaction visibility
- Source: User report (2026-09-05) — when the host is not visible from the guest's view, right-clicking still shows the Medical button, while other options are not shown.
- Landed: 2026-09-05 — Medical now uses the same shared line-of-sight/visibility gate as every other remote-player action; the fix is in the row projection so the right-click menu, Players page, and quick panel cannot drift apart.

## Problem

From the guest's perspective, when the host cannot be seen (not visible / not
on screen / otherwise not a valid interaction target), the right-click remote
player context menu still exposes the Medical option. Other context-menu
options are not visible in the same situation.

Expected behavior: the set of right-click options should be consistent with
whether the target is actually visible/available. If the target is not visible,
remote player actions should not be offered (or at minimum Medical must not be
shown alone).

## Goal

- Audit the remote-player context-menu visibility logic and the Medical action's
  gating.
- Make Medical (and the rest of the remote options) respect the same
  visibility/availability conditions as the other context-menu actions.
- Cover roles/directions: guest -> host, host -> guest, and third-party views.
- Add regression/runtime evidence for the exact user reproduction before moving
  to `review/`.

## Landed

- The root cause was in the shared member projection: `CanViewMedical` did not
  require `CanSee`, while every other remote action did. The context menu then
  intentionally kept Medical when returning early on `!CanSee`, leaving it as
  the only button.
- `OnlineUiMemberProjection.Build` now requires non-local + in-world +
  `localInWorld` + `canSee` + cached vitals before exposing the Medical action.
  All consumers (`OnlineUiPlayerContextMenu`, `OnlineUiMemberListDrawer`, and
  the docked quick panel) inherit the same gate from that one row flag.
- No wire/protocol/authority change.
- Evidence: `docs/evidence/selfchecks/ui/remote-context-menu-medical-visibility-selfcheck.md`

## Acceptance criteria

- When the remote player is not visible, right-click does not show Medical (or
  shows no inconsistent lone action).
- When the remote player is visible and valid, the expected context actions
  appear together; no option is missing due to the same gating bug.
- Existing remote medical/backpack/interaction tests and repo gates stay green.
