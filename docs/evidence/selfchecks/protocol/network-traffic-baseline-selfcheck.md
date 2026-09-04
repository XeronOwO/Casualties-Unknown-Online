# Network traffic baseline — per-payload percentiles and regression gate

Owner cycle: backlog `docs/backlog/todo/network-traffic-baseline.md` (moved to
Review after this cycle). The existing whole-protocol monitor already counted
actual frames/bytes by `NetMsg` and peer; this cycle adds the missing semantic
dimension (`WirePayloadType`) and a regression scenario that measures the
frame-size distributions the optimization tickets asked for before optimizing.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Send semantic classification | `PacketSender` already sees the decoded payload object; when it is a `ProtocolFrame`, `ProtocolFrameTrafficClassifier` reads the envelope `Header.PayloadType` with no protobuf re-encode/decode. |
| 2 | Receive semantic classification | `KernelEnvelopeHandler` is the single transport entry point for the four-envelope protocol and already has the decoded `ProtocolFrame`; `PacketHandlerBase.CurrentFrameLength` gives the exact wire frame byte count without changing the handler contract. |
| 3 | Size distribution | `NetworkTrafficTracker.PayloadAccumulator` keeps a byte-size histogram per `WirePayloadType` and computes nearest-rank P50/P95/min/max in the immutable window snapshot. |
| 4 | Per-window payload summary | `NetworkTrafficWindow` exposes `SendByPayloadType` / `ReceiveByPayloadType`; `NetworkTrafficWindowLog` prints the top payload families with bytes, frequency, P50 and P95. |
| 5 | Checkpoint lab measurements | `NetworkTrafficBaselineTests.CheckpointBaseline_RecordsChunkCountSizeAndRestoreTime` splits a 600-item checkpoint, encodes every chunk over `NetPacket`, sums wire bytes, reassembles, and restores into a fresh `GameStateKernel`. |
| 6 | Live regression scenario | `NetworkTrafficBaselineTests.LivePair_RecordsPerPeerBytesAndPerPayloadStats` runs a real host/guest pair with 20 Hz player streams and asserts per-peer bytes plus `PlayerStateStream` send/receive P50/P95 are visible. |
| 7 | Wire / protocol | No new `NetMsg`, no `WirePayloadType` change, no protocol version bump. The monitor remains observability-only. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ProtocolFrameTrafficClassifier` | New internal static classifier: `WirePayloadType?` from one decoded `ProtocolFrame` envelope. |
| `PacketSender` | Classifies `KernelEnvelope` payloads before reporting send traffic; non-kernel frames stay raw `NetMsg`-only. |
| `PacketHandlerBase` | Adds `CurrentFrameLength` for the duration of `Handle` (observability-only, reset in `finally`). |
| `KernelEnvelopeHandler` | Reports receive-per-payload to `NetworkTrafficMonitor` after decode and before the kernel processes the frame. |
| `NetworkTrafficMonitor` | New `RecordReceivePayload`; `RecordSend` accepts optional `WirePayloadType`. |
| `NetworkTrafficTracker` | New per-payload accumulators with byte-size histograms and percentile computation. |
| `NetworkTrafficWindow` | New `PayloadTraffic` record and send/receive payload maps. |
| `NetworkTrafficWindowLog` | Prints top payload families with P50/P95. |
| `NetworkTrafficBaselineTests` | New regression suite: pure percentile math, live kernel-path observation, checkpoint split/restore metrics. |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Send payload classification | `PacketSender` classifies `ProtocolFrame` before `RecordSend` | `LivePair_RecordsPerPeerBytesAndPerPayloadStats` (host `SendByPayloadType[PlayerStateStream]`) |
| Receive payload classification | `KernelEnvelopeHandler` records from decoded frame + `CurrentFrameLength` | `LivePair_RecordsPerPeerBytesAndPerPayloadStats` (guest `ReceiveByPayloadType[PlayerStateStream]`) |
| Percentile math | Tracker `PayloadAccumulator` histograms and nearest-rank percentiles | `PayloadTracker_ComputesP50P95FrequencyPerPayloadType` |
| Raw counter compatibility | Existing `NetMsg`/peer counters unchanged | existing `NetworkTrafficTrackerTests` / `PacketTrafficMonitorTests` still green |
| No double protobuf decode | Receive stats come from `KernelEnvelopeHandler`, not `PacketReceiver` | code review; `PacketReceiver` path unchanged |
| Checkpoint baseline | Split/encode/assemble/restore measurements | `CheckpointBaseline_RecordsChunkCountSizeAndRestoreTime` |
| Observability-only | No batching/rate-limit/bandwidth decision | monitor/ tracker have no decision path; optimizations remain future tickets |

## 4. Verification design (development-period, no manual acceptance)

- L0: pure tracker test locks P50/P95/min/max and payload frequency.
- L1: real session stack (`TestNode` + `FakeNetwork`) proves the kernel receive
  path reports by payload type and per-peer bytes are observed on both sides.
- L2: checkpoint lab records chunk count, total encoded bytes, and restore elapsed.
- Static evidence: no wire/protocol change; monitor remains a one-way sink.
- Runtime verification box: **L0/L1/L2 simulation + static evidence, no manual
  acceptance** (user rule).

## 5. Verification results (2026-09-05)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2233 passed / 0 failed / 0 skipped |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `check-delivery.ps1` | pass (7 boxes checked) |

## 6. What was NOT changed (and why)

- No bandwidth optimization: this cycle only adds the baseline measurements and
  regression gate; choosing delta/keyframe/compression still requires the data
  this scenario now produces.
- No duplicate protobuf decoding in `PacketReceiver`; semantic payload stats are
  reported from the already-decoded kernel handler, not by re-parsing raw frames.
- No new wire format or protocol version; the per-payload statistics are a local
  observability extension only.
