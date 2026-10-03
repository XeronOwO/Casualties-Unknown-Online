# Acceptance record — Sync cadence review: fallback stretch limits and first-resend latency

- Ticket: `sync-cadence-review` — verdict: **stays in `review/`** (rows 1, 5 and 6 pass with limits; rows
  2–4 are handed to batch `20261003-l` and stay `unproven` here)
- Batch: `20261003-e` — tickets `recipe-unlock-fallback`, `trade-domain-dual-side-runtime`,
  `guest-report-fallback-first-resend`, `sync-cadence-review` (scope:
  `docs/evidence/acceptance/20261003-e-scope.md`)
- Commit: `eef70481` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+eef70481fef869766c530da6d234c9f96f637ff1` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, 09:22–09:36 +08:00 · Host: physical machine (Steam, app id 4576510) · Guest: Steam1
  sandbox; two clients, one world
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: the `s9*`, `s11*`, `s13*` ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Pressure-simulated peer: the governor still only stretches cadence, never drops a fallback | `machine` | **pass** (limit) | every fallback kept running through the session: the trader stream stretched from its 5.016 s base (09:29:05.958 → 09:29:10.974) to 8.6–10.0 s (09:34:28.212 → 09:34:36.775 → 09:34:46.780, `s11-guest-state.txt`) while every window was delivered and applied; the recipe-set repair ran at 60–63 s (`s1f/s2d-host-set-sends.txt`); the entry-repair group answered the member's repeat (`s9d-host-repair.txt`). The limit: no max-pressure injection was produced in this batch — rows 2–4 carry that harness |
| 2 | Item keyframe cap under max pressure: measured, cap decided | `machine` | **unproven — handed to batch `20261003-l`** | the max-pressure harness is not part of this batch; the record names the hand-off and the ticket stays in `review/` |
| 3 | Trader fallback cap under max pressure: measured, cap decided | `machine` | **unproven — handed to batch `20261003-l`** | same |
| 4 | Fluid full viewport under max pressure: measured, accepted | `machine` | **unproven — handed to batch `20261003-l`** | same |
| 5 | Host mutation during the P2P swallow window — the member converges on the entry repair | `machine` | **pass** | the host placed block 11 at (509,1003) at 09:33:27.279 while the guest's inbound was blacked out 09:33:27.236–28.824 (the guest read 0, the host 11 — `s9a/s9b/s9c-*`); the guest's own readiness-window repeat (`scene-repeat`) at 09:33:34.341 drove the host's `Sending the in-session repair group to …` at 09:33:34.645 (**304 ms** later, `s9d-host-repair.txt`) and the guest read the cell as 11 by 09:33:36.8 (`s9d-read-guest.json`) |
| 6 | Bandwidth baseline: no regression beyond the recorded one | `machine` | **pass** (limit) | the run's own windows (`s13-host-traffic.txt`, `s13-guest-traffic.txt`) show the ordinary session profile (host send ≈149 kB/s, guest receive ≈149 kB/s) with the repair and set sends riding it; the repair pass itself is test-pinned at 2438 B and ≤6 passes per open entry window (`docs/evidence/sync-cadence-measurements.md`), not re-measured live |

## Limits

- Rows 1 and 6 are judged from the run's ordinary-load behaviour, not from a max-pressure injection; the
  pressure-only measurements of rows 2–4 are handed to batch `20261003-l` and the ticket stays open.
- One session is one sample of the cadences; the repair answer was measured at 304 ms and the entry-repair
  cycle at ~60 s.
- No third peer was present; third-party isolation is a unit-pinned property here, not a live row.

## Residuals for the user

- None: the unproven rows are a harness hand-off named above, not subjective judgements.
