# Acceptance record — World/layer generation identity is missing from the wire

- Ticket: `world-layer-generation-identity` — verdict: **back to `todo/`** (row 3 passes; rows 1, 2, 4, 5, 6, 7 and 8 unproven)
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
| 3 | The same generation, first-writer-wins — unchanged | machine | **pass** | four same-generation writes by three different senders were judged and converged on all three clients: guest mine (512,1002) 8 → 0, guest place (512,1003) block `3`, host break (512,1003) 3 → 0, alt break of the guest's placed block (520,991) — each step's three reads agree (`k-B-guest-mine.json`, `k-B-read-mine-*.json`, `k-B-guest-place.json`, `k-B-read-place-*.json`, `k-B-host-write-2.json`, `k-B-read-fww*.txt`, `k-B-alt-break-fww.json`, `k-B-read-fww2-*.txt`); the host answered every report (`[BlockSync] host answered … dropped the pending report (0 left)` on the reporting client, `Block placed … type N from <reporter>` on the peers) and its arbitration window reads `76561198863287957's break at (520,991) accepted — … registered + relayed` and `remote damage broke the block … the block-state difference was recorded and the break relayed with its claim` (`k-B-host-arbitration.log`). Limit: no **conflicting** claim was produced — a second writer always observed the already-updated cell (the driver's inter-command gap, ~2.4 s, exceeds the relay latency), so the refusal branch was not restaged live; the row's "unchanged" half is what the run observed. |
| 1 | The air-write report is lost but the drops report arrives in the SAME generation — accepted; drops survive | machine | **unproven** | needs a dropped air write (no packet-loss injection on this machine, scope §3). |
| 2 | A previous layer's break report arrives after the layer change — refused as stale; the new layer's cell untouched | machine | **unproven** | the genuine in-flight case needs a report stamped before a descent and delivered after it; the run could not stage it deterministically (and its layer change came from a restored cut, not a live descent). |
| 4 | A stale `BlockDamageReport` row set for a regenerated cell — not applied | machine | **unproven** | no deterministic drive for a stale row set crossing a generation boundary. |
| 5 | Session end / new run — the identity resets with the run baseline; no cross-run attribution | machine | **unproven** | the run never ended a session or started a new run; the observed layer move kept `run 1` (`Projected kernel run baseline (run 1, layer 1)`, `k-F-world-host.log`), so the new-run reset was not staged. |
| 6 | Third-party view after a layer change — every peer refuses the other generation's relay, no re-attribution | machine | **unproven** | no stale relay was produced; what the post-layer-change windows show instead is a layer-modifier baseline divergence on both guests (`k-F-baseline-*.log`) — a different failure, filed separately. |
| 7 | The stamp is on the wire, not added by the caller | machine | **unproven** | requires decoding frames off the transport; not available on this machine. |
| 8 | No stamp / no baseline — never treated as current (pre-stamp behaviour) | machine | **unproven** | an unstamped report cannot be produced by the deployed peers. |

## Limits

- Row 3's positive evidence is sequential same-generation writes; its refusal branches (a recorded air write
  whose cell stands again, a different sender on an accepted cell) stayed unit-pinned and were not restaged.
- Rows 1, 2, 4, 7 and 8 are the batch scope's declared capability boundary (swallowed report, stale crossing,
  frame decode); row 5 additionally needed a session end or a new run.
