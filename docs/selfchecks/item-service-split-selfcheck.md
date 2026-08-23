# ItemService message-flow split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (continued). Decision: split the 928-line logical `ItemService`
into a thin `IItemControl` facade plus two real top-level responsibility
classes: `ItemMessageFlowService` (report/receive message flow) and
`ItemPendingPickupArbiter` (host-side pending-pickup arbitration). No behavior,
DI, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `ItemService.cs` | Was 309-line main partial + four more partials (928 aggregate); now a 411-line normal facade. It keeps the world table, sub-services, application events, host-only surfaces, crafting seams, traffic and direct player-interaction forwarding. |
| `ItemMessageFlowService.cs` | New 312-line top-level class owns the report/receive message flow: spawn/cook/pickup/use/slot/drop/destroy sends, wire receive events, block-drop registration, corrections and snapshot/action receive forwarding. |
| `ItemPendingPickupArbiter.cs` | New 233-line top-level class owns the host-side pending-pickup queue: queued claims, spawn/drop registration settlement, first-writer-wins rejection and the expiry pump. |
| Deleted partials | `ItemService.PendingPickups.cs`, `ItemService.ReportReceive.cs`, `ItemService.PlayerInteraction.cs`, `ItemService.Traffic.cs` removed. |
| DI registration | Unchanged — `ItemService` remains the single `IItemControl` implementation; the two new classes are internal owned dependencies. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old logical class mixed arbitration, message flow and traffic in one
  partial-family. The new split gives message flow and pending-pickup
  arbitration first-class owners while the facade keeps the authoritative
  table and sub-service composition.
- State ownership: `WorldItemTable` and `ItemTrafficTracker` stay in
  `ItemService`; `PendingPickupQueue` moves with `ItemPendingPickupArbiter`.
- Event forwarding is explicit: the two child classes receive callbacks that
  raise the `ItemSpawned` / `ItemPickedUp` / `ItemDropped` / `ItemDestroyed` /
  `ItemCookedReceived` / `ItemRejected` events on the facade.
- No dead mechanism: all moved method bodies are verbatim and delegated.
- No new expression-state bool fields.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `ItemService` | 928 aggregate → 411-line facade | `ItemService.cs` |
| Message flow | New 312-line top-level class | `ItemMessageFlowService.cs` |
| Pending-pickup arbitration | New 233-line top-level class | `ItemPendingPickupArbiter.cs` |
| Old partials | Removed | `git status` / deleted files |
| One top-level type per file | New files contain exactly one top-level type | `check-architecture.ps1` |
| DI / readonly wiring | Unchanged | Source diff; build |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `ItemService` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split; the moved method bodies are
  verbatim in the new classes, and the facade is explicit delegation.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to continue the autonomous backlog item and
complete adjacent architecture work. This cycle's plan is approved without a
separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `ItemService` is no longer in
  `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The new classes are internal owned dependencies, not DI-visible shared state.
- Backlog updated: `ItemService` removed from the remaining flattening list
  and the architecture watchlist note now points to the real top-level split.
