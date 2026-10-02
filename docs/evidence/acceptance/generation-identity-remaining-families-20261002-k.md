# Acceptance record — The remaining generation-relative report families

- Ticket: `generation-identity-remaining-families` — verdict: **back to `todo/`** (rows 3 and 8 pass; rows 1, 2, 4, 5, 6, 7 and 9 unproven)
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
| 3 | Same generation — the entry materializes exactly as today | machine | **pass** | the same-generation runtime creations materialized exactly once on every peer and stayed idempotent: keypad `/ geyser / crystalenemy` spawned by the host produced `applied carried keypad code` / `applied carried liquid type 2` / `applied carried crystal tint` on each peer (`k-C-payload-*.log`), one copy each in the payload read (`k-payload-C-*.json`), and the two same-cell shadecrawlers carried distinct creation keys (`k-creation-census2-*.json`); the 60 s snapshot re-applied the table without adding a copy (`applied host runtime-entity snapshot (2 entries, 3 animal acknowledgements)` ×2 — `k-C-payload-guest.log`). |
| 8 | The entry group's ordering — the run baseline precedes every stamped absolute table | machine | **pass** | at world entry the guest's log order is `Projected kernel run baseline (run 1, layer 0)` 16:40:07.264 → `Restored kernel checkpoint at revision 93` 16:40:07.274 → `[TrapLayout] aligning` 16:40:07.366 → the entry tables (`applied 32 host keypad code(s)`, `applied 99 host liquid type(s)`) (`k-A-ordering-guest.log`, `k-A-entry-guest.log`, `k-A-entry-alt.log`). Limit: one entry sample, read from log order — no frame decode. |
| 1 | A trap-layout repair crosses the guest's layer change — refused stale, nothing materialized | machine | **unproven** | needs the in-flight crossing of the 60 s repair and the guest's own boundary; it did not occur naturally in this run (the layer change came from a restored cut, and the trap-layout alignment after it read `0 to materialize, 0 to destroy`). |
| 2 | A runtime-entity creation report crosses the host's layer change — refused, answered, never relayed | machine | **unproven** | needs a swallowed live report + descent + stale re-report; not stageable on this machine. |
| 4 | No stamp / no baseline — pre-stamp behaviour | machine | **unproven** | an unstamped report cannot be produced by the deployed peers. |
| 5 | Third-party view — every peer's trap/entity world agrees with the host's after a layer change | machine | **unproven** | after the layer move the coarse object census matched on all three (`Trap 0 / Lock 6 / Item 26 / Building 0 / Door 0` — `k-E-object-*.json`), but the trap family itself was not isolated and the same state carries a layer-modifier baseline divergence plus a 74-vs-85 enemy split (`k-F-baseline-*.log`, `k-census-F-*.json`); reading "agreement" from a count-only census in that state would be a soft pass. |
| 6 | The stamp is on the wire, not added by the caller | machine | **unproven** | requires frame-level decoding; not available. |
| 7 | A guest receives a stale relay / a stale absolute table — refused; a guest never answers | machine | **unproven** | no stale relay/table crossing could be staged. |
| 9 | A stale answer while a second creation sits in the same cell — only the answered creation's pending report ends | machine | **unproven** | the stale-answer path is not stageable live. |

## Limits

- Row 3's evidence is the same-generation creation path (the family's materialization half); the trap-layout
  half of the family is not covered by it.
- Row 8 is a log-order read of one entry edge, not a frame decode.
- Row 5's count-only census cannot carry the row; the run records it as unproven rather than reading agreement
  into a state whose generation baselines diverge.
