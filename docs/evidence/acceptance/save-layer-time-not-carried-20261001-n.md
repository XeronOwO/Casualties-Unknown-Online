# Acceptance record — layer time accounting (resume ruling, after the seam fix)

- Ticket: `save-layer-time-not-carried` — verdict: **moved to `done/`**
- Batch: `20261001-n` (re-verification of the batch `20261001-m` Run A rejection)
- Commit: `9f79f25a` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+9f79f25a`
- Run: 2026-10-01 11:15 → 11:25 · Host: physical machine (Steam) · Guest: sandbox (Steam1)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The timer resumes at 6 of 10 minutes | machine | **pass** | `layer-time-set seconds=360` reported `before 192.5876 → after 360` (`row1b-layer-time-set-360.json`); the MenuReturn cut that followed carried `layer time 364.8` (decoder line in `host-log-excerpt.txt`); the world-entry write logged `the layer timer 364.8s (written) — the world finished generating` and the live value read `364.7701` after a pre-write sample of `2.229084` (`row1b-timer-after-continue-host.json`); the guest converged to the same value through the in-world message path (`showing 369.8669` at the read, guest log `the layer timer 364.8s (written)`, `row1b-timer-after-continue-guest-2.json`) |
| 2 | An archive without the row restarts the timer and says why | machine | **pass** | the live snapshot's optional `layerTimeSpent` property was dropped before the continue (backup `row2-live-run-before.json`; the injected edit is named as the setup and surfaced as `ChecksumMismatch in run.json` in `host-log-excerpt.txt`); the decoder reported `layer time absent`, the handover log said `holding the restored run clock base (871.6s) and the layer timer (<none>)`, no write line followed, and the continued layer restarted from zero (`0.230296 → 8.175593`, `row2-timer-after-continue-host.json`) |
| 3 | A layer advance keeps the game's accounting | machine | **pass** | after `game-console skiplayer` (`row3-skiplayer.json`), the next layer's timer restarted at `0.6484709` and accumulated normally to `36.56904` with the game's own limit (`maxTimePerLayer 3600`), `row3-timer-next-layer-host.json` |

## Residuals for the user

- None; all three rows are machine readings.

## Limits

- Row 2's archive shape was constructed by dropping the optional property from the live snapshot — the
  shape a pre-row archive has. The edit is visible to the archive's own manifest check
  (`ChecksumMismatch in run.json: the file is 22289 bytes but the manifest says 22326`) and is named
  here instead of left implicit; the restore continued and its account reported the one content loss.
- The first continue of the session restored `w-20261001-b951`, the repository's `lastOpenedWorldId`,
  rather than the freshly started run: a new run does not move that pointer. Its evidence is kept as a
  bonus observation (`row1-baseline-*.json`, `row1-timer-after-continue-host.json`, `row1-*` ids) and
  shows the same resumed write (`the layer timer 75.1s (written)` against a cut that carried `75.1`);
  the row-1 verdict uses the clean in-session cycle (`row1b-*`).
- The run observed each row once (row 1 twice, counting the bonus cycle); it does not attempt to prove
  the absence of a race.
