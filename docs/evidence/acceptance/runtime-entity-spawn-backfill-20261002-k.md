# Acceptance record — Runtime-created BuildingEntity spawns have no backfill or re-report

- Ticket: `runtime-entity-spawn-backfill` — verdict: **back to `todo/`** (rows 5–10 pass; rows 1–4, 11 and 12 unproven)
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
| 1 | Guest creates a runtime entity; its report is dropped — host creates its copy; third-party guests see it | machine | **unproven** | no packet-loss injection on this machine (scope §3); the run cannot drop the report. |
| 2 | Host creates a runtime entity; the relay is dropped — guest converges without reconnect | machine | **unproven** | same capability boundary; no relay-drop injection. |
| 3 | Late joiner enters the world — all runtime entities present exactly once | machine | **unproven** | the re-entry ran through the layer-end Continue: the restored cut moved the session to layer 1 and the runtime-entity table was reset there, so the same-world late-join shape was not staged (an in-place reconnect of the same world would be needed). |
| 4 | Reconnect while in world — same; no duplicates | machine | **unproven** | same substitution as row 3. |
| 5 | Creation-time payload (keypad code / geyser liquid type / crystal tint) preserved on every path | machine | **pass** | host spawned `dropcapsule`, `geyser` and `crystalenemy` through the game's own console (`k-C-spawn-keypad.json`, `k-C-spawn-geyser.json`, `k-C-spawn-crystal.json`); host logged `reporting dropcapsule … (keypad code carried)`, `reporting geyser … (liquid 2)`, `reporting crystalenemy … (crystal tint carried)` (`k-C-payload-host.log`); each peer logged `applied carried keypad code`, `applied carried liquid type 2`, `applied carried crystal tint` (`k-C-payload-guest.log`, `k-C-payload-alt.log`); the payload read is identical on all three — keypad code `458596975`, geyser `liquidType 2`, crystal tint `1,1,1,1` / intensity `1` (`k-payload-C-*.json`) |
| 6 | Entity destroyed before the re-report — not resurrected | machine | **pass** | creation sequence `2560977844` destroyed (`k-C-kill-seq44.json`: health 100 → -293.8, host `died — dropped the accepted creation record`); a full repair cycle later it is absent on all three while the sibling `2560977845` is present (`k-creation-census3-*.json`, `k-creation-census4-*.json`) |
| 7 | Layer regeneration — runtime entities from the previous layer are not re-materialized | machine | **pass** | after the layer-end Continue the host logged `Captured world baseline … the runtime-entity table is reset` (`k-F-world-host.log`, 16:59:28) and every client's runtime-creation markers went to zero (`k-census-F-*.json`: `withCreationMarker=0` on host, guest and alt); no keypad/geyser/animals from layer 0 materialized in layer 1. Substitution: the layer change came from the restored layer-end cut, not a `skiplayer` descent. |
| 8 | Duplicate delivery of the same creation record — exactly one local entity | machine | **pass** | the 60 s snapshot re-applies the creation table and the counts stay exact: keypads 33/33/33, geysers 100/99/99 (the one-copy difference is a generation-geyser count, see Limits), animals 87/87/87 after the snapshot (`k-payload-C-*.json`, `k-census-C3-*.json`); peers logged `applied host runtime-entity snapshot (2 entries, 3 animal acknowledgements)` at 16:51:46 and 16:55:47 with no extra copies (`k-C-payload-guest.log`; note the `[EntitySpawn] created …` line is logged unconditionally on a rebind — it is not duplication evidence) |
| 9 | Two identical prefabs created inside one cell — two entities on every peer; a death drops only its own record | machine | **pass** | `k-spawn-pair.cs` created two shadecrawlers at (5.591,484.616) and (5.991,484.616) — one cell (5,484) — with distinct creation sequences `2560977844`/`2560977845` (`k-C-pair2.json`, `k-creation-census2-*.json`: both present on host, guest and alt with the same creation key); destroying `2560977844` removed only it while `2560977845` stayed (`k-C-kill-seq44.json`, `k-creation-census3-*.json`, `k-creation-census4-*.json`) |
| 10 | An accepted animal report — acknowledged by the snapshot key list; never materialized from it | machine | **pass** | guest-direction creation: guest `reporting shadecrawler … (animal)`; host `created shadecrawler` (materialized from the live report) and the guest logged `host answered shadecrawler … dropped the pending report (0 left)` (`k-C-guest-direction-host.log`, `k-C-guest-direction-guest.log`); both peers logged `applied host runtime-entity snapshot (0 entries, 2 animal acknowledgements)` (`k-C-ack-guest.log`, `k-C-ack-alt.log`) with zero `materialized runtime spawn`; census 86 → 87 on every client with `withCreationMarker=2` (`k-census-C3-*.json`) |
| 11 | The host cannot materialize a reported creation — rejected, answered, local copy destroyed | machine | **unproven** | needs a prefab/template the host lacks; not stageable on a single install (named). |
| 12 | A MOD-registered template (building or animal) — materialized, never rejected by a `Resources.Load` pre-check | machine | **unproven** | no mod-registered template staged in this batch (named). |

## Limits

- Rows 3 and 4 need an in-place same-world reconnect; the run's Continue restored the run's last cut (a layer-end
  cut), which replaced the layer instead — named as the missing setup.
- Row 8's geyser counts differ between host (100) and peers (99) because the peers lost one *generation* geyser
  during the entry trap-layout alignment (guest `applied 99` vs alt `applied 98` of the host's 99 host liquid
  types at entry; both guests `destroying surplus GeyserActivated at (-339.4,-360.4)` in `k-A-entry-*.log`). The
  runtime-created geyser itself exists on all three (`k-geyser-check-*.json`, `marker:true`), so row 5's payload
  half is unaffected — the count gap is recorded here as an adjacent observation.
- The kill probe's 1.5-unit explosion also destroyed the runtime geyser at (5.9,482.6) (host
  `geyser … died — dropped the accepted creation record`); this is a probe-side collateral, named for
  reproducibility.
- One session; the snapshot re-apply was sampled twice per state.
