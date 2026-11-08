# Save backups are too many and the archive is disorganized

- Status: Review — user-reported (2026-11-08): the save system keeps too many backups, they are not organized, and the
  interaction is unfriendly; the user asks for a proper design and UI.
- Priority: High
- Category: Persistence / UX
- Source: the user, 2026-11-08 (translated: "the save system — too many backups, badly organized, unpleasant to use;
  design the UI properly")
- Related: `done/save-format-and-world-repository.md` (the world repository), `done/save-interval-autosave-and-backup-recovery.md`
  (the autosave and backup cadence), `done/save-mid-run-consistent-cut.md` (the cut kinds), `done/save-system-mid-run-and-layer-end.md`
  (the umbrella)

## The defect

Every cut writes a backup under the world's `backups/` folder, and nothing prunes or groups them. A world accumulates
`mid-run-<date>-<time>.cuoz`, `layer-end-…`, `quit-…` files with no retention rule, no naming a player can read, and no
surface to inspect, restore or delete one. The player sees a flat folder and a growing count.

Measured on this machine (2026-11-08): `saves/w-<id>/backups/` holds dozens of `.cuoz` files across the sessions of one
day; the manual names (`w-<id>`, `mid-run-…`) are the format's, not the player's.

## What it affects

- The disk: unbounded growth (each cut is a full archive).
- The player's trust: a restore surface that lists raw filenames is not a product.
- The future: the save panel ("saves" tab in the window) has no backup list today.

## Design direction (to be confirmed before the first edit)

1. **Retention policy**: keep the last N cuts per world (N configurable, default e.g. 10), always keep the newest
   layer-end and the newest quit cut, prune the rest oldest-first at the cut's own end (never mid-write). A policy the
   host applies — backups are local to the host.
2. **A readable structure**: `saves/<world>/backups/<kind>/<yyyy-MM-dd HH-mm-ss>.cuoz` or one flat folder with a
   manifest, so the list can be grouped by kind and time without parsing names.
3. **A restore surface in the window**: the saves tab lists worlds, then each world's backups (kind, time, size, reason),
   with Restore and Delete (confirm), and a "open folder" escape hatch. Restore uses the same continue path the
   product already has; Delete prunes one file with a confirmation naming what it is.
4. **The native `save.sv` stays untouched** (the existing rule); only CUO's own archive is managed.

## Acceptance

- After N+3 cuts on one world, the backups count is bounded and the oldest are gone; the newest layer-end and quit
  cuts remain.
- The window lists the backups by kind and time; Restore on one brings that world back with the same rows the file
  carries; Delete removes exactly one file after the confirmation.
- The native save file's mtime never changes.

## Also reported (2026-11-08, the same session)

- The list carries empty worlds: layer 0, 0 players, 0 backups — records that hold nothing to continue and nothing to
  restore, yet still take a row.
- Each row offers a backup button only: there is no "set as the continue target" action, so the world the player wants
  next cannot be chosen from the surface.
- The empty rows and the missing target action belong to the same design: the list is the player's view of the archive,
  so it shows only worlds with something in them and gives each the two actions the product actually has (continue
  here / back it up).

## Facts to read before designing (the empty rows)

- Where the world list is built (the window's saves tab and the repository's index): whether an empty directory is
  admitted because a backup was attempted there.
- What the Continue button reads: `lastOpenedWorldId` is the only target today (the reopen path in
  `docs/decisions/active.md`); a "set as continue target" action writes that field.

- The count itself is a defect: with enough backups the window lags while it lists them, so the surface must page or
  virtualize (render a window of rows, not the whole list), load a row's details lazily, and never build a full DOM of
  hundreds of rows per frame. The retention rule above is the first fix; the list's rendering is the second.
