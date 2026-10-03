# Acceptance record — Guest pending-report fallback: flat 60 s first resend

- Ticket: `guest-report-fallback-first-resend` — verdict: **stays in `review/`** (rows 1, 3, 4 and 6 pass;
  row 2 (runtime entity) and row 5 (clock wrap) stay `unproven`)
- Batch: `20261003-e` — tickets `recipe-unlock-fallback`, `trade-domain-dual-side-runtime`,
  `guest-report-fallback-first-resend`, `sync-cadence-review` (scope:
  `docs/evidence/acceptance/20261003-e-scope.md`)
- Commit: `eef70481` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+eef70481fef869766c530da6d234c9f96f637ff1` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, 09:22–09:36 +08:00 · Host: physical machine (Steam, app id 4576510) · Guest: Steam1
  sandbox; two clients, one world
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0)
- Artifacts: the `s5*`, `s6*`, `s7*` and `s13*` ids below, in the directory named by
  `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest breaks a block inside the swallow window — the host converges within the 5 s entry step | `machine` | **pass** | the guest entered the world at ~09:31:06 (fresh re-entry); the break at 09:31:27.593 (block 11 → 0) with the host's inbound blacked out 09:31:27.264–29.177 (`s5b/s5c-*`); the guest's pending count read block=1, drops=1 at +1.5 s (`s5c-pending-guest.json`); the dense re-report `[BlockSync] re-reported 1 unacknowledged block mutation(s) to the host.` at 09:31:32.636 — **5.04 s** after the break — and the host's `[BlockBreak] … (513,1003) accepted — 1 block drop(s) … registered + relayed.` at 09:31:32.682 (`s5d-guest-rereport.txt`, `s5d-host-accept.txt`); the host read the cell as 0 and the pending table drained (`s5d-*`) |
| 2 | Guest creates a runtime entity inside the swallow window — the same first-resend latency | `machine` | **unproven** | the batch's vocabulary can bind a mod content definition (`building-template-inject`) but has no drive that produces a genuine runtime entity creation; the creation half's 5 s step stays pinned by the ticket's tests |
| 3 | Report acknowledged before the window — no duplicate re-send | `machine` | **pass** | a live break at 09:31:55.715 (no blackout) was accepted by the host at 09:31:55.772 (**57 ms**, `s6b-host-accept.txt`); the guest's pending read 0 right after (`s6b-pending-guest.json`) and no block re-report line follows it in the run (the next one is row 4's steady re-report) |
| 4 | Steady state (long after entry) — one re-report a minute per outstanding set | `machine` | **pass** | the steady-phase break at 09:32:12.650 (the entry phase had closed at ~09:32:06; a fresh block placed at 09:32:05.795, `s7a-*`), pending block=1, drops=1 (`s7b-pending-guest.json`); the steady re-report at 09:33:12.710 — **60.06 s** — and the host's acceptance at 09:33:12.743 (`s7c-guest-rereport.txt`, `s7c-host-accept.txt`); pending drained (`s7c-guest-pending.json`) |
| 5 | Clock wrap — the window re-bases instead of stalling | `machine` | **unproven** | a `TickCount` wrap is not producible at runtime; the ticket's unit test pins the branch and the run names it as a limit |
| 6 | Bandwidth baseline — no regression beyond the recorded one | `machine` | **pass** (limit) | the run's own windows (`s13-host-traffic.txt`, `s13-guest-traffic.txt`: host send ≈149 kB/s, guest receive ≈149 kB/s over the session) show no anomaly; the entry phase healed on its **first** dense step (1 re-send, inside the declared ≤11 bound) and the steady re-report was one per minute; the 5-byte one-cell frame is the recorded simulation measurement (`docs/evidence/sync-cadence-measurements.md`), not re-measured live |

## Limits

- The entry-phase row is one sample per phase: the dense step was measured at 5.04 s (break → re-report),
  the steady step at 60.06 s, and the acknowledged control at 57 ms.
- The blackout stays a blunt inbound cut; every window stayed under 2 s and was lifted inside the 15 s
  watchdog, and `mode=status` read `armed:false` before each arming.
- A genuine runtime-entity creation and a clock wrap are outside this batch's capability; both are named
  `unproven` above rather than replaced by a weaker check.

## Residuals for the user

- None: the two unproven rows are capability limits named above, not subjective judgements.
