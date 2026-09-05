# Remote medical treatment operations

- Status: Todo
- Priority: Medium
- Category: Remote player UI / cross-player medical actions
- Source: User request (2026-09-05) — the remote medical panel is view-only and cannot perform medical treatment operations, e.g. using a syringe on a remote player.

## Problem

The current remote medical/health panel (`review/remote-player-medical-panel.md`)
reuses the native WoundView but is explicitly display-only. A player can see a
remote player's body/limb condition, but cannot perform treatment actions that
the native medical UI would allow on a local player — e.g. using a syringe
(administer medicine/injection) or other medical operations from the panel.

## Goal

Allow the native remote medical panel to perform real medical treatment actions
against a remote player, while preserving the safe cross-player action
semantics already established by the project:

- Reuse the game's native medical UI/operations where one exists.
- Route each treatment operation through the existing host-validated /
  cross-player interaction/arbitration path, not by mutating the remote clone
  or the display-only body copy directly.
- Define the exact supported operation set (at minimum: syringe/injection;
  identify other native WoundView medical actions and either support or
  explicitly defer them).
- Cover all roles/directions: local acting on remote, remote acting on local,
  and third-party views.
- Keep the remote clone non-authoritative; the target player's own client
  validates/applies the committed operation.

## Acceptance criteria

- A player can open the remote medical panel and use the native medical
  treatment UI (e.g. syringe) to perform a treatment on the remote player.
- The treatment is applied through the existing cross-player interaction /
  host arbitration path, with a verified commit and replay on the target side.
- The display-only WoundView path is not bypassed by direct writes to remote
  clones.
- Unsupported native medical operations are either supported or recorded
  explicitly with user acceptance (no silent "future" scoping).
- Full build, tests, architecture/event/entity gates pass; deployed artifact
  verified before moving to `review/`.

## Non-goals

- Not re-introducing a parallel CUO medical panel.
- Not mutating the remote render clone as the operation path.
- Not expanding beyond the native medical UI's own operations without user direction.
