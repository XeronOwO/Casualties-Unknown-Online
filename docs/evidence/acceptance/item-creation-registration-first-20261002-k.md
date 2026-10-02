# Acceptance record — An item can be operated on before its creation is registered

- Ticket: `item-creation-registration-first` — verdict: **back to `todo/`** (rows 1 and 3 pass; rows 2, 4, 5, 6 and 7 unproven)
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
| 1 | Guest creates an item, then operates on it — the host judges the creation first; the operation needs no wait | machine | **pass** | guest created a geofruit (`k-D-guest-create.json`, item `14451443822`, slot 1) and used it immediately (`k-D-guest-use.json`, only the aim left alone); the guest's own ordering gate is visible in `[ItemCommand] ItemSpawn on item 14451443822 … 1 report(s) now outstanding` followed by `ItemPickup … 2 report(s) now outstanding` (`k-D-guest2.log`); the host materialized the creation first — `[ItemSpawn] materializing geofruit (id 14451443822) at (12.1,480.6)` and `ItemArbitration Rebuilt transfer table … items` — then received both commands (`Receive ItemSpawnCommand=96B/1f`, `Receive ItemPickupCommand=68B/1f`) and the later use (`Receive ItemUpdateStateCommand=76B/1f`); **zero** `Protocol violation:` lines in the window (`k-D-host2.log`); guest `[ItemTrace] op=18 … origin=OnItemPickedUp result=Committed(2)` and `[ItemUsed] geofruit (id 14451443822) reported (digest)` (`k-D-guest2.log`) |
| 3 | Creation and operation in flight together — judged as one unit (all-or-nothing outcome) | machine | **pass** | the same run: the creation report and the pickup report left back-to-back (both outstanding at 16:55:42.266) and both were judged — the host had the creation and the resulting item table converged with no refusal (`k-D-host2.log`, `k-D-guest2.log`). Limit: the run cannot frame-decode the wire to prove they travelled as one datagram; the observed outcome is the all-or-nothing shape. |
| 2 | The creation is refused — every later operation answered with the precise reason | machine | **unproven** | no legitimate refused-creation drive was identified (reject paths need a lost first-writer-wins break or a rejected `ItemSpawn`); not staged. |
| 4 | Two senders, one item — no "unknown item" window is needed to order them | machine | **unproven** | both guests attempted the dropped world item `1117012477891` (`k-D-guest-pickup.json`, `k-D-alt-pickup.json`) and both were refused by the game's own `PickUpItem` guard (`pickup-refused: left geofruit outside slot …`, their starting supplies occupy the slots — the recipe's pickup path is deliberately unforced); a simultaneous two-sender race is also not stageable (the driver's inter-command gap, ~2.4 s, exceeds the relay latency). Named missing setup. |
| 5 | Reconnect / late join — unchanged semantics; the tombstone is not resurrected | machine | **unproven** | the re-entry restored a layer-end cut (new layer), so the item-table reconnect semantics were not exercised in place; the host rebuilt its transfer table (`k-D-host2.log`) but no tombstone state existed to observe. |
| 6 | Unknown-item operation reaching the host — recorded as a protocol violation, not silently held | machine | **unproven** | no legitimate unjudged-item operation exists to stage: `item-provide mode=create` reports spawn-then-pickup (its review note), and the run has no way to forge an operation envelope. |
| 7 | Regression: generation-time items, container contents, drops, crafting, trade — unchanged outcomes | machine | **unproven** | staged halves: host drop propagated with the same readable id to all three (`k-item-D-pre-*.json`: `1117012477891`); local container reads succeeded on all three (`k-D-container-local-*.json`; host view of the guest: `tableCount=3, worldCount=264, carriedCount=4, containedCount=0, terminalCount=0` — `k-D-container-hostview.json`); generation-item census equal on all three (269 items — `k-item-D-pre-*.json`). Crafting and trade were not staged, so the whole regression row stays unproven. |

## Limits

- Rows 2 and 6 need setups the committed vocabulary does not have (a refused creation, a forged unknown-item
  operation); both are named rather than guessed.
- Row 4's native refusal is a local inventory-state condition, not a CUO judgment; the run reports it as the
  reason the two-sender shape could not be put into the world.
- Row 1/3 evidence is order-of-commands plus the host's materialization; no frame decode was performed.
