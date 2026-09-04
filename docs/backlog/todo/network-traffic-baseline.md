# Network traffic baseline and regression gate

- Status: Todo
- Priority: Medium
- Category: Networking observability / performance
- Source: Loomi architecture review (2026-09-04)

The existing state-stream bandwidth and snapshot size tickets are measurement-first,
but no concrete baseline scenario or regression gate is defined yet.

Goal: add a benchmark/lab scenario that records:

- Sent and received bytes per player per second.
- P50/P95 frame size and frequency by `PayloadType`.
- Checkpoint size, chunk count, and restore time.

Use that data before choosing delta/keyframe, dirty-field bitsets, area-of-interest,
batching, checkpoint compression, or adaptive frequency. Add the benchmark to the
regression suite so traffic changes are visible.

Non-goal: adding complex compression or a generic replication framework without
measured data. Related tickets:
`docs/backlog/todo/state-stream-bandwidth-reduction.md`,
`docs/backlog/todo/snapshot-size-reduction.md`.
