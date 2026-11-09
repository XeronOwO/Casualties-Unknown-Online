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
  `20261108-x`; readings: `docs/evidence/acceptance/save-backups-runs-page-20261108-x.md`). The 2026-11-09 change adds the
  DELETE surface (one save point, or the whole run, both two-step and both refusals visible) and the block indent as a
  ROW fact (`OnlineUiRowModel.Indent`, a layout spacer in the adapter, never spaces inside a label). The list's frame and
  the in-window editor for the ceiling remain.
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

## Design pass (2026-11-08, the user's second look)

The user read the first slice and named two visual problems: the list has no container, so a long page reads as
undifferentiated text, and the expanded save points start at the same left edge as the run names, so the two levels look
alike. The design for the next slice:

- **The list is a box.** The runs sit inside the window's own frame style: a one-pixel border with the page's dark fill,
  the header line inside the box's top edge, and the "show older" control on the box's bottom edge. The box is the
  scroll viewport's own child, so it grows with the list and the window scrolls it, never the window.
- **Two levels, two margins.** A run row sits at the box's left margin with its own name; the expanded save points sit one
  step in (a leading indent the width of a character cell), under a thin vertical rule that runs from the run's name
  down the length of its save points. The save-point rows carry the rule on their left, the run rows do not, so the eye
  reads "run → its saves" without labels.
- **The control is a noun.** The button says what it reveals and how many there are ("Save points (3)") — never a bare
  verb that could read as "make a backup" (2026-11-08: the user read "Backups" as an action).
- The counts in the detail line stay: "surface · 3 player(s) · 3 save point(s) · last saved …" — the noun in both places
  comes from the same key, so they cannot drift apart.

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

## The delete surface (2026-11-09)

The player's complaint was the missing half: "the page only ever grows". Two verbs now exist, each behind a second click
that names what goes, and each with its refusals visible on the status line:

- **Delete this save point** — `IWorldLibrary.TryDeleteBackup` → `WorldRepository.DeleteBackup(worldId, fileName)`. The
  file is resolved by NAME among the world's own listed archives (a caller string is never turned into a path); the last
  save point and the newest one are refused, because the newest is the restore's own fallback.
- **Delete this run** — `IWorldLibrary.TryDeleteWorld` → `WorldRepository.DeleteWorld(worldId)`. The path comes from the
  validated world id under the saves root; a world that is loaded or being generated is refused (its files are in use);
  the folder and every archive in it go.
- The confirmation lines name the run or the save point (kind · time · size), never a file name (a `const float
  BlockIndent = 26f` in the drawer and `OnlineUiRowModel.Indent` in the model).
- The block indent is the model's: a row carries `Indent`, the adapter puts one `LayoutElement` spacer at the head of
  its first line. A label cannot do it — a label takes the row's remaining width and pushes its neighbours to the right
  edge (measured twice, 2026-11-08).

Focused tests: `tests/CasualtiesUnknownOnline.Tests/Persistence/SaveArchiveDeleteTests.cs` (5 facts: an unknown name, the
last save point, the one-file delete, the whole-run delete, an invalid id).
