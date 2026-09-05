# Remote player medical/health panel

- Status: Todo (rejected in review)
- Priority: Medium
- Category: Remote player UI / native UI reuse
- Source: User report (2026-09-04); rejected by user (2026-09-05) because the implementation did not reuse the game's existing UI.

## Rejection

The previous implementation built a CUO-side read-only IMGUI medical panel
(`OnlineUiMedicalPanel`) instead of reusing the game's existing medical UI.
The user rejected this directly and compared it to the earlier backpack
mistake: when the game already has a native UI surface, CUO must reuse/focus
that surface, not create a parallel custom UI.

This ticket is re-opened as TODO. Any future implementation must follow the
native remote-backpack pattern (thin adapter focus + localizations + safety
guards), not replace the native UI with a custom CUO panel.

## Landed (rejected history)

The rejected implementation added:
- `RemoteVitalsService` full `RemoteMedicalSnapshot` / `RemoteLimbSnapshot` cache.
- `OnlineUiMedicalPanel` IMGUI panel with general/nutrition/circulation/trauma/status/limb sections.
- Players page / quick panel / right-click entry points.

Selfcheck: `docs/evidence/selfchecks/players/remote-medical-panel-selfcheck.md`.

## Goal (revised)

Allow a player to open another player's medical/health panel and view their
detailed body condition by reusing the game's own medical UI with a remote
focus, analogous to the native remote backpack.

## Investigation needed

1. Identify and reverse-engineer the game's native medical/health UI:
   - what component/window/state drives it;
   - whether it reads from a `Body` or other player object that can be pointed
     at a remote render clone;
   - what data it displays and whether that data is available from the remote
     clone/cache.
2. Determine the required adapter focus pattern:
   - can it be focused on a remote clone like `RemoteBackpackCoordinator` /
     `RemoteBackpackView`;
   - what local camera/body hijack or patch scope is required;
   - how to keep it display-only and prevent any proxy mutation.
3. Decide which existing CUO entry points should open it (Players page, quick
   panel, right-click menu).
4. Determine whether additional data must be retained on the side already
   syncing `CharacterHealthMsg` / limbs.

## Required design direction

- Reuse the game's native medical UI; do not build a replacement CUO IMGUI
  medical panel.
- If a native medical UI cannot be safely focused on a remote clone, stop and
  document the concrete blocker with evidence and get user direction before
  building any custom UI.
- Keep the display read-only: no remote clone mutation, no authority change.
- Reuse the already-synced 1 Hz `CharacterDataMsg` when possible; do not add
  wire traffic unless proven necessary.

## Acceptance criteria (for the future implementation)

- A player can open the game's own medical/health UI for a remote player and see
  the detailed body condition.
- The existing native UI is reused, not replaced by a parallel CUO panel.
- The UI is reachable from an existing player-facing entry point.
- The remote clone is never mutated; the view is display-only.
- No dead/duplicate presentation path remains.
- Existing tests and repo gates remain green.

## Non-goals

- Not re-accepting the previous CUO-side IMGUI panel as-is.
- Not adding a second medical presentation surface alongside the native UI.
