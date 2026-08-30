# Whole-protocol network traffic monitor — mechanism inventory and self-check

Owner cycle: backlog "General network traffic monitor" (Networking
observability / optimization). Decision: implement an observability-only
whole-protocol monitor at the data-plane boundary. `PacketSender` reports every
actual transport frame it attempts (one per recipient) with its byte length and
transport verdict; `PacketReceiver` reports every received frame. The monitor
rolls 10-second windows and logs per-`NetMsg` send/receive byte counts plus
per-peer totals. No batching, no rate-limit, no bandwidth decision is made from
these numbers yet.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Send boundary | `PacketSender` is the single send primitive used by every domain; `TrySend` and `SendToAll` are the only paths that call `INetworkTransport.SendTo`. |
| 2 | Receive boundary | `PacketReceiver.OnTransportMessage` is the single receive boundary from `INetworkTransport.MessageReceived`; it validates direction before dispatch. |
| 3 | Frame shape | `NetPacket.Encode` produces `[msgId:1][protobuf payload]`; `frame.Length` is the wire byte count. |
| 4 | Transport verdict | `PacketSender` already distinguishes send success/failure through `TrySend`; `SendToAll` previously ignored the return value, now records it too. |
| 5 | Existing send-failure diagnostics | `SteamTransport.LogSendDiagnostics` + `SteamSendFailureClassifier` still own the transport-level failure-family log; the monitor adds counts/bytes for failed sends but does not replace classification. |
| 6 | Existing item traffic | `ItemTrafficTracker` remains the item-domain logical-operation counter; the new monitor is separate because it counts actual transport frames and bytes. |
| 7 | Time edge | `NetworkTrafficMonitor` implements `ICuoService` and rolls/logs windows on the Unity main thread, same pattern as `ItemTrafficPump`. |
| 8 | Wire | No new `NetMsg`, no packet format change. `ProtocolVersion` stays 29. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `NetworkTrafficTracker` | New pure counter: per-message (send/receive), per-peer, and totals in bytes/frames; failed sends counted separately. |
| `NetworkTrafficWindow` | New immutable window snapshot with `SendByMessage`, `ReceiveByMessage`, `ByPeer`, totals and failed counts. |
| `NetworkTrafficMonitor` | New `ICuoService` owner of the rolling window and periodic `[NetworkTraffic]` log. |
| `NetworkTrafficWindowLog` | New formatter for the periodic log (send/recv/fail totals, per-peer, top messages by bytes). |
| `PacketSender` | Added `NetworkTrafficMonitor` dependency; records each `TrySend` / `SendToAll` recipient frame. |
| `PacketReceiver` | Added `NetworkTrafficMonitor` dependency; records each received frame before direction/drop logic. |
| `CuoBootstrap` | Registered `NetworkTrafficMonitor` as singleton + `ICuoService`. |
| `ItemTrafficTracker` | Unchanged (item-domain logical operation counter). |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Per-message send bytes | `RecordSend` accumulates `NetMsg` → count/bytes | `NetworkTrafficTrackerTests.RecordSendAndReceive_AccumulateTotalsPerMessageAndPeer` |
| Per-message receive bytes | `RecordReceive` accumulates `NetMsg` → count/bytes | same test |
| Per-peer totals | `RecordSend`/`RecordReceive` update a per-peer accumulator | same test |
| Failed sends | A false transport verdict counts as failed bytes/frames, not delivered traffic | same test; `PacketTrafficMonitorTests.PacketSender_RecordsFailedSendAsTraffic` |
| Window roll | `TryCollectWindow` rolls at the window boundary and resets once | `NetworkTrafficTrackerTests.TryCollectWindow_RollsAndResetsWithoutDoubleCounting` |
| Snapshot no-reset | `Snapshot()` never mutates the window | `NetworkTrafficTrackerTests.Snapshot_DoesNotResetTheWindow` |
| Constructor guard | Non-positive windows rejected | `NetworkTrafficTrackerTests.Constructor_RejectsNonPositiveWindow` |
| Send boundary | Real `PacketSender` path records send | `PacketTrafficMonitorTests.RequestPing_RecordsSendOnHostAndReceiveOnGuest` |
| Receive boundary | Real `PacketReceiver` path records receive | `PacketTrafficMonitorTests.RequestPing_RecordsSendOnHostAndReceiveOnGuest` |
| Fan-out sends | `SendToAll` records one frame per recipient, not one logical op | `PacketTrafficMonitorTests.PacketSender_SendToAll_RecordsOneFramePerRecipient` |
| Observability-only | No batching/rate-limit, no traffic shaping | code review: monitor has no decision path; `ItemTrafficTracker` unchanged |
| No wire/protocol regression | No new `NetMsg`; no packet change | full suite green; `ProtocolVersion` unchanged |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real session stack (`TestNode`) verifies the full
  `PacketSender`/`PacketReceiver` path: a real ping/pong round trip appears as
  a send on one side and a receive on the other.
- Pure tracker tests cover per-message/per-peer aggregation, failed-send
  accounting, window roll/reset, and constructor guard.
- Static evidence: the monitor is a one-way sink on the data plane; no
  domain/handler/routing behavior changed.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1124 passed / 0 failed (7 new network-traffic tests) |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| `check-delivery.ps1` | pass (checked boxes tracked in `../delivery-checklist.md`) |
| No manual acceptance | per development-period rule |

## 6. What was NOT changed (and why)

- No `Network health metrics` claim in this cycle: per-peer packet loss /
  jitter were still unmeasured here; only bandwidth/RTT-adjacent counts and
  per-peer window logs were provided by this monitor. That backlog item was
  closed later by `docs/evidence/selfchecks/players/network-health-metrics-selfcheck.md`.
- No protocol/version bump: this is pure diagnostics, no wire behavior change.
- No bandwidth optimization: the monitor exists precisely to measure before
  optimizing; state-stream and snapshot reduction remain explicitly deferred.
- No removal of `SteamSendFailureClassifier`: transport-level failure families
  remain logged at the point where Steam gives the reason.
