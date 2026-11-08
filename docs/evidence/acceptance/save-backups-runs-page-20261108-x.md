# The Worlds page reads as runs: newest first, save points folded, the place in the player's words

- Date: 2026-11-08
- Batch: `20261108-x` (host desktop only)
- Deployed artifact: `0.1.0+a0ae5b84`, `tools/verify-deploy.ps1` — "Deployment matches this tree's build output"
- Ticket: `docs/backlog/review/save-backups-overwhelm-the-archive.md`

## What the player saw before

Every world folder was one row in a long list, the layer printed as the raw index (translated: "layer 0"),
and a run's save points lived in a section after the whole list.

## Fixture

1. Deploy with the acceptance channel; the verifier matched.
2. `launch-clients.ps1 -Only host` — up 23:20:26.
3. Driver `open-window` → `ok`; `goto-page -Page worlds` → `ok, page: "Worlds"`.
4. `capture-frame name=worlds-runs-20261108`; then the driver's `click -Control worlds.backups.w-20261108-c6c0`
   (the newest run) → `ok`; `capture-frame name=worlds-fold-20261108`.

## Readings

- The list is one row per run, newest first: the rows read (translated from the frames) "surface · 3 player(s) ·
  N backup(s) · last saved 2026-11-08 21:25/20:58/20:21/19:21" — the place is named "surface" instead of the raw layer
  index, and no row without a snapshot and without backups appears.
- Each row carries `Use for Continue` and `Backups`; the newest row was expanded: its button flipped to `Hide` and,
  under the row, the page printed "Save points in this run:" with `autosave · 2026-11-08 21:25 · 0.0 MB` and a
  `Restore` button — the other runs stayed collapsed.
- Frames: `worlds-runs-20261108.png` (the folded list), `worlds-fold-20261108.png` (the expanded run).

## Limits

- The five-newest cap and the "show older" button were not exercised: the archive on this machine holds four runs with
  content, so the cap never bound.
- The row text is read from the frames; `cuo-ui-dump` carries no text field for these rows (the window draws them with
  the game's pixel font).
- What remains for the ticket: editing the backup ceiling in the window (it is displayed and config-driven; the window
  does not write it yet).
