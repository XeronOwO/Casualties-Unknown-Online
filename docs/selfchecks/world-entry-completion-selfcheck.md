# World-entry snapshot completion + fan-out ownership — self-check (2026-08-23)

Backlog §3.4 asked for an explicit completion marker for the world-entry
snapshot group; §3.3 also called out `HandlerContext` owning world-entry
fan-out. This cycle closes the completion semantics and moves the fan-out into
its own service, shrinking `HandlerContext`'s responsibilities.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `HandlerContext.SendWorldStateToMember` | Old world-entry fan-out method on the handler context (removed). |
| `SceneStateHandler` / `HandshakeHandler` | The only two callers of that fan-out; they now depend on `WorldEntryFanout`. |
| World-entry snapshots | `BlockState`, `BlockDamage`, `TrapState`, `OpenedEntities`, `BuildingEntityHealth`, `TrapLayout`, `RadiationLineState`, `ItemSnapshot`, `EnemySnapshot`. |
| Completion signal | None existed; a late join/reconnect could not tell a full backfill from a partial/best-effort set. |
| `WorldService` / `IWorldControl` | The world-state send surface; new completion send/receive event rides the same domain. |

## 2. Whole-family audit

- All world-entry fan-out call sites were aligned: `SceneStateHandler` InWorld
  edge and `HandshakeHandler` reconnect-while-InWorld both use
  `WorldEntryFanout.Send`.
- The completion marker is sent in the same service, after the whole snapshot
  group, so it covers first entry, late join and reconnect.
- World-ready / start-gate semantics unchanged: `WorldReady` still means
  "start playing"; `WorldSnapshotComplete` is only the backfill group marker.
- No handler/control-plane behavior changed beyond moving the fan-out; no dead
  mechanism left behind (`SendWorldStateToMember` removed).

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Fan-out ownership | `HandlerContext.SendWorldStateToMember` → `WorldEntryFanout.Send` | `HandlerContext.cs`, `WorldEntryFanout.cs` |
| Callers | `SceneStateHandler` / `HandshakeHandler` inject `WorldEntryFanout` | those handler files |
| Completion wire signal | New `WorldSnapshotComplete` (NetMsg 110, HostToGuest) | `NetMsg.cs`, `WorldSnapshotCompleteMsg.cs` |
| Completion receive surface | `IWorldControl.SendWorldSnapshotComplete` / `FireWorldSnapshotCompleteReceived` / event | `IWorldControl.cs`, `WorldService.MessageFlow.cs`, `WorldSnapshotCompleteHandler.cs` |
| Protocol | `ProtocolVersion` 37 → 38 | `ProtocolVersion.cs` |
| Tests | first entry + reconnect both receive the marker | `WorldEntrySnapshotTests`, `ReconnectWorldSnapshotTests`, `DirectionTests` |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| Protocol | 37 → 38 |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- The new completion marker is exercised through fake-network L0 tests on both
  the first InWorld edge and reconnect-while-InWorld.
- No manual dual-side acceptance required for this protocol-metadata/backfill
  marker change (user rule 2026-08-16).

## 6. Plan approval

The user instructed this session to continue autonomously after the previous
NetMsg-registry cycle, so this cycle's plan is approved without a separate
interactive approval step.

## 7. Structure review

- New top-level types are one per file: `WorldEntryFanout`,
  `WorldSnapshotCompleteMsg`, `WorldSnapshotCompleteHandler`.
- `HandlerContext` is reduced: it no longer owns a concrete world-entry flow.
- Touched classes remain within the 600-line gate.
- No new expression-state bool fields.
- No state ownership change: the world-entry snapshot sources stay with their
  owning registries/domains; `WorldEntryFanout` only orchestrates sends.
