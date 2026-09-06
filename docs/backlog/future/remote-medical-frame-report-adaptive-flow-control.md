# Remote medical frame-report adaptive flow control

- Status: Future
- Priority: Medium (enhancement after Stage 1 frame-level reporting)
- Category: Remote medical / network / adaptive sync
- Parent: `remote-medical-native-minigame-parity.md`

## Objective

Stage 1 now reports and broadcasts medical injection progress every frame
(operator → host per-frame deltas, host → clients per-frame authoritative
State). That maximizes real-time fidelity at the cost of message volume and
serialization work when many operators/observers are active. This future item
designs and implements dynamic flow control: measure live traffic and available
bandwidth, then adapt the medical-session report/broadcast frequency without
changing the protocol or losing the guarantee that `EndCommitted` remains the
single terminal result.

## Background / terminology

The mechanism is generally called the **report rate** or **sync frequency**
（上报率 / 同步频率）: how often a non-authoritative side sends incremental
reports to the authoritative host, and how often the host fans authoritative
state out to observers. Adaptive version often appears as **adaptive sync
frequency** or **dynamic reporting rate**.

## Scope

- Per-peer effective-bandwidth and round-trip estimation.
- Traffic/byte accounting per medical session (already exists at the transport
  observer level; extend to per-payload classification for medical messages).
- Dynamic cadence: keep per-frame reporting during healthy networks, progressively
  reduce to e.g. 30/20/10 Hz under high latency, jitter or bandwidth pressure.
- Host-side fan-out cadence may be adjusted independently of the operator's
  report cadence.
- Progress remains cumulative and loss-tolerant: the final `EndRequest.TotalMl`
  reconciles any skipped intermediate deltas, so a lower cadence never loses
  committed ml.
- Client/server must not mix cadence decisions into wire protocol version; only
  message rate changes.

## Non-goals for this item

- No anti-cheat or malicious-rate policing (separate future strict-validation item).
- No prediction/interpolation of medical effects beyond the host-authoritative
  snapshot; the display may use presentation-side interpolation if needed later.

## Acceptance direction

- Dynamic cadence reduces message volume under simulated bandwidth constraints
  while preserving final committed ml equality.
- No regression in the frame-perfect mode when no pressure is detected.
- Metrics are observable in logs/telemetry.
