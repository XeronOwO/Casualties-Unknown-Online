# Acceptance record — Guest world-block mutations have no periodic re-report

- Ticket: `guest-block-mutation-re-report` — verdict: **back to `todo/`** (row 5 passes this run; rows 1–4, 6, 8–10 unproven; row 7 passed batch `20261001-y` and was not re-run)
- Batch: `20261002-k` — tickets `guest-block-mutation-re-report`, `runtime-entity-spawn-backfill`,
  `enemy-snapshot-binding-recovery`, `item-creation-registration-first`, `world-layer-generation-identity`,
  `generation-identity-remaining-families`, `world-determinism-world-fingerprint`
- Commit: `393d79a8` (artifact) · tree `b20984cb` (docs-only) · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+393d79a8c0d8f4a20320b667f6122884daa656ae`
- Run: 2026-10-02, 16:38–17:03 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present
- Artifacts: `k-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 5 | Third-party guest view — every peer converges to the same cell | machine | **pass** | **mine**: guest rolled the marked cell (512,1002) 8 → 0 (`k-B-guest-mine.json`) and all three clients then read it `block=0, damage=-1` (`k-B-read-mine-host.json` / `-guest.json` / `-alt.json`, all `cellX=512, cellY=1002`); host `[BlockBreak] presenting a relayed break at (512,1002)` + `Block placed at (512,1002) type 0 from <guest>` (`k-B-host.log`), guest `host answered (512,1002) — dropped the pending report (0 left)` + `a relayed break … found the cell already air` (`k-B-guest.log`), alt presented the same relay (`k-B-alt.log`); the 60 s snapshot later carried it (`Applied host block-state snapshot (1 blocks, …)`).. **place**: guest created and used one scrapmetal (`k-B-guest-provide.json`, item `10156476526`; `k-B-guest-place.json`) landing block `3` at (512,1003), and all three read `block=3` (`k-B-read-place-*.json`); host `Block placed at (512,1003) type 3 from <guest>`, guest `host answered (512,1003) — dropped the pending report (0 left)` + the host's echo, alt the relay (`k-B-*-place.log`) |
| 1–4, 10 | swallowed report / host-first-write after a swallow / partial-then-break with both reports dropped | machine | **unproven** | no packet-loss injection on this machine (scope §3); the run cannot drop a report or a relay, so the recovery shapes cannot be put into the world. |
| 6 | Guest reconnect — host table and guest world agree; mined blocks not resurrected | machine | **unproven** | the reconnect was staged as leave-world + Continue, but the run's Continue restored a **layer-end cut** (`Projected kernel run baseline (run 1, layer 1)`, `k-F-baseline-*.log`), so the layer-0 marked cells no longer exist; an in-place same-layer reconnect was not staged. |
| 8 | Table cap (`MaxDamagedBlocks` = 65 536) respected; overflow logged | machine | **unproven** | a 65 536-entry fill is unreachable in one session (named). |
| 9 | Solo → lobby → guest joins — the accumulated diff handed over exactly once | machine | **unproven** | the host accumulated a break while the guests were out (`k-F-host-accumulate.json`), but the guests' Continue restored the layer-end cut and the layer-0 diff did not carry over; the exactly-once handover shape was not staged. |
| 7 | Layer regeneration — the table resets with the new baseline | — | **not re-run** | row passed in batch `20261001-y`; the ticket's status still records it. |

## Limits

- Row 5 is the only reachable row of the ticket on this machine; the swallowed-report family needs a packet-loss
  or relay-drop capability the machine does not have.
- Rows 6 and 9 were blocked by the run's cut state (the layer-end Continue), not by a defect in the ticket's
  mechanism; the missing setup is named above.
- One session; each read was taken once per client (three independent clients).
