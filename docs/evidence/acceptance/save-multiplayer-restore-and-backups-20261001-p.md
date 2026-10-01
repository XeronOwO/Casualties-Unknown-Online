# Acceptance record — S4 multiplayer restore and backups (rows 4–6 of the umbrella)

- Ticket: `save-multiplayer-restore-and-backups` — verdict: stays in `review/` (rows 4–6 pass here;
  row 3's fix is open in `done/save-new-player-starting-supplies.md`, and rows 1–2 and 7 were judged in
  batch `20261001-o`)
- Batch: `20261001-p` — tickets `save-interval-autosave-and-backup-recovery`,
  `world-and-backup-management-surface`, `save-multiplayer-restore-and-backups` (its rows 4–6)
- Commit: `f4ddfcf11499338b4125aa32846fe54172d3b3dc` · Deployed artifact:
  `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+f4ddfcf11499338b4125aa32846fe54172d3b3dc`
  (`tools/deploy.ps1` rebuilt the tree at run start, `tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-01 12:19 → 12:33 +08:00 · Host: physical machine (Steam) · Guest: Sandboxie Steam1
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (host + guest), `capture`,
  `input`, `logs`, `artifacts`
- Artifacts: the `batch-p/` files named below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | Interval autosave over several cycles: the expected number of backups, retention honoured, the newest never pruned | machine + live | pass | With the interval set to one minute through the config-entry path: cuts at 12:22:15 (manual), 12:25:07 (auto, immediately due), 12:26:07 (auto), 12:26:35 (manual), 12:27:35 (auto after the restart); every account after the third reads `3 archive(s) kept, 1 pruned` and the newest archive is present in each listing (`p-host-cuts-and-autosave.txt`, `p-host-cut-accounts.txt`) |
| 5 | Disk full / read-only save directory: loud failure, previous snapshot intact, session continues | machine + live | pass (limit) | Live read-only: deny-write on the world folder → `cut refused … StageFailed (… .staging is denied)`, `live/manifest.json` hash unchanged, no new archive, the client kept playing, and the next cut after the ACL was removed committed; the suite's `WorldSaveDegradationTests` injects the disk-full half. **Limit:** disk full itself was not produced live — see this batch's S4.4 record |
| 6 | Restore from a backup after a damaged live snapshot: live snapshot preserved as evidence, backup promoted, action reported | machine + live | pass | `live/run.json` corrupted → Continue: `the snapshot has no readable run baseline (run.json)`; the recovery took `mid-run-20261001-043204.cuoz` (it became `live/`), preserved the refused snapshot at `damaged-20261001-043214/`, reported `the pre-restore archive … could not be written … its preserved folder is the only copy; the refused live snapshot is preserved at damaged-20261001-043214; backup mid-run-20261001-043204.cuoz was promoted to the live snapshot`, and the world then loaded; `WorldSaveRecoveryTests` in the focused suite (`run-c-host-console-after-refusal-recovery.json`, `p-host-latest.log`) |

## Residuals for the user

None for rows 4–6.

## Limits

- The machine half of rows 4–6 was run in this batch's focused suite (`batch-p/p-focused-suite.txt`, 104/104).

- The disk-full half of row 5 rests on the suite's injected failure; the live refusals proved the same
  refusal step (`StageFailed`) and the same "previous snapshot stays live" outcome.
- The umbrella cannot move yet: row 3 (the starting-supplies race) is open in
  `done/save-new-player-starting-supplies.md` with its failing order named, and rows 1–2 and 7 carry
  batch `20261001-o` verdicts.
