# Save archive surface: empty rows dropped, the retention ceiling stated

- Date: 2026-11-08
- Batch: `20261108-w` (host desktop only)
- Deployed artifact: `0.1.0+c84087ffa3c8624802721b2910b98ef1ad5bf094`, `tools/verify-deploy.ps1` — "Deployment matches this tree's build output"
- Ticket: `docs/backlog/done/save-backups-overwhelm-the-archive.md`

## What was claimed

The user reported "too many backups, badly organized, unpleasant to use". The code read shows the retention rule already
exists (`[Save] BackupRetentionCount`, default 10; `WorldRepository.PruneBackups` keeps the newest 10 per world — measured:
every world's `backups/` folder holds exactly 10 `.cuoz` files). What the window lacked: rows with nothing to show, and
any statement of the ceiling. This batch reads the change that drops the empty rows and prints the limit.

## Fixture

1. Deploy with the acceptance channel; `tools/verify-deploy.ps1` matched.
2. `tools/acceptance/launch-clients.ps1 -Only host` — host started 22:38:55, endpoint up 22:38:59, up 22:39:03.
3. Driver `open-window` → `ok: true`; `goto-page -Page worlds` → `ok: true, page: "Worlds"`.
4. `cuo-ui-dump` → 320 nodes under the window; `capture-frame name=worlds-page-20261108`.

## Readings

- The page header renders the localized title (translated: "Worlds") and, under it, the new line (translated from the frame): **"Every world keeps the newest
  10 backups; the older ones are pruned when a save is committed."** — the number 10 is the configured
  `BackupRetentionCount`, read through `IWorldLibrary.BackupRetention`.
- Every visible row carries content (translated from the frame): "layer 0, 3 players, N backup(s), last saved
  2026-11-08 21:25" with the "Use for Continue" / "Backup" buttons. No row without a snapshot and without
  backups is listed — the filter `!row.HasSnapshot && row.BackupCount == 0` removes them before they reach the list.
- Frame: `worlds-page-20261108.png` (full window) — the crop of the header and the first rows is in the batch record.

## Limits

- The `cuo-ui-dump` text field is empty for these rows (the window draws them with the game's pixel font, not a TMP
  component the dump reads), so the row text is read from the frame, not from the dump.
- The empty-row filter is read from the code path plus the absence of such rows in the frame; a before/after pair in one
  batch was not captured (the worlds on this machine all carry snapshots and backups).
- What remains for the ticket: editing the ceiling in the window (the config section `[Save] BackupRetentionCount`
  exists; the window only displays it) and paging for a long world list.
