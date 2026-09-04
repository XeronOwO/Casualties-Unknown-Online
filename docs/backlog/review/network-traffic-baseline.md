# Network traffic baseline and regression gate

- Status: Review
- Priority: Medium
- Category: Networking observability / performance
- Source: Loomi architecture review (2026-09-04)

Landed a traffic-baseline regression suite plus per-`WirePayloadType` frame-size
statistics in the existing whole-protocol monitor.

What landed:

- `PacketSender` classifies `KernelEnvelope` frames and records send traffic by
  `WirePayloadType`; `KernelEnvelopeHandler` records receive traffic by payload
  type from the already-decoded frame and its exact wire length.
- `NetworkTrafficTracker`/`NetworkTrafficWindow` now expose per-payload count,
  bytes, P50/P95, min/max; periodic logs print top payload families.
- `NetworkTrafficBaselineTests`:
  - locks P50/P95/frequency math in the pure tracker,
  - runs a live host/guest pair and verifies per-peer bytes and `PlayerStateStream`
    send/receive payload statistics,
  - measures a 600-item checkpoint's chunk count, total encoded bytes, and
    restore time.

Selfcheck: `docs/evidence/selfchecks/protocol/network-traffic-baseline-selfcheck.md`.

Verification: `dotnet build` clean, `dotnet format` clean, 2233 tests green,
architecture/event-replay/entity-event/delivery gates pass.

Non-goal: no bandwidth optimization yet. The measured baseline is the input for
`docs/backlog/todo/state-stream-bandwidth-reduction.md` and
`docs/backlog/todo/snapshot-size-reduction.md`.
