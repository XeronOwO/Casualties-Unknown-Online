# Delivery Self-Check Fact Sheets

Historical per-delivery records: mechanism × change × evidence + verification
design. They are **audit evidence**, not the current open-work view — see
[`../backlog.md`](../backlog.md), [`../tech-decisions.md`](../tech-decisions.md),
and [`../verification.md`](../verification.md).

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

| Phase | Fact sheet |
|---|---|
| A — Shadow kernel | [`phase-a-kernel-foundation-selfcheck.md`](phase-a-kernel-foundation-selfcheck.md) |
| B — Items authority | [`phase-b-item-authority-selfcheck.md`](phase-b-item-authority-selfcheck.md) |
| C — Protocol/save switch | [`phase-c-protocol-core-selfcheck.md`](phase-c-protocol-core-selfcheck.md) |
| D — Full domain migration | [`phase-d-full-domain-migration-selfcheck.md`](phase-d-full-domain-migration-selfcheck.md) |
| E — Delete dual architecture | [`phase-e-legacy-inventory-selfcheck.md`](phase-e-legacy-inventory-selfcheck.md) |

## Domain/feature self-checks

Per-domain or per-mechanism fact sheets are named by topic, for example:

- Items: `item-service-split-selfcheck.md`, `custom-item-data-state-selfcheck.md`,
  `container-content-sync-selfcheck.md`
- Players / cross-player: `cross-player-item-use-selfcheck.md`,
  `cross-player-medicine-use-selfcheck.md`, `carry-interaction-selfcheck.md`,
  `remote-inventory-view-selfcheck.md`
- Enemies: `enemy-targeting-selfcheck.md`, `enemy-combat-replay-split-selfcheck.md`,
  `crystal-teleport-sync-selfcheck.md`
- World/entities: `world-event-relay`-related sheets, `crystal-*-sync-selfcheck.md`
- Fluids: `fluid-presentation-selfcheck.md`
- Mod API: `mod-api`-related sheets (`mod-content-registration-selfcheck.md`,
  `mod-native-api-selfcheck.md`, `mod-ui-selfcheck.md`, etc.)

Use `Get-ChildItem docs/selfchecks -Filter *selfcheck.md` for the complete list.
