# EnemySyncCoordinator combat-replay split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (continued). Decision: extract the guest-side host-ordered
attack/bite replay from `EnemySyncCoordinator` into a real top-level
`EnemyCombatReplay`, dropping the logical class from 750 aggregate lines to 542.
No behavior, DI, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `EnemySyncCoordinator.cs` | Was 551-line main partial + 199-line runtime-spawn partial (750 aggregate); now 343-line main partial + 199-line runtime-spawn partial (542 aggregate) after combat extraction. |
| `EnemyCombatReplay.cs` | New 257-line top-level class owns host-ordered spider-bite/crystal-lunge application, local crystal-lunge reporting, enemy-bite reporting and the received bite/lunge apply paths. |
| `EnemySyncCoordinator.RuntimeSpawns.cs` | Unchanged partial: still owns runtime-spawn materialization/pairing inside the coordinator. |
| Event wiring | `EnemySyncCoordinator.BindToSession` / `Unbind` now subscribe/unsubscribe `EnemyAttackReceived` / `EnemyLungeReceived` / `EnemyBiteReceived` to the `EnemyCombatReplay` instance. |
| Facade methods | `ReportLocalCrystalLunge` and `ReportEnemyBite` remain on `EnemySyncCoordinator` as thin delegations so patch/caller surfaces do not change. |
| DI / ownership | `EnemyCombatReplay` is an internal owned dependency, not a DI service; all enemy mapping state stays in `EnemySyncCoordinator`. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old logical class mixed enemy binding/streaming with guest combat
  replay. The new split gives each a first-class owner.
- Mutable mapping state (`_idByEntity`, `_entityById`, `_healthReconcile`,
  runtime sets, mapping flags) remains in `EnemySyncCoordinator`;
  `EnemyCombatReplay` resolves entities through a `Func<NetworkEntityId, BuildingEntity?>`
  delegate, so it does not own or duplicate the mapping.
- No dead mechanism: the same event paths, RemoteApply/command semantics,
  dedicated EnemyBite/EnemyLunge reports and local damage reconciliation remain.
- No new expression-state bool fields.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `EnemySyncCoordinator` | 750 aggregate → 542 aggregate | `EnemySyncCoordinator.cs` + `EnemySyncCoordinator.RuntimeSpawns.cs` |
| Combat replay | New 257-line top-level class | `EnemyCombatReplay.cs` |
| Event wiring | Same events, new delegate target | `BindToSession` / `Unbind` |
| One top-level type per file | New file contains exactly one top-level type | `check-architecture.ps1` |
| DI / readonly wiring | Unchanged | Source diff; build |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `EnemySyncCoordinator` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split; the moved method bodies are
  verbatim in `EnemyCombatReplay`.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to continue the autonomous backlog item and
complete adjacent architecture work. This cycle's plan is approved without a
separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `EnemySyncCoordinator` is no
  longer in `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The replay class is an internal owned dependency, not DI-visible shared
  state.
- Backlog updated: `EnemySyncCoordinator` removed from the remaining
  flattening list and from the architecture watchlist.
