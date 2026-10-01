# Acceptance record — S4 multiplayer restore (host + two guests)

- Ticket: `save-multiplayer-restore-and-backups` — verdict: **stays in `review/`** (row 2 unproven:
  no IP-direct session was staged; rows 4–6 are the batch plan's Run C and were not executed here)
- Batch: `20261001-o` (the batch `20261001-m` plan's Run B) — siblings
  `save-guest-restore-claim-and-legacy-store-retirement`, `save-restore-account-surface`,
  `save-new-player-starting-supplies`, `save-run-clock-not-sent`, `restore-account-arm-release`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf`
- Run: 2026-10-01 11:42 → 12:01 · Host: physical machine (Steam) · Guests: the primary sandbox and the
  alternate sandbox (third client)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `capture`,
  `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host + 2 guests save; both guests rejoin, each claims their own character; two cycles stay stable | machine + residual | **pass** | all three clients carried a distinctive marker item before the cut (`o-host-item-provide.json` `scrapmetal#2379732862915`, `o-guest-item-provide.json` `climbingrope#14451443822`, `o-alt-item-provide.json` `waterbottle#13787924117`); the mid-run cut was taken with all three present (`o-host-save-cut1.json`: `cut written (world w-20261001-3315 at revision 1445 … backup mid-run-20261001-034739.cuoz …)`); after the restore every client's local tree held its own marker with the **same instance id** (`o-host-local-tree-after-restore1.json`, `o-guest-local-tree-after-restore1.json`, `o-alt-local-tree-after-restore1.json`), and the binder logged `Restored characters: 3 bound to present peers, 0 left unclaimed, 0 refused (key space Steam)` (`o-host-log-excerpt.txt`); a second cut/restore cycle repeated it — host `scrapmetal#2379732862915`, guest `climbingrope#14451443822`, the third client's new character `rosepod#26672826005` (`o-*-local-tree-after-restore3.json`, binder line at 11:55:25 in `o-host-log-excerpt.txt`) — and the account said `CUO restored world w-20261001-3315: nothing was lost.` both times (`o-host-console-after-restore1.json`, `o-host-console-after-restore3.json`). Residual: the on-screen restore notification |
| 2 | A Steam world opened over IP-direct | machine | **unproven** | not staged: the run kept the Steam transport throughout (every `state` probe reads `transport: Steam`); the Online UI does expose the Steam / IP-direct switch, but no drive path for it was exercised in this batch |
| 3 | A guest who was not in the package joins | machine | **pass** | before the second restore the third client left the session (`o-alt-home-leave.json`, `o-alt-state-after-leave.json`: `role None`); the binder logged `Character steam-… has no claimant in this session; decision 162: that player joins as a new character` and `2 bound to present peers, 1 left unclaimed, 0 refused` (`o-host-log-excerpt.txt`); on rejoining the restored world the client read `CUO new player: starting supplies (light) given — emergencylight.` (`o-alt-console-after-rejoin-2.json`) and its tree held one freshly created `emergencylight#22377858709` (`o-alt-local-tree-after-rejoin-2.json`) |
| 4–6 | interval autosave / read-only save directory / backup promotion | — | not this run | the batch plan assigns these rows to Run C (the backup/recovery world); they were not executed here |
| 7 | A guest disconnects and rejoins a restored world, with no legacy `character-data.bin` on disk | machine | **pass** | the third client was quit and cold-relaunched twice (`o-alt-quit.json`, `o-alt-ping-cold.json`, `o-alt-quit-2.json`); after both rejoins its tree held the character the restore had bound, unchanged (`rosepod#26672826005` + `emergencylight#22377858709`; `o-alt-local-tree-cold-reconnect-2.json`, `o-alt-local-tree-clean-reconnect.json`), and its log shows `Received character restore (3 items)` (`o-alt-log-excerpt.txt`); a pre-retirement `CasualtiesUnknownOnline.character-data.bin` (last written 2026-09-06, before the store was retired) was found in the install's config directory and renamed aside as the declared setup before the clean cycle, and no new file of that name appeared (`o-world-archive-listing.txt`, `run-b-log.txt`) |

## Residuals for the user

- Row 1: whether the restore notification reads clearly on screen (the state, not the text, is the
  machine half; no frame of it was captured this run).

## Limits

- The run staged one world (`w-20261001-3315`, mid-run layer 1) and executed three restores: a clean
  in-session cycle with all three present, a backup-fallback cycle with the third client absent, and a
  second clean in-session cycle with all three present. Two cycles are the repetition this row asks for.
- The Continue entry was pointed at the run's own world through the Worlds page's own select control
  (`o-host-worlds-page.json`, `o-host-worlds-select-3315.json`); the repository's pointer named an
  earlier world before that click.
- Every restore of this run reported a geyser-fact shortfall in the live world
  (`the live world did not take 15 geyser type(s)`, then `7`); the account names it rather than
  dropping it silently. It is the same family batch `20261001-m`/`n` observed and is not judged by this
  ticket.
- A row the run could not stage is `unproven`; it is named above, not guessed.
