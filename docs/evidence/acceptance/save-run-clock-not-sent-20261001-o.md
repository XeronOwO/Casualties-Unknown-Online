# Acceptance record — the run clock at a mid-run joiner

- Ticket: `save-run-clock-not-sent` — verdict: **moved back to `todo/`** — row 1 FAILS
  (`- Status: Todo — Rejected (row 1: a joining guest's live run clock is short by the interval since
  the host last published its base — 30.0 s and 85.6 s in this run — and the 60 s repair re-sends the
  same absolute value, so it never converges)`)
- Batch: `20261001-o` (the batch `20261001-m` plan's Run B) — siblings
  `save-multiplayer-restore-and-backups`, `save-guest-restore-claim-and-legacy-store-retirement`,
  `save-restore-account-surface`, `save-new-player-starting-supplies`, `restore-account-arm-release`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf`
- Run: 2026-10-01 11:42 → 12:01 · Host: physical machine (Steam) · Guests: the primary sandbox and the
  alternate sandbox (third client)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `capture`,
  `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest joining a run in progress reads the run total | machine + named gap | **FAIL** | after the joining client's run clock had settled, one same-moment read of the three clients gave host `582.49`, a member present at the restore `582.02` (Δ 0.46 s) and the joiner `552.44` (Δ **30.04 s**) (`o-host-runclock-triclient.json`, `o-guest-runclock-triclient.json`, `o-alt-runclock-triclient.json`); a bounded 58 s observation (10 reads) saw the Δ sit at 30.2 s without converging (`o-host-runclock-converge.json`, `o-alt-runclock-converge.json`); a later cold rejoin measured Δ **85.61 s** while the present member stayed at 1.90 s (`o-host-runclock-cold-reconnect-3.json`, `o-guest-runclock-cold-reconnect-3.json`, `o-alt-runclock-cold-reconnect-3.json`). Mechanism in this run's own lines: the joiner takes the host's **last published** base (`o-alt-log-excerpt.txt`: `the live world took the run clock base 348.8s (written)` at 11:52:01, `668.2s` at 11:56:58) while its own counter starts at its own world entry, and the 60 s repair re-sends the same absolute value — the host log shows the repair group being sent (`o-host-log-excerpt.txt`) and the joiner logs only `the layer timer … (kept — the layer has already spent at least as long)` with no new clock write (`o-alt-log-excerpt.txt`) — so the shortfall (the interval between the host's last publish and the joiner's entry) stays for the life of that world. The named gap: the end-screen text itself was not read |
| 2 | The clock keeps counting from the run's total across a layer change | machine | **pass** | before the layer change the three clients read `647.21` / `646.84` / `647.02` (`o-host-runclock-before-layer2.json`, `o-guest-runclock-before-layer2.json`, `o-alt-runclock-before-layer2.json`); after it they read `697.20` / `695.20` / `695.56` (Δ ≤ 2 s) with the boundary base `668.2s` written on both guests (`o-guest-log-excerpt.txt`: `the live world took the run clock base 668.2s (written)`), i.e. the clock continued from the run total instead of restarting at zero (`o-host-runclock-after-layer2-2.json`, `o-guest-runclock-after-layer2-2.json`, `o-alt-runclock-after-layer2-2.json`) |
| 3 | A sender with no clock sends nothing and names it | machine | **pass** | `RunClockFactsTests.NoCapturedClocks_SendsNothing`, `.NoCommittedRun_SendsNothing` in this batch's focused suite (`o-runclock-suite.txt`, 19 passed); the same run's `MemberEntersWorld_ReceivesTheRunClocks` and `InSessionRepair_AlsoCarriesTheRunClocks` pin the carrier row 1 exercises live |

## Residuals for the user

- None; both living rows are machine readings.

## Limits

- Row 1's shortfall is measured on `savedRunTime + realTimeElapsed`, the pair the game's own end screen
  derives from; the screen itself was not opened, which is the row's named gap.
- The exact intended reference instant is the one thing this record cannot settle from the tree alone:
  the carrier's own doc says the published value is "the host's total at the generation boundary", and
  the row says the joining guest reads "the run total". The run judged the row by the live comparison
  the batch plan names ("the guest's live clock is the archive's/host's value"), which the joiner misses
  by the interval between the host's last publish and its own entry.
- Row 2 holds at a boundary because the boundary is exactly when the host re-publishes; the two rows
  together show the publish point, not the value, is what a mid-world joiner lacks.
- One layer change and one world were observed; the run does not claim to have measured every ordering
  of join and boundary.
