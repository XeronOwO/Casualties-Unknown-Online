# Carried-inventory registration has no re-report

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / items / arbitration
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row I8)
- Related: `review/remote-backpack-native-interaction-parity.md`, `review/remote-backpack-item-projection-acceptance-issues.md`

## Problem (evidence)

`CarriedInventory` (NetMsg 65) is the guest's one-shot registration of the item
ids it self-assigned during generation. The host uses it to build the
per-guest transfer table used by cross-player take/arbitration.

- Only send site: `src/CasualtiesUnknownOnline.GameAdapter/Items/CarriedInventoryReporter.cs:133`
  (`_items.SendCarriedInventory(items);`), emitted on the generation-finished
  edge (`:51`, `if (_generating)`).
- Channel: `src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemIdCoordinator.cs:104`
  (`SendCarriedInventory`), reliable by `PacketSender` default.
- No periodic re-report, and the 1 Hz character snapshot does not feed the host's
  arbitration table (`CharacterDataStore.SaveCharacterData` stores the snapshot
  only), so a swallowed registration leaves the host's transfer table empty for
  those ids.
- Consequence at the arbitration seam: `ItemArbitration.AdoptEvidence` logs
  "no transfer-table entry, not arbitrated" and returns null; the kernel state is
  partially healed by the accepted-first carried update path
  (`src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelProtocolCommandHandler.cs:168`),
  but the arbitration table is not.
- `ItemIdWatermark` (NetMsg 64) is the better-behaved sibling: it is monotonic and
  self-heals on the next allocation
  (`src/CasualtiesUnknownOnline.GameAdapter/Items/ItemIdAllocator.cs:33`), and the
  host re-grants it on member add (`ItemIdCoordinator.cs:42-49`).

## Goal

The host's per-guest carried-id registration converges after a swallowed
`CarriedInventory`, so cross-player take/drop arbitration works for the guest's
starting inventory and craft products without requiring a reconnect.

## Design direction (decide at implementation)

1. **Low-frequency absolute re-report** — re-send the guest's current carried-id
   set periodically (e.g. 5–10 s) and on member (re)entry; the host replaces its
   table for that guest idempotently.
2. **Host request on miss** — when arbitration misses an id, ask the owner for a
   registration refresh.
3. **Fold into the character snapshot** — teach the 1 Hz snapshot consumer to
   register unknown carried ids (largest change; only if it keeps the snapshot a
   read model).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest starting inventory; registration dropped | Host converges; take arbitration works |
| 2 | Craft product ids added after the first report | Converge on the next re-report |
| 3 | Reconnect while in world | Table rebuilt exactly once |
| 4 | Guest picks up a world item (id already host-known) | No duplicate registration |
| 5 | Two guests with overlapping local counters | Host keeps per-guest tables separate |
| 6 | Item destroyed after registration | Terminal fact wins; no resurrection |
| 7 | Registration arrives before the item fact | Accepted-first path still works |

## Non-goals

- Item id allocation strategy changes.
- Cross-player take protocol changes (the arbitration seam stays).
