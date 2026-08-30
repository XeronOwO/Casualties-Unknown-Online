# PlayerInteractionService flattening — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening". Decision: split the 716-line logical `PlayerInteractionService`
(partial class) into real top-level responsibility classes, not another physical
partial split. No behavior, DI, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `PlayerInteractionService.cs` | Was 511-line main partial; now a 94-line thin facade that composes the three domain services and delegates `IPlayerInteractionControl`. |
| `PlayerInteractionService.Heal.cs` | Was the 205-line heal partial; deleted. Its logic moved to `PlayerHealService`. |
| `PlayerInventoryTakeService.cs` | New 170-line class owns the cross-player inventory-take operation and its transfer event/publish path. |
| `PlayerCarryService.cs` | New 271-line class owns the host carry relation tables, carry start/stop arbitration, broadcast mirror and session lifecycle cleanup. |
| `PlayerHealService.cs` | New 217-line class owns the cross-player heal operation, item consumption and result publish path. |
| `PlayerCharacterAccess.cs` | New 106-line bounded access/projection helper shared by the three domain services: local/remote character get/save, in-world check and snapshot clone helpers. |
| DI registration | Unchanged — `PlayerInteractionService` remains the single `IPlayerInteractionControl` implementation; no new service registration. |
| Events | Forwarded through the facade with the same `TransferReceived` / `CarryStateChanged` / `HealReceived` surface. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old partial split (`PlayerInteractionService.cs` + `PlayerInteractionService.Heal.cs`)
  had three genuinely different responsibilities: take, carry/release and heal.
  The new split separates those responsibilities into first-class classes.
- State ownership remains internal: the carry relation dictionaries stay inside
  `PlayerCarryService`; no shared mutable state was moved into DI.
- No dead mechanism: no method body was dropped or rewritten semantically; each
  operation was moved verbatim into its owning class and the facade only delegates.
- No new expression-state bool fields were introduced.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `PlayerInteractionService` | 716 aggregate → 94 lines facade | `PlayerInteractionService.cs` |
| Heal partial | Removed | `PlayerInteractionService.Heal.cs` deleted |
| Take operation | New 170-line owner | `PlayerInventoryTakeService.cs` |
| Carry operation | New 271-line owner | `PlayerCarryService.cs` |
| Heal operation | New 217-line owner | `PlayerHealService.cs` |
| Character access | New 106-line shared projection | `PlayerCharacterAccess.cs` |
| One top-level type per file | All new files contain exactly one top-level type | `check-architecture.ps1` |
| DI / readonly wiring | Unchanged | Source diff; build |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `PlayerInteractionService` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split with no semantic rewrite.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `PlayerInteractionService`
  is no longer in `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- State remains owned by the operation owner; the facade is delegation only.
- Backlog updated: the `PlayerInteractionService` entry is removed from the
  remaining flattening list and from the architecture watchlist.
