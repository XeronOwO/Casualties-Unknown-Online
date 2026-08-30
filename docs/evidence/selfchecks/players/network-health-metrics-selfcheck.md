# Network health metrics — RTT history / jitter / probe loss self-check

Owner cycle: backlog "Network health metrics". Decision: implement the missing
health-specific counts as pure per-peer diagnostics fed by the existing
ping/pong loop and surfaced in the periodic log; do **not** change protocol,
transport, or any gameplay/bandwidth behaviour.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Ping/pong loop | `SessionService.RequestPing` sends `PingMsg` every 5 s (and on F7); `PongHandler` -> `ISessionControl.RecordPong` computes RTT from the echoed tick |
| 2 | Existing RTT | `SessionService.LastRttMs` + `MemberPresence.RttMs` already carried the latest sample only; no history/jitter/loss existed |
| 3 | Whole-protocol traffic monitor | `NetworkTrafficMonitor` / `NetworkTrafficTracker` already counted per-peer send/receive bytes and frames over 10 s windows and `NetworkTrafficWindowLog` printed them |
| 4 | Steam reliable channel | The probe is reliable, so an unanswered ping is a peer-reachability signal (broken/lost session), not raw packet loss; the metric is named/tracked as **probe loss**, not transport-level packet loss |
| 5 | Measurement-first rule | Backlog explicitly says no bandwidth optimization before data; this change only adds data, it makes no batching/size decision |

## 2. Design

- New pure `PeerHealthTracker` (one file, one top-level type) owns per-peer
  rolling RTT samples (max 16), average RTT, jitter (absolute difference of the
  last two samples), completed/lost probe counters, and the derived loss
  percentage.
- `NetworkTrafficMonitor` now owns one `PeerHealthTracker` (state belongs to
  the observer, not to the session), exposes `RecordPingSent` / `RecordPong`
  to `SessionService`, and logs `[NetworkHealth] peer=... rtt=... avg=...
  jitter=... loss=...` on the same 10 s window edge as `[NetworkTraffic]`.
- `SessionService` calls `RecordPingSent` immediately before each regular
  ping send and `RecordPong` in `RecordPong`; `TeardownSession` resets the
  whole monitor so stale peers do not leak into the next lobby.
- No protocol change, no new `NetMsg`, no direction-table change,
  `ProtocolVersion` stays 31.
- The existing `[NetworkTraffic]` log already carries per-peer bandwidth; the
  new `[NetworkHealth]` log carries latency/jitter/loss, so the backlog's
  "Online UI or logs" surface is satisfied by logs.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Ping send | Record probe sent before send | `SessionService.RequestPing` → `NetworkTrafficMonitor.RecordPingSent` |
| Pong receive | Record RTT sample / close probe | `SessionService.RecordPong` → `NetworkTrafficMonitor.RecordPong` |
| RTT history | Rolling 16 sample average + last RTT | `PeerHealthTracker` + `PeerHealthTrackerTests` |
| Jitter | Absolute difference between last two samples | `PeerHealthTrackerTests.RecordPingAndPong_UpdatesRttAverageAndJitter` |
| Probe loss | An unanswered probe is counted as lost when the next probe goes out; late/duplicate pongs do not double-count | `PeerHealthTrackerTests.UnansweredPing_IsCountedAsLossOnTheNextProbe`, `LateDuplicatePong_DoesNotCreateASecondSample` |
| Log surface | `[NetworkHealth]` on the 10 s window edge; bandwidth remains in `[NetworkTraffic]` | `NetworkTrafficMonitor.Update` |
| Session boundaries | `TeardownSession` resets traffic + health | `SessionService.TeardownSession` |
| Integration | A real host→guest ping produces a health snapshot | `PacketTrafficMonitorTests.RequestPing_RecordsPeerHealthSnapshot` |
| No wire change | None | No `NetMsg`/protocol edits; `ProtocolVersion.cs` unchanged |

## 4. Verification

- **L0 unit**: `PeerHealthTrackerTests` (5 tests) covers sample/jitter,
  probe-loss edge, late-pong matching, duplicate/late pong,
  ordering/reset.
- **Integration**: `PacketTrafficMonitorTests.RequestPing_RecordsPeerHealthSnapshot`
  drives the production session stack over `FakeNetwork` and asserts the host
  observes a completed peer-health record for the guest.
- **Code gates**: `dotnet build`, `dotnet test`, `dotnet format`,
  check-architecture / check-event-replay / check-entity-event-dispatch.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
