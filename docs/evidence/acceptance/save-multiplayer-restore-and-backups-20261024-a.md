# Acceptance — S4 (multiplayer restore, validation/recovery, scheduled backups) closes

- Ticket: `docs/backlog/done/save-multiplayer-restore-and-backups.md`
- Artifact: `0.1.0+7b02e9d3`
- Batches: `20261001-o`, `20261001-p`, and `20261024-c` (the local batch that closed row 2)
- Date: 2026-10-24
- Result: **every stage done and every row has a passing judgement**; the umbrella is a roadmap with no
  implementation of its own, so it closes with its stages.

## Rows

| # | Row | Judged by |
|---|---|---|
| 1 | Host + 2 guests save; both guests rejoin | `save-multiplayer-restore-and-backups-20261001-o.md` (row 1 passed) |
| 2 | A Steam world opened over IP-direct | **2026-10-24**: `save-guest-restore-claim-and-legacy-store-retirement-20261024-a.md` — the Steam session claims nothing and the cross-space mismatch is named in the itemized account (`… the snapshot was written in the IpDirect key space and this session runs Steam, so none of its 1 stored character(s) was claimed (decision 162)`); the ambiguous same-name case is `save-restore-account-surface-20261024-a.md` |
| 3 | A guest who was not in the package joins | `save-new-player-starting-supplies.md` (S4.3, done — its acceptance record) |
| 4 | Interval autosave over several cycles | `save-multiplayer-restore-and-backups-20261001-p.md` |
| 5 | Disk full / read-only save directory | `save-multiplayer-restore-and-backups-20261001-p.md` |
| 6 | Restore from a backup after a damaged live snapshot | `save-multiplayer-restore-and-backups-20261001-p.md` + `save-interval-autosave-and-backup-recovery` (S4.4, done) |
| 7 | A guest disconnects and rejoins a restored world | `save-multiplayer-restore-and-backups-20261001-o.md` |

## Stages

| Stage | Ticket | State |
|---|---|---|
| S4.1 | `done/save-guest-restore-claim-and-legacy-store-retirement.md` | done 2026-10-24 (rows 1–3 passed) |
| S4.2 | `done/save-restore-account-surface.md` | done 2026-10-24 (row 2 passed) |
| S4.3 | `done/save-new-player-starting-supplies.md` | done |
| S4.4 | `done/save-interval-autosave-and-backup-recovery.md` | done |

## What changed in the ticket's own text

The "Verification limits" paragraph said the multi-client rows cannot be machine-verified and need the
user's dual-client pass. That is no longer true for the claim rows: the 2026-10-24 batch staged the
IP-direct roster (two same-named present players, a Steam session over an IP-direct snapshot) and the three
S4.1 rows passed on the machine. Rows 4–6 remain the backup/autosave family's own machine rows (S4.4).

## Limits

- Rows 4–6 are judged from the 2026-10-01 batch and S4.4's record, not re-run here.
- No OS-level mouse or keyboard input was used in the 2026-10-24 batch.
