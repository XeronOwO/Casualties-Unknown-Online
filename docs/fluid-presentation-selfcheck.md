# Guest-side fluid water sound / push / slip sync

Date: 2026-08-20
ProtocolVersion: 25 (new `FluidPresentationMsg`, NetMsg 96)

## Problem

CUO's fluid domain (#129) is host-authoritative: the host simulates the world
fluid grid alone (`FluidSimulationAuthority`) and streams absolute RLE regions
to each guest (`FluidRegionMsg`). The guest never runs
`FluidManager.SimulationStep`, so it gets the **grid** but not the **transient
effects** the game's simulation produces while water moves:

- `FluidManager.IncrMove` (FluidManager.cs:232-254) creates 0.75 s
  `WaterPusher` objects that push the local body and drive `liquidSlipTime` /
  `liquidRagdollBar` — without them a guest standing in moving water is never
  pushed/slipped/ragdolled by the current.
- `FluidManager.SimulationStep` (FluidManager.cs:411) plays `waterflow1..3`
  sounds as water falls — a guest hears no water-flow ambience.

This was the recorded "guest-side fluid water sound/push/slip gaps" backlog
item.

## Change

The host now sends a dedicated reliable `FluidPresentationMsg` (NetMsg 96)
whenever its simulation produces one of these transient effects inside a
guest's viewport:

- `KindWaterPush`: sent on the exact `WaterPusher` cadence the game uses
  (`waterMoveCount > 10`, FluidManager.cs:238-253). The message carries the
  cell and the flow direction (`Vector2.down/right/left`).
- `KindWaterflowSound`: sent on the exact `tileCooldown > 16` cadence
  (FluidManager.cs:406-413). The host already consumed `Random.Range(1,4)`
  for the clip suffix, so the message carries the chosen `SoundIndex` and the
  receiver plays the exact clip without consuming random again.

The guest-side `FluidPresentationApplication` replays these:

- Water push: creates the same `WaterPusher` GameObject
  (`CircleCollider2D`, radius 1.5, `Object.Destroy(go, 0.75f)`) at the cell —
  mirroring `FluidManager.IncrMove` (FluidManager.cs:242-252). The guest's own
  body then gets the native push/slip/ragdoll behavior.
- Waterflow sound: plays `Sound.Play("waterflow" + index, ...)` with the same
  arguments as the host.

The authoritative grid path is untouched: `FluidRegionMsg` remains the only
writer of `FluidManager.fluid` on the guest, and the guest never simulates.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `FluidManager.SimulationStep` waterflow sound | Host replicates the cadence and sends the exact chosen clip suffix | `FluidSimulationAuthority.SimulateBand`; FluidManager.cs:406-413 |
| `FluidManager.IncrMove` WaterPusher creation | Host replicates the cadence and sends the direction; guest mirrors the GameObject creation | `FluidSimulationAuthority.SendWaterPushIfDue`; FluidManager.cs:232-254; `FluidPresentationApplication.SpawnWaterPusher` |
| `FluidRegionMsg` authoritative grid | Unchanged — the guest still only applies streamed absolute regions | `FluidRegionApplication.Apply` |
| New wire message | `FluidPresentationMsg` (NetMsg 96) host→guest, reliable | `NetMsg.cs`, `FluidPresentationHandler`, `PacketReceiver.IsValidDirection` |
| Protocol compatibility | v24 peers cannot receive the new events — version bumped to 25 | `ProtocolVersion.cs` |
| Guest replay safety | Replays run inside `RemoteApply`, so `SoundPlayPatch` cannot echo them as character sounds | `FluidPresentationApplication.Apply`, `CallContext` |
| Message volume | Only guests whose viewport contains the cell receive the event; the cadence is the game's own (not per-cell) | `FluidSimulationAuthority.SendPresentation` |

## Why this is safe

- The host remains the only fluid authority; the guest still never runs
  `SimulationStep`.
- The guest-side replay only creates transient presentation/physics objects
  that the game itself would create while simulating — it never writes
  `FluidManager.fluid`.
- Water push is a local-body effect: the host cannot push a guest's rigidbody
  directly, so the exact `WaterPusher` event is the correct host-authoritative
  trigger.
- Reliable delivery is used because a lost push would be a missed local-body
  effect that the next grid snapshot cannot heal (the grid is not the push).
- The sound index is chosen host-side and carried verbatim, so no additional
  random-stream consumption happens on the receiver.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- L0 wire/plumbing tests:
  - `DirectionTests` classifies `FluidPresentation` as host→guest only.
  - `EntityEventSimulationTests.FluidPresentation_HostSendsToGuest_ReplaySurfaceFires`
    drives the real channel: host sends, guest's `FluidPresentationReceived`
    fires with the exact fields.
- Reflective adapter surface tests:
  - `FluidPresentationContractTests.FluidPresentationApplication_Exists_AndHasApply`
    — the guest replay class and its `Apply(FluidPresentationMsg)` surface exist.
  - `FluidPresentationContractTests.FluidPresentationApplication_HasWaterPusherSpawner`
    — the WaterPusher spawner mirrors the game type's `direction` field.
  - `FluidPresentationContractTests.FluidSimulationAuthority_KeepsPushCadenceHelper`
    — the host push-cadence helper exists with the expected surface.
- Gates: `check-architecture`, `check-event-replay`, `check-entity-event-dispatch`
  all pass (no dispatch-table change).
- Full suite: 1003 tests green.
- Development-period rule: L0/static evidence; **no manual acceptance**
  (user 2026-08-16 mandate).
