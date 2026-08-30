# In-Flight Pickup Queue — Self-Check (2026-08-16)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Delivery fact sheet for the pending-pickup hold window that replaces the old
immediate `UnknownItem` reject when a pickup report beats its spawn report
(backlog item "In-flight pickup reject friction").

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Old behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Pickup report, item known | host removes the table entry, transfers it to the picker, broadcasts `ItemCarriedSync` + `ItemPickup` | unchanged; extracted into `CompleteAcceptedPickup` | ItemService.PendingPickups.cs:158-176 |
| 2 | Pickup report, item unknown | immediate `ItemReject` (`UnknownItem`) and local rollback; the late spawn then registers and needs a manual re-pickup | claim is queued in `PendingPickupQueue` for 500 ms unless it is a container content, the sender's own retransmit, or an item another guest already owns | ItemService.PendingPickups.cs:28-63 |
| 3 | Obvious first-writer conflict | unknown pickup also rejected immediately (same branch as the in-flight case) | still rejected immediately via `ItemArbitration.IsTransferredToAnyGuest` — the queue is only for a registration still in flight | ItemArbitration.cs:51-59; ItemService.PendingPickups.cs:46-53 |
| 4 | Spawn registration | register + relay (`ItemSpawn`), then the later pickup report transfers | after `ItemSpawned` fires, the first queued claim for the id settles through the same transfer path; later claims lose with `ItemReject`; the settled winner is excluded from the spawn relay (it already has the local item) | ItemService.PendingPickups.cs:65-107 |
| 5 | Drop registration | register + relay (`ItemDrop`) | same settlement edge as spawn; the queued winner is excluded from the drop relay, then `ItemDropped` fires before the transfer (drop fact then pickup fact) | ItemService.PendingPickups.cs:109-155 |
| 6 | Container content race | `IsContainedInEntry` accepts content pickups silently when the container is already in the table | after every spawn/drop registration, queued claims that are now contents of the registered container are accepted silently; the container transfer carries them | ItemService.PendingPickups.cs:201-208 |
| 7 | Unconfirmed claim | — | `PendingPickupPump` (ICuoService) expires every claim after the 500 ms hold and sends exactly one late `ItemReject`; a claim whose item registered through a non-settling path still transfers at expiry | PendingPickupPump.cs:14-35; ItemService.PendingPickups.cs:178-199 |
| 8 | Duplicate queued report | — | same sender + same item already queued is silent (retransmission family) | PendingPickupQueue.cs:32-41 |
| 9 | Lifecycle | item table/transfer state reset on session end and new layer | the pending queue resets with both (`ResetItems`, `ResetSessionState`) | ItemService.cs:422-435; ItemService.PendingPickups.cs |
| 10 | Wire format | `ItemPickup` / `ItemSpawn` / `ItemDrop` / `ItemReject` | no message changed, no field added — host-only timing change | Protocol/NetMsg.cs; ProtocolVersion unchanged |
| 11 | Test transport | a handler's no-delay send re-entered `FlushDue` and delivered a later-due frame in the middle of the handler (A,C,B instead of A,B,C) | `FakeNetwork` skips nested flushes; the outer flush still drains everything due in order — the production poll-batch shape | FakeNetwork.cs:95-157; TransportTests.cs:103-124 |

## Design

- **Hold window: 500 ms.** The pickup report and its registration both ride the
  reliable Steam channel in the normal case; 500 ms covers the observed
  reorder/delay window while staying shorter than the 1 Hz snapshot fallback,
  so an unconfirmed claim rolls back within one snapshot period instead of
  waiting forever.
- **Queue is pure state** (`PendingPickupQueue`): no sends, no logging, no
  clock — `ItemService` owns the integration and `PendingPickupPump` owns the
  time edge. The host never blocks the picker: the guest's local-compute
  pickup stays applied until a late reject, exactly like the old path.
- **Settlement is the normal transfer.** A queued claim that a spawn/drop
  registration confirms goes through `CheckAndTransferToGuest` /
  `PublishCarriedSync` / `ItemPickup` — the same accept-with-correction path
  as a pickup that arrived after the registration. No new wire message, no
  protocol bump.
- **First-writer-wins holds inside the queue.** The earliest queued claim for
  an id settles; every later queued claim for that id is rejected at
  settlement.
- **Container-content exception is preserved and strengthened.** Contents
  claimed while the container's own spawn/drop is still in flight now resolve
  silently when that registration lands, instead of rejecting after the hold.

## Verification design

1. L0 pure: `PendingPickupQueueTests` (duplicate enqueue, first-writer order,
   predicate extraction, bounded expiry, reset).
2. L0 simulations over the real wire path:
   - `ItemRaceTests.SpawnPickupInflight_PickupArrivesFirst_SettlesWhenTheSpawnLands`
     and `..._SpawnArrivesAfterTheHold_RejectedThenSpawnRegisters`;
   - the existing two-guest races keep immediate conflict rejects;
   - the jittered random-lifecycle oracle now models the queue, settlement and
     per-pump expiry and compares the final table + reject stream.
3. Replay fossils: `pickup-spawn-inflight.replay` (spawn lands inside the hold
   → transfer, no reject) and `pickup-spawn-inflight-timeout.replay` (spawn
   lands after the hold → exactly one late reject, then idempotent spawn
   registration).
4. Test-harness contract: `TransportTests.HandlerSends_DoNotReenterTheFlushingBatch`
   locks the poll-batch shape the queue reasoning depends on.
5. Runtime dual-side pass (future/optional): with a 300 ms guest→host delay on
   the item channel, pick up a freshly spawned item and observe no
   `ItemReject`; with a > 500 ms delay, observe exactly one reject and the
   item back in the world. Logs: `Item pickup ... queued` →
   `Item ... picked up by ... transferred + relayed` or `rejected after the
   500 ms hold`.
6. Assertion-validity proof: removing `PendingPickupPump` registration or the
   queue reset turns the new simulations red; restored to green.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Unknown pickup | queue instead of immediate reject | ItemService.PendingPickups.cs:28-63 |
| Conflict detection | transfer-to-any guard keeps races immediate | ItemArbitration.cs:51-59 |
| Spawn settlement | registration → `ItemSpawned` → first queued transfer + loser rejects | ItemService.PendingPickups.cs:65-107 |
| Drop settlement | registration → `ItemDropped` → first queued transfer + loser rejects | ItemService.PendingPickups.cs:109-155 |
| Accepted transfer | extracted single completion path | ItemService.PendingPickups.cs:158-176 |
| Hold expiry | per-frame pump, one late reject | PendingPickupPump.cs:14-35; ItemService.PendingPickups.cs:178-199 |
| Container contents | post-registration silent resolution | ItemService.PendingPickups.cs:201-208 |
| Queue purity | duplicate / order / expiry / reset | PendingPickupQueue.cs |
| Lifecycle reset | `ResetItems` + `ResetSessionState` | ItemService.cs:422-435 |
| DI registration | `PendingPickupPump` is an ICuoService | CuoBootstrap.cs:148-149 |
| Test transport | no re-entrant flush | FakeNetwork.cs:95-157; TransportTests.cs |
| Wire compatibility | no protocol bump | NetMsg.cs / ProtocolVersion.cs unchanged |
| Replay fossils | settle-inside-hold + timeout | tests/.../Replays/*.replay |
| Structure | all touched files under the 600-line gate | tools/check-architecture.ps1 |
