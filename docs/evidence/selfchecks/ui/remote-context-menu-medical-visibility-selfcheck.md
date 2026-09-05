# Remote context menu Medical visibility — self-check

Owner cycle: `docs/backlog/todo/remote-context-menu-medical-visible-when-target-not-visible.md`
(now `review/`). The user rejected the previous behavior because the in-world
right-click menu showed only the Medical action for a remote player that was not
visible; every other remote-player action was gated by the existing
line-of-sight rule. This cycle aligns Medical with the same visibility gate on
every surface that renders it.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Single row projection | `OnlineUiMemberProjection.Build` computes `CanSee` from `localInWorld`, remote `InWorld`, and the deferred line-of-sight seam, then computes every interaction flag from it. |
| 2 | Medical eligibility | `CanViewMedical` previously required only non-local + in-world + cached vitals, deliberately ignoring line-of-sight. |
| 3 | UI consumers | `OnlineUiPlayerContextMenu`, `OnlineUiMemberListDrawer` (Players page and the docked quick panel both render through it) all consume `OnlineUiMemberRow.CanViewMedical`; there is no separate context-menu-specific medical rule. |
| 4 | Native medical open path | `RemoteMedicalCoordinator.Open` already refuses when the local player or the remote is not in-world; it does not need a new authority/wire rule. |

## 2. Whole-family audit

The same defect existed in every surface that draws a Medical button from the
member projection, not only the right-click menu:

| Surface | Before | After |
|---|---|---|
| Right-click in-world context menu | Medical shown while `CanSee == false`, so it appeared alone | Medical hidden until the target is visible |
| Online UI Players page | Medical button shown while `CanSee == false` | Medical button hidden while `CanSee == false` |
| Docked quick panel | Medical button shown while `CanSee == false` | Medical button hidden while `CanSee == false` |

Because the fix is in the shared projection, all three surfaces cannot drift
apart again. The medical-view action remains available whenever the target is
visible, in-world, and has cached vitals.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Medical follows line-of-sight | `CanViewMedical` now also requires `localInWorld` and `canSee` | `CanViewMedical_RequiresLineOfSight` asserts both `CanSee == false` and `CanViewMedical == false` |
| Medical requires local in-world | A player in the lobby/menu can no longer be offered the in-world medical action | `CanViewMedical_RequiresLocalInWorld` |
| Visible in-world remote still can be viewed | The normal positive path is unchanged | `InWorldRemoteWithVitals_CanViewMedical` |
| No lone Medical in the no-visibility suite | The existing no-line-of-sight all-actions-hidden test now asserts Medical is also hidden | `NoLineOfSight_HidesAllDirectInteractionActions` |
| Non-remote/non-in-world/no-data rows remain hidden | Existing edge coverage unchanged | `RemoteWithoutVitalsOrNotInWorld_CannotViewMedical` |
| Context-menu comment matches behavior | The stale “display-only so it ignores line-of-sight” rationale is replaced by the same-visibility-gate rule | `OnlineUiPlayerContextMenu.BuildActions` |

No protocol field, wire message, entity-event row, or authority rule changed.

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build --no-restore` | 2288 passed / 0 failed |
| Focused `OnlineUiMemberProjectionTests` | 30 passed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | passed |

## 5. Structure review

- The fix is a boolean-condition change in the existing projection; no new
  property, class, state field, or UI path was added.
- `OnlineUiMemberRow.CanViewMedical` documentation now states the visibility
  requirement, so the row remains a dumb, testable data projection.
- `OnlineUiPlayerContextMenu.BuildActions` did not need new routing logic:
  because all consumers use the single projected flag, the family is aligned at
  the root instead of patched separately in each UI surface.
- No class approached the 600-line gate.

## 6. Remaining manual acceptance

The L0 tests prove the projected eligibility matrix, and the deployed plugin
should be checked by the user in a real dual-client session: with a remote
player out of line of sight, the right-click menu (and the Players page/quick
panel) must no longer show Medical alone.
