# Acceptance record — the archived run clock base for a mid-run joiner (batch 20261001-q)

- Ticket: `save-run-clock-not-sent` — verdict: **stays in `review/`** (row 1's re-verification is
  inconclusive and row 2's layer-change half was not exercised; both are named below)
- Batch: `20261001-q` (Run D solo; Run E host + guest) — siblings `save-mid-run-consistent-cut`,
  `save-solo-menu-exit-trigger`, `save-new-player-starting-supplies`
- Commit: `2efca14b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:31 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest joining a run in progress reads the run total | machine + named gap | **unproven** | the run's own start matched: host `runClock 543.755` vs guest `543.609` at the same entry (Δ0.146 s) — `e-host-clock-entry.json`, `e-guest-clock-entry.json`, and the guest's base is epoch-mapped (`savedRunTime 489.64` + `realTimeElapsed 53.97`). The mid-run re-entry could not be paired: at ~14:28:00 the host read `558.95` while the guest (still on its stale base) read `490.30`; the guest then applied `the live world took the run clock base 558.3s (written)` at 14:28:01 (`e-runfacts-lines.txt`), after which the host stopped answering evals (transport runaway) and the guest's later reads (`604.63` at ~14:28:45, `661.34` at ~14:29:45 — `e-guest-clock-converge.json`, `e-guest-clock-now2.json`) have no same-moment host counterpart. Whether the settled joiner value equals the host's total at that moment is therefore **not judged** |
| 2 | The clock keeps counting from the run's total across a layer change | machine | **unproven** (entry half observed) | the send-point read carried the host's **current** layer timer to the re-entering member: the guest applied `the layer timer 503.9s (written)` at 14:28:01 (`e-runfacts-lines.txt`) while the host's own live read one second earlier was `503.86` (`e-host-clock-after-rejoin.json`) — the d590d69b half, observed on a world entry. No `skiplayer` descent was executed in Run E, so the **layer-change** half itself was not measured. The host's cross-end layer-limit behaviour is unverified |
| 3 | A sender with no clock sends nothing and names it | machine | **pass** | this batch's suite (`suite.txt`; `RunClockFactsTests.NoCapturedClocks_SendsNothing` / `NoCommittedRun_SendsNothing`), and the live no-world path is visible in the host log (`the live run clock could not be read … no live world is present … nothing sent`) |

## Residuals for the user

- Row 1's end-screen text (the death-stats clock) was not read: the panel was never staged, and the
  host's failure ended the session first.

## Limits

- **The host's Steam transport runaway (k_EResultLimitExceeded flood, log growth ≈7 MB/s) ended Run E
  at ~14:30**; the host was stopped by the run to protect the machine and the guest was quit cleanly.
  The clock comparison was cut in the middle: the pre-apply gap (host `558.95` vs guest `490.30`) and
  the guest's apply line are real readings, but the post-apply pairing is missing. A repeat needs a
  session that stays responsive; the follow-up ticket carries the runaway.
- **The re-entry was into a restored world rather than a live follow**: the host's Continue opened the
  repository's `lastOpenedWorldId` world (the solo world) because a new run does not move the pointer,
  so the guest's re-entry is a world-entry-group send into a restored world. That is still the send
  path row 1 names (the entry group), but it is not a fresh mid-run follow.
- The observation window for "a repair re-send never jumps the clock" was not reached (the 60 s repair
  would have fired after the host was stopped).
