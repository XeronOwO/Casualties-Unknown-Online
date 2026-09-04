# State-stream bandwidth reduction

- Status: Review
- Priority: Medium
- Category: Networking observability / optimization
- Source: original backlog — Open work; measurement input landed as
  `docs/backlog/review/network-traffic-baseline.md`.

## What landed

Removed the redundant per-recipient player-state echo from the high-frequency
player stream. The host previously sent every guest a full roster list that
included that guest's own entry on every 20 Hz frame. A player already knows its
own local state, so this cycle builds one stream per recipient and omits the
recipient's own entry while still sending the host and every other member.

- `PlayerStreamExchange.BroadcastPlayerState` now writes per-recipient streams
  via `BuildPlayerStreamList(synced, target.SteamId)`.
- No wire/protocol change: same `WirePayloadType.PlayerStateStream`,
  `WireStateStream` shape, unreliable delivery and sequence gating.
- Regression test: `StateStreamBandwidthTests.HostPlayerStream_OmitsRecipientOwnState_ButKeepsOthers`
  verifies both guests never receive their own entity and still receive the host.

Selfcheck: `docs/evidence/selfchecks/protocol/state-stream-bandwidth-reduction-selfcheck.md`.

## Non-goals

- No delta encoding or field-level compression yet; this is the first
  measurement-driven, clearly-safe reduction.
- No frequency reduction: the stream cadence remains client interpolation and
  failure-recovery behavior, not just bandwidth.

## Remaining related

- `docs/backlog/todo/snapshot-size-reduction.md` is still open and remains
  measurement-first.
