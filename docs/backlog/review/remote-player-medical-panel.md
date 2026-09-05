# Remote player medical/health panel

- Status: Review
- Priority: Medium
- Category: Remote player UI / native UI reuse
- Source: User report (2026-09-04); rejected in an earlier review (2026-09-05) because the previous delivery used a CUO-side IMGUI panel instead of the game's native medical UI.

## Goal

Allow a player to open another player's medical/health panel and view their
detailed body condition by reusing the game's own medical UI (WoundView) with a
remote focus, analogous to the native remote backpack.

## Implemented

This cycle replaces the rejected CUO IMGUI medical panel with the native
`WoundView` medical UI:

- `IGameAdapter.OpenRemoteMedical(steamId, displayName)` opens the game's own
  WoundView medical panel for an in-world remote player.
- `RemoteMedicalCoordinator` creates a display-only body copy from the
  `"Experiment"` template, projects the remote player's latest 1 Hz
  `CharacterDataMsg` (health, limbs, skills) onto it, and opens the native
  WoundView with that body as the read source.
- `RemoteMedicalView` holds the session-scoped remote focus; the live remote
  render clone is never mutated by this path.
- `RemoteMedicalPatches` keep the native view read-only while it is focused on
  a remote player: nap, radial use/wear, and special limb actions are blocked;
  closing the native panel tears down the display body.
- `RemoteMedicalCoordinator.Update` closes the focus on session/world/remote
  exit and refreshes the display body from snapshot updates.
- The CUO-side custom `OnlineUiMedicalPanel` and all of its presentation/block
  wiring were removed. The Online UI Players page and right-click menu still
  expose the "Medical" action, now routed to the native WoundView.
- No protocol change and no authority change: the view is display-only.

Entry points: Online UI Players page "Medical" button and in-world right-click
player context menu "Medical" action.

Selfcheck: `docs/evidence/selfchecks/players/remote-native-medical-view-selfcheck.md`.

## Acceptance criteria

- A player can open the game's own medical/health UI for a remote player and see
  the detailed body condition. ✅ native `WoundView` with projected snapshot.
- The existing native UI is reused, not replaced by a parallel CUO panel. ✅
  custom IMGUI panel deleted.
- The UI is reachable from an existing player-facing entry point. ✅
  Players page + right-click context menu.
- The remote clone is never mutated; the view is display-only. ✅ uses a
  separate inactive display body; no authority/protocol change.
- No dead/duplicate presentation path remains. ✅ custom panel file removed.
- Existing tests and repo gates remain green. ✅ 2278 tests; build/format/gates pass.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx --no-restore` — 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` — 2278 passed / 0 failed.
- `dotnet format CasualtiesUnknownOnline.slnx --no-restore` run.
- `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1`, `check-delivery.ps1` all pass.
- Protocol unchanged.

## Non-goals

- Not re-introducing any CUO-side IMGUI medical panel.
- Not adding remote medical editing/healing through the native panel; this
  delivery is display-only.
