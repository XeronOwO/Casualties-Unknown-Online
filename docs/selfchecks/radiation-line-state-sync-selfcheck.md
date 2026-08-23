# RadiationLine World-State Sync — Self-Check (2026-08-23)

Delivery fact sheet for the host-authoritative radiation-line world state
(backlog: RadiationLine world-state sync, exploration §1.1). ProtocolVersion
33, NetMsg 106.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Layer-timer activation | `WorldGeneration.Update`: `layerTimeSpent > maxTimePerLayer` → `RadiationLine.line.Activate()` (WorldGeneration.cs:859-863) | host owns the activation; guest local `layerTimeSpent` is capped at `maxTimePerLayer` in `WorldGenerationUpdatePatch.Prefix` so the guest can never independently start the line | `WorldGenerationUpdatePatch.cs` |
| 2 | Line descent | `RadiationLine.Update`: `timeGone += delta * (body.conscious ? 1 : 0.2) * 1.5`; transform position follows `timeGone` (RadiationLine.cs:Update) | host broadcasts the absolute `timeGone`; the guest applies it and continues the local per-frame presentation/body effects between resends | `RadiationLineSync.cs` |
| 3 | Local body effects | `RadiationLine.Update` applies `radiationSickness`, `eyeScareTime`, `SetIrradiateIntensity` to the local body above the line | unchanged — still local per side; the host only owns the line's world state, not the per-player simulation (local-compute mandate) | `RadiationLineSync.OnRadiationLineStateReceived` |
| 4 | Deactivation | `RadiationLine.Deactivate()` resets `active`, `timeGone` and moves the line above the world (world generation / clear) | host broadcasts an inactive state on transition and stores it for world entry; guest applies `Deactivate()` when it receives inactive | `RadiationLineSync`, `WorldService` |
| 5 | Late joiner / reconnect | a regenerated/joining client has no memory of the host's current boundary | `HandlerContext.SendWorldStateToMember` includes the stored `RadiationLineState` snapshot; `WorldService.SetRadiationLineState` keeps it current in solo too | `HandlerContext.cs`, `WorldService.MessageFlow.cs` |
| 6 | Periodic stabilization | each side's local line could diverge over time | host re-broadcasts the absolute state at 5 Hz while active; guests re-align | `RadiationLineSync.Update` |

## Design

- **Message**: `RadiationLineStateMsg` (`Active` bool + `TimeGone` float),
  `NetMsg.RadiationLineState = 106`, host→guest only.
- **Runtime plumbing**: `WorldService` stores the current host snapshot
  (`RadiationLineState`), broadcasts it, sends it to one member on world
  entry/reconnect, and raises `RadiationLineStateReceived` on the guest.
  `RadiationLineStateHandler` is the thin `[PacketHandler]` adapter.
- **GameAdapter deep module**: `RadiationLineSync` is the single owner:
  - Host: publishes while the line is active at 5 Hz; publishes the
    activation/inactivation transition; snapshots the state in solo/menu for a
    later solo→lobby conversion.
  - Guest: applies the host's absolute `active`/`timeGone` to the local
    `RadiationLine` (the private field via exact-float `Traverse`).
  - Node: `WorldGenerationUpdatePatch` caps guest `layerTimeSpent` so the
    guest never calls `Activate()` on its own.
- **Rates**: 5 Hz active resend is a deliberate cost/accuracy tradeoff. The
  line moves at most ~1.5 units/s, so a guest's local continuation between
  resends stays within a small fraction of a unit even when the host and guest
  consciousness differ.

## Verification design

1. L0 (wire): `NetPacketTests.RadiationLineState_RoundTripsActiveAndTimeGone`
   locks the protobuf round-trip.
2. L0 (direction): `NetMsg.RadiationLineState` is in the host→guest list;
   `DirectionTests.EveryNetMsg_IsExplicitlyClassified` guards completeness.
3. L0 (relay): `WorldEventRelayTests.RadiationLineState_HostBroadcast_ReachesEveryGuest`
   proves the host broadcast reaches every guest with the fields intact.
4. L0 (world entry): `WorldEntrySnapshotTests.MemberEntersWorld_ReceivesCurrentRadiationLineState`
   proves `SetRadiationLineState` is handed out on the InWorld fan-out.
5. Static contract: `GameFieldContractTests` locks `RadiationLine.timeGone`
   exact `float` type; `PatchContractTests` keeps the patch inventory complete.
6. Runtime (final acceptance only): host F8 + guest join; wait for the layer
   time limit; both sides see the line activate at the same boundary, the
   host's timeGone drives both sides, and a late join receives the current
   state. Logs: `[RadiationLine]` lines on both sides.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Guest local activation | suppressed via `layerTimeSpent` cap | `WorldGenerationUpdatePatch.cs` |
| Host activation/publish | line `active` + `timeGone` broadcast | `RadiationLineSync.Update/Publish` |
| Guest apply | writes host absolute state + keeps local presentation | `RadiationLineSync.OnRadiationLineStateReceived` |
| Deactivation | host inactive state → guest `Deactivate()` | `RadiationLineSync.OnRadiationLineStateReceived` |
| Late joiner | stored snapshot in world-entry fan-out | `HandlerContext.SendWorldStateToMember` |
| Solo→lobby | state snapshot kept without a session | `RadiationLineSync.Update` → `WorldService.SetRadiationLineState` |
| Wire | NetMsg 106 host→guest | `NetMsg.cs`; `PacketReceiver.IsValidDirection`; `DirectionTests` |
| Protocol | 32→33 | `ProtocolVersion.cs` |
| Game-field contract | `RadiationLine.timeGone` exact float | `GameFieldContractTests` |
| Structure | new small owners under 600-line gate | `tools/check-architecture.ps1` |
