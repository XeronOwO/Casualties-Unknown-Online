# Save backups are too many and the archive is disorganized

- Status: Review — user-reported (2026-11-08): the save system keeps too many backups, they are not organized, and the
  interaction is unfriendly; the user asks for a proper design and UI. Corrected 2026-11-08: the retention rule already
  exists and holds (every world keeps exactly 10 backups; `[Save] BackupRetentionCount`); what the report hit is the
  surface — empty rows with nothing to show, a ceiling the window never states, and a list with no paging. The 2026-11-08
  change removes the empty rows and states the ceiling, and the live window reading shows it (batch `20261108-w`: the
  page prints "Every world keeps the newest 10 backups; the older ones are pruned when a save is committed." and lists
  only rows that carry content; `docs/evidence/acceptance/save-backups-overwhelm-the-archive-20261108.md`); editing the
  limit in the window and paging remain. The 2026-11-08 redesign makes the page a list of runs — newest first,
  five shown, older behind one button — with a run's save points folded under it, and the place named the way the
  player sees it ("surface" / "N levels down", translated from the catalogue) instead of the raw layer index (batch
  `20261108-x`; readings: `docs/evidence/acceptance/save-backups-runs-page-20261108-x.md`).
- Priority: High
- Category: Persistence / UX
- Source: the user, 2026-11-08 (translated: "the save system — too many backups, badly organized, unpleasant to use;
  design the UI properly")
- Related: `done/save-format-and-world-repository.md` (the world repository), `done/save-interval-autosave-and-backup-recovery.md`
  (the autosave and backup cadence), `done/save-mid-run-consistent-cut.md` (the cut kinds), `done/save-system-mid-run-and-layer-end.md`
  (the umbrella)

## The defect

Every cut writes a backup under the world's `backups/` folder. A world accumulates
`mid-run-<date>-<time>.cuoz`, `layer-end-…`, `quit-…` files; the retention rule keeps the newest 10 and prunes the
rest (read 2026-11-08, corrected below — this paragraph first said nothing prunes, and the measurement disproved it).

Measured on this machine (2026-11-08): every world's `backups/` folder holds exactly 10 `.cuoz` files. The manual names
(`w-<id>`, `mid-run-…`) are the format's, not the player's.

## The retention is not the gap (read 2026-11-08)

The chain, read from the code and then measured on the machine:

- `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/SaveOptions.cs` — `BackupRetentionCount`, default 10, clamped
  to 1..1000.
- `src/CasualtiesUnknownOnline.Plugin/PluginDependencyRegistrar.cs:160` binds it under the config section `[Save]`, key
  `BackupRetentionCount`.
- Every committed cut passes `Options.Retention` into `WorldRepository.PruneBackups(worldId, keep)`
  (`src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldRepository.cs:342-382`): it keeps the newest `keep`
  files, never deletes the newest, and reports failures instead of swallowing them.
- Measured: `saves/w-*/backups/` — every folder holds exactly 10 `.cuoz` files.

So the count the user saw is the ceiling doing its job. What the report actually hit:

1. Rows with no snapshot and no backups stayed in the Worlds list — they carry nothing and cannot be continued or
   backed up, so they read as noise.
2. The ceiling was invisible: the window never said that 10 is the limit or that older cuts are dropped. The player
   sees a count appear to grow the first ten times.
3. The list has no paging, so many worlds make a long scroll (the window itself redraws on every revision).

The 2026-11-08 change (this ticket's first slice) fixes 1 and 2:

- `IWorldLibrary` gained `BackupRetention`; `WorldLibraryService` answers it from `Options.Retention`.
- `OnlineUiWorldsDrawer.BuildWorlds` drops a row when `!row.HasSnapshot && row.BackupCount == 0`; nothing to show prints
  `worlds.empty`.
- `OnlineUiWorldsDrawer.BuildHeader` prints `worlds.retention` with the ceiling (both catalogues: "Keeps the newest {0}
  backup(s) per world; the oldest are pruned when a cut is committed.").

What remains for this ticket: an in-window editor for the ceiling (the config section exists; the window does not write
it yet), and paging for a long world list.

## What it affects

- The disk: unbounded growth (each cut is a full archive).
- The player's trust: a restore surface that lists raw filenames is not a product.
- The future: the save panel ("saves" tab in the window) has no backup list today.

## Design

The surface is the archive: `docs/design/cross-cutting/saves-and-recovery.md` (what a cut is, what the player may
recover). The design direction below is a proposal to confirm against that page before the first edit.

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

## Configuration (the user's addition, 2026-11-08)

The retention and the panel are settings, not constants: a `[Saves]` section in the CUO config
(`BepInEx/config/CasualtiesUnknownOnline.cfg`) with at least `MaxBackups` (the automatic ceiling per world), `KeepKinds`
(which kinds are never pruned — layer-end and quit), and `PruneOnCut` (on/off). Defaults must be a sensible product
choice (e.g. 10 backups per world, layer-end and quit always kept), the window's saves tab shows the current limits,
and the panel can change them without editing the file by hand. The retention rule and the config are one design: the
ceiling is the player's, the pruning is the product's.
