# Delivery Self-Check Fact Sheets

Historical per-delivery records: mechanism × change × evidence + verification
design. They are **audit evidence**, not the current open-work view — see
[`../backlog.md`](../backlog.md), [`../tech-decisions.md`](../tech-decisions.md),
and [`../verification.md`](../verification.md).

- **[`MANIFEST.md`](MANIFEST.md)** is the complete per-file index: domain, current
  vs historical, and note. Use it before citing any selfcheck as current evidence.
- Each file corresponds to one delivery cycle or architecture phase.
- The canonical feature sync status lives in
  [`../item-features.md`](../item-features.md) and
  [`../entity-features.md`](../entity-features.md).
- Code search / runtime debugging paths are in
  [`../game-internals.md`](../game-internals.md).
- Older sheets may describe mechanisms that were later replaced (for example the
  legacy `ItemReject` frame or pre-kernel item wire paths). Those references are
  snapshots of the delivery at that time, not current behavior. Verify current
  mechanisms against [`../architecture-evolution/protocol.md`](../architecture-evolution/protocol.md)
  and [`../tech-decisions.md`](../tech-decisions.md).

## Architecture evolution self-checks

| Phase | Fact sheet | Current evidence? |
|---|---|---|
| A — Shadow kernel | [`phase-a-kernel-foundation-selfcheck.md`](phase-a-kernel-foundation-selfcheck.md) | Historical |
| B — Items authority | [`phase-b-item-authority-selfcheck.md`](phase-b-item-authority-selfcheck.md) | Historical |
| C — Protocol/save switch | [`phase-c-protocol-core-selfcheck.md`](phase-c-protocol-core-selfcheck.md) | Historical |
| D — Full domain migration | [`phase-d-full-domain-migration-selfcheck.md`](phase-d-full-domain-migration-selfcheck.md) | Current |
| E — Delete dual architecture | [`phase-e-legacy-inventory-selfcheck.md`](phase-e-legacy-inventory-selfcheck.md) | Current |

## Domain/feature self-checks

Per-domain or per-mechanism fact sheets are named by topic. Prefer current-evidence
sheets; use MANIFEST before citing historical ones.

Current examples:
- Items: `item-keyframe-state-selfcheck.md`,
  `container-content-sync-selfcheck.md`, `custom-item-data-state-selfcheck.md`
- Protocol: `netmsg-registry-selfcheck.md`, `world-entry-completion-selfcheck.md`
- Players: `respawn-rules-selfcheck.md`, `remote-backpack-container-take-selfcheck.md`
- Architecture: `phase-d-full-domain-migration-selfcheck.md`,
  `phase-e-legacy-inventory-selfcheck.md`
- Tooling: `partial-aware-gate-selfcheck.md`, `simtrace-diff-selfcheck.md`

Historical/superseded examples (bannered as HISTORICAL):
- `cross-player-item-use-selfcheck.md`, `cross-player-medicine-use-selfcheck.md`,
  `carry-interaction-selfcheck.md`, `enemy-targeting-selfcheck.md`,
  `pickup-inflight-selfcheck.md`, `online-ui-selfcheck.md`,
  `world-item-service-partial-split-selfcheck.md`

Use `Get-ChildItem docs/selfchecks -Filter *selfcheck.md` for the complete list and
[`MANIFEST.md`](MANIFEST.md) for status.
