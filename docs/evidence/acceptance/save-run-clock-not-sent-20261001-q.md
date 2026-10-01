# Acceptance record — the archived run clock base for a mid-run joiner (batch 20261001-q)

- Ticket: `save-run-clock-not-sent` — verdict: **moved to `done/`** (row 1's re-verification passes; the
  end-screen text stays a residual for the user)
- Batch: `20261001-q` (Run D solo; Run E and Run E2 host + guest) — siblings
  `save-mid-run-consistent-cut`, `save-solo-menu-exit-trigger`, `save-new-player-starting-supplies`
- Commit: `2efca14b` (runs) · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+2efca14b871112f814b64c8338b69ea47e5f2d44`
- Run: 2026-10-01 14:13 → 14:42 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest joining a run in progress reads the run total | machine + residual | **pass** | the guest left the lobby and rejoined the host's in-progress run (host total ≈ 76 s at its entry). Before the entry group's `RunFacts` arrived the joiner sat on its own counter: host `76.11` vs guest `10.64` at 14:39:00 (`f2-host-clock-midjoin.json`, `f2-guest-clock-midjoin.json`). The entry-group send then mapped the host's live total onto the receiver's epoch — `[RunFacts] the live world took the run clock base 66.0s (written)` at 14:39:01.982 (`f2-runfacts-midjoin.txt`) — and the settled pair read host `94.02` vs guest `97.46` (14:39:18), `117.02` vs `120.38` (14:39:41) and `130.74` vs `134.11` (14:39:54): a **stable +3.4 s** offset, not a growing gap. A later entry (the layer change) settled to **Δ0.067 s** (host `215.1655` vs guest `215.2321`, `f2-host-clock-layer-settled.json`, `f2-guest-clock-layer-settled.json`). Residual: the end-screen death-stats text was not read (see Limits) |
| 2 | The clock keeps counting from the run's total across a layer change | machine | **pass** | the host's `skiplayer` moved it to layer 1; after the member followed, the same-moment pair read host `215.1655` vs guest `215.2321` (Δ0.067 s) with both on layer 1 (`f2-host-clock-layer-settled.json`, `f2-guest-clock-layer-settled.json`), and the **layer timer** paired `5.194` (host) vs `5.103` (guest) — the joiner holds the host's **current** layer's timer, not the replaced layer's value (the old layer read `137.0 s` before the jump). The apply line names it: `[RunFacts] the layer timer 0.2s (kept — the layer has already spent at least as long)` (`f2-layer-settled-lines.txt`) |
| 3 | A sender with no clock sends nothing and names it | machine | **pass** | this batch's suite (`suite.txt`; `RunClockFactsTests.NoCapturedClocks_SendsNothing` / `NoCommittedRun_SendsNothing`), and the live no-world path is visible in the host log (`the live run clock could not be read … no live world is present … nothing sent`) |

## Residuals for the user

- Row 1: the end screen's death-stats clock text. The panel was never staged (the run's deaths were
  debug-console kills on a live host, not an end-screen view), so the rendered value is for a person to
  look at; the live value it renders was measured here.

## Limits

- **The `+3.4 s` entry settle.** The receiver's mapping is `sentTotal − its own elapsed`; the observed
  entry-group apply left the joiner 3.4 s ahead of the host until the next apply rewrote the base (the
  layer-change apply then settled to 0.067 s). The samples show the offset is constant, not growing.
- **No repair-group apply was observed** in the ~55 s window sampled after the mid-run join (the next
  apply seen was the restore cycle's), so "a repair re-send never jumps the clock" rests on this batch's
  suite (`RunClockSendPointFreshnessTests.InSessionRepair_ReReadsTheLiveClock_TheSameWay`) rather than
  on a live repair tick.
- Run E (the first host + guest attempt) was aborted by the host's Steam transport send-limit runaway
  before its clock pair could be completed; Run E2 (a light world, 80 animals) ran the same path cleanly.
  See the batch scope page and the runaway's own ticket.
