# Systemic save and backup management

- Status: Review — re-scoped 2026-09-19: every scope below is landed or closed, and the stage ticket carries the delivery (decision 198).
  The umbrella holds no implementation work of its own.
- Priority: Medium
- Category: Persistence / tooling
- Source: Promoted from `future/` by user request (2026-09-07, user-promoted) alongside the new save-system requirement
- Depends on: `review/save-system-mid-run-and-layer-end.md` — **reuse its package format** (manifest + per-domain JSON files + directory-entry archive). Do not define a second archive format.

Goal: provide a user-facing, inspectable backup/restore layer on top of CUO's
persistence without breaking the runtime/wire contracts.

## Current state (re-verified 2026-09-19)

The archive side is delivered; the management side is the stage ticket.

- Every committed cut writes a `.cuoz` archive (`docs/architecture/save-archive-format.md` §5/§7), so a
  backup package exists at every save — including the manual `/save` command. The earlier reading of
  scope 1 as "manual backup is missing" was wrong, and this ticket's 2026-09-07 text is what was stale.
- Interval autosave, retention, the config surface, the failure-degradation matrix and the
  pre-restore backup landed in S4.4 (`review/save-interval-autosave-and-backup-recovery.md`).
- The player-chosen restore and the world/backup picker are the one thing that was missing:
  `review/world-and-backup-management-surface.md`.
- `CharacterDataFileStore` / `CharacterDataFile` were RETIRED in S4.1 (decision 178), so the CUO world
  archive is the only persistent copy of a character and there is nothing else for a backup to carry.
- `ModStateFileStore` / `HostBanFileStore`: mod-state and host-ban persistence, unchanged by this
  ticket (mod state is host-persistent per decision 37 and is not part of the world package).

## Scope ownership

| # | Scope | State |
|---|---|---|
| 1 | Manual backup: a host command/UI that creates a backup package at any time | Landed — every committed cut archives, and `/save` (`WorldSaveCommands`) is the player's own cut. The stage ticket adds the picker, not a second write path. |
| 2 | Scheduled backup: configurable interval plus lifecycle hooks, with retention/pruning | Landed in S4.4: interval autosave, retention (default 10), the `[Save]` config surface, the layer-end/menu-return/interval/pre-restore triggers. |
| 3 | Restore/import: validated package import with checksum/version/build gating, host-only, no active live session, explicit degradation semantics | `review/world-and-backup-management-surface.md`. The archive is validated through the reader's §6 gate before anything is replaced; the load's own §6.1 version/build gating is unchanged. |
| 4 | Native game-layer backup: a GameAdapter-only slice for `SaveSystem` / `save.sv` | **Dropped** — contradicts decision 165 and §8 ("CUO never writes `save.sv` and never depends on it"); the native format cannot express a mid-run cut, so it holds nothing the CUO archive does not. Written 2026-09-07, before the 2026-09-10 freeze. |
| 5 | Retention/rotation and a "restore previous" surface | Retention landed in S4.4; the restore surface is the stage ticket, which keeps the replaced state as a pre-restore archive so "restore previous" is one more restore. |
| 6 | Migration/versioning between old protobuf saves and new packages | **Dropped** — old-format migration was excluded from v1 by the save system's frozen scope, and the legacy store was retired in S4.1 (decision 178). The module is unreleased, so there is no migration burden. |

## Stages

| Stage | Ticket | Scope | State |
|---|---|---|---|
| M1 | `review/world-and-backup-management-surface.md` | The world/backup picker in the Online UI, the player-chosen in-place restore, and the promotion trigger it needs | landed (review) |
