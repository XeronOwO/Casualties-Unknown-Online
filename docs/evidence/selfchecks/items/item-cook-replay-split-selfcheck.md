# ItemApplication cook-replay split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (顺带完成 alongside the `PlayerInteractionService` split). Decision:
extract the heater-cook replay apply side from the `ItemApplication` partial
into a real top-level `ItemCookReplayApplier`, so the 630-line logical class
drops below the 600-line gate. No behavior, DI, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `ItemApplication.cs` | Was 578-line main partial; now 587-line normal class (not partial) that keeps the remote world-item application surface and delegates the cook replay through `_cookReplay`. |
| `ItemApplication.Heater.cs` | Was the 52-line heater partial; deleted. |
| `ItemCookReplayApplier.cs` | New 59-line top-level class owns the host→guest ItemCook replay: kill source, spawn cooked item from the event fact, replay the guest's Scald sound once. |
| Event wiring | `ItemApplication.BindToSession` / `Unbind` now subscribe/unsubscribe `_items.ItemCookedReceived` to `_cookReplay.OnRemoteItemCooked`. |
| DI registration | Unchanged — `ItemApplication` is still constructed by the Game Adapter composition; `ItemCookReplayApplier` is an internal owned dependency, not a DI service. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old partial split had two distinct responsibilities: the general remote
  item application surface and the specific heater-cook replay. The new split
  gives the cook replay its own top-level class.
- All mutable state stays in `ItemApplication` (`_materializedFrame`,
  `PickupOrigins`); the replay applier only calls into the owner's existing
  internal `KillRemoteItem` / `SpawnWorldItem` and the static item lookup.
- No dead mechanism: the same event path, same RemoteApply scope, same
  idempotent duplicate guard and same positional sound replay remain.
- No new expression-state bool fields.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `ItemApplication` | 630 aggregate → 587 lines | `ItemApplication.cs` |
| Heater partial | Removed | `ItemApplication.Heater.cs` deleted |
| Cook replay owner | New 59-line top-level class | `ItemCookReplayApplier.cs` |
| One top-level type per file | New file contains exactly one top-level type | `check-architecture.ps1` |
| Event wiring | Same event, new delegate target | `ItemApplication.BindToSession` / `Unbind` |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `ItemApplication` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split with no semantic rewrite; the
  event handler body is verbatim in the new class.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it, including any easily reachable adjacent items. This cycle's plan is
approved without a separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `ItemApplication` is no longer
  in `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The replay applier is an internal owned dependency, not DI-visible shared
  state.
- Backlog updated: `ItemApplication` removed from the remaining flattening
  list.
