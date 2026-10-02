# Acceptance record — Enemy snapshot binding has no recovery path

- Ticket: `enemy-snapshot-binding-recovery` — verdict: **back to `todo/`** (rows 3, 4 and 8 **fail**; row 2 unproven; rows 1, 5 and 6 pass)
- Batch: `20261002-k` — tickets `guest-block-mutation-re-report`, `runtime-entity-spawn-backfill`,
  `enemy-snapshot-binding-recovery`, `item-creation-registration-first`, `world-layer-generation-identity`,
  `generation-identity-remaining-families`, `world-determinism-world-fingerprint`
- Commit: `393d79a8` (artifact) · tree `b20984cb` (docs-only) · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+393d79a8c0d8f4a20320b667f6122884daa656ae`
- Run: 2026-10-02, 16:38–17:03 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (`dotnet`, `game`, `deploy`,
  `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`, `artifacts`)
- Artifacts: `k-*` in the directory named by `acceptance-artifacts-dir`; this record cites artifact ids only

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Enemies published after the entry edge still reach a member that stays in world, via the in-session repair | machine | **pass** | entry edges applied an empty pass, then the first repair repeat bound the whole set: host `snapshot sent … 85 enemies, 0 runtime spawns` at 16:40:06.9 and 16:40:46.5 (`k-A-entry-host.log`); both guests `snapshot applied: 85 generated bound, 0 runtime spawns, mapping=True` at 16:40:46.7 (`k-A-entry-guest.log`, `k-A-entry-alt.log`); zero `generation spawn pairing failed` / `mapping=False` in that window |
| 2 | An empty host enemy table sends nothing | machine | **unproven** | no window with an empty host table was isolated: the host's table was populated by the time the run's sends happened, and the guests' `0 generated bound` lines are applies of a non-empty snapshot before their local copies existed (batch `20261002-g` read the same shape). Missing setup named. |
| 3 | A late joiner receives the full enemy snapshot | machine | **fail** | after the layer-end Continue re-entry the host sent `74 enemies, 0 runtime spawns` (16:59:44, 16:59:46, 17:00:46 — `k-F-snapshot-latest-host.log`) while both guests held `85` generated copies; each guest logged `generation spawn pairing failed (74 host vs 85 guest generated enemies)` + `snapshot applied: -11 generated bound, 0 runtime spawns, mapping=False`, repeating every repair cycle (`k-F-snapshot-latest-guest.log`) — the snapshot arrived but the set never bound |
| 4 | Reconnect-while-InWorld re-fans the same group | machine | **fail** | same run state as row 3: the re-fan happened (host `snapshot sent` per member) but both members' pairing failed and stayed `mapping=False` across at least two repair cycles (`k-F-snapshot-latest-*.log`) |
| 5 | The runtime-spawn half of the snapshot is unchanged | machine | **pass** | host direction: `k-C-spawn-host.json` `ok:true`, host `reporting shadecrawler … (animal — recovery owned by the enemy domain)` + `host bound runtime spawn …:85:0`; each peer `created shadecrawler` once + `bound 1 runtime enemy copies to host ids` once (`k-C-host.log`, `k-C-guest.log`, `k-C-alt.log`); census 85 → 86 on all three with `withCreationMarker=1` (`k-census-A-*.json`, `k-census-C2-*.json`, `k-census-C2-compare.txt`) |
| 6 | An explicit removal is final and no snapshot resurrects it | machine | **pass** | `k-kill-seq.cs` removed creation sequence `2560977844` (health 100 → -293.8, `k-C-kill-seq44.json`); the creation disappeared on all three clients (`k-creation-census3-*.json`: `seq44=False`) while its same-cell sibling `2560977845` stayed (`seq45=True`); a full repair cycle and re-apply later still showed both unchanged (`k-creation-census4-*.json`) |
| 8 | Peers agree on the shared enemy id set and terminal health across two live clients | machine | **fail** | post-re-entry census: host 74 animals (69 shadecrawler + 5 trader), guest 85 (78 + 7), alt 85 (`k-census-F-*.json`); both guests repeat `[LayerMod] baseline divergence — local segment start 5200E7D148E10BB7426A68F7E2407117 vs host's E76DFACE27BBFC1FC9FFB1C2648822EB (world effects may diverge)` every 10 s (`k-F-baseline-guest.log`, `k-F-baseline-alt.log`); before the re-entry the three health censuses were identical (85/85/85, 0 differences — `k-health-A-*.json`, `k-health-A-compare.txt`) |

## Limits

- Rows 3, 4 and 8 were judged after the run's **layer-end Continue** re-entry: the host restored layer 1 while
  both guests regenerated it, and the two sides' layer-modifier baselines diverged. The enemy-binding failure is
  real and observed in that state; its root cause may be the restore/regeneration divergence rather than the
  snapshot binding itself — the fix cycle must attribute it. The divergence is filed separately
  (`docs/backlog/todo/layer-mod-baseline-divergence-on-continue.md`).
- Row 2's empty-table no-op had no isolated window.
- One session; one sample per state; the repair cycle was read at least twice per state.
