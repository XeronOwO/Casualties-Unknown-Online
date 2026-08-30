# Delivery Self-Check Fact Sheets

Historical per-delivery records: mechanism × change × evidence + verification
design. They are **audit evidence**, not the current open-work view — see
[`../../backlog/README.md`](../../backlog/README.md),
[`../../decisions/active.md`](../../decisions/active.md), and
[`../verification.md`](../verification.md).

- **[`MANIFEST.md`](MANIFEST.md)** is the complete per-file index: domain, current
  vs historical, and note. Use it before citing any selfcheck as current evidence.
- Each file corresponds to one delivery cycle or architecture phase.
- The canonical feature sync status lives in
  [`../../features/items.md`](../../features/items.md) and
  [`../../features/entities.md`](../../features/entities.md).
- Code search / runtime debugging paths are in
  [`../../features/game-internals.md`](../../features/game-internals.md).
- Older sheets may describe mechanisms that were later replaced (for example the
  legacy `ItemReject` frame or pre-kernel item wire paths). Those references are
  snapshots of the delivery at that time, not current behavior. Verify current
  mechanisms against [`../../architecture/protocol.md`](../../architecture/protocol.md)
  and [`../../decisions/active.md`](../../decisions/active.md).

## Architecture evolution self-checks

| Phase | Fact sheet | Current evidence? |
|---|---|---|
| A — Shadow kernel | [`phase-a-kernel-foundation-selfcheck.md`](architecture/phase-a-kernel-foundation-selfcheck.md) | Historical |
| B — Items authority | [`phase-b-item-authority-selfcheck.md`](architecture/phase-b-item-authority-selfcheck.md) | Historical |
| C — Protocol/save switch | [`phase-c-protocol-core-selfcheck.md`](architecture/phase-c-protocol-core-selfcheck.md) | Historical |
| D — Full domain migration | [`phase-d-full-domain-migration-selfcheck.md`](architecture/phase-d-full-domain-migration-selfcheck.md) | Current |
| E — Delete dual architecture | [`phase-e-legacy-inventory-selfcheck.md`](architecture/phase-e-legacy-inventory-selfcheck.md) | Current |

## Domain/feature self-checks

Per-domain or per-mechanism fact sheets are named by topic. Prefer current-evidence
sheets; use MANIFEST before citing historical ones.

Current examples:

- Items: `items/item-keyframe-state-selfcheck.md`,
  `items/container-content-sync-selfcheck.md`,
  `items/custom-item-data-state-selfcheck.md`
- Protocol: `protocol/netmsg-registry-selfcheck.md`,
  `protocol/world-entry-completion-selfcheck.md`
- Players: `players/respawn-rules-selfcheck.md`,
  `items/remote-backpack-container-take-selfcheck.md`
- Architecture: `architecture/phase-d-full-domain-migration-selfcheck.md`,
  `architecture/phase-e-legacy-inventory-selfcheck.md`
- Tooling: `tooling/partial-aware-gate-selfcheck.md`,
  `tooling/simtrace-diff-selfcheck.md`

Historical/superseded examples (bannered as HISTORICAL):

- `items/cross-player-item-use-selfcheck.md`,
  `players/cross-player-medicine-use-selfcheck.md`,
  `players/carry-interaction-selfcheck.md`,
  `enemies/enemy-targeting-selfcheck.md`,
  `items/pickup-inflight-selfcheck.md`,
  `ui/online-ui-selfcheck.md`,
  `items/world-item-service-partial-split-selfcheck.md`

Use `Get-ChildItem docs/evidence/selfchecks -Filter *selfcheck.md -Recurse` for the
complete list and [`MANIFEST.md`](MANIFEST.md) for status.
