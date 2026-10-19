# Guest frame rate lower than host with frame drops

- Status: Done — accepted by batch `20261021-a` on the deployed `0.1.0+c9769177…`: with
  `Diagnostics:LatencyInstrumentation=true`, two 45 s windows compared each client's own `[Latency] Frame`
  lines, and the guest's update pump ran MORE often and cost LESS than the host's in both windows (guest
  69.3/75.7 calls per second, 3.97/3.68 ms average; host 63.1/69.9 calls per second, 6.96/6.24 ms average).
  The reported guest deficit did not reproduce on this artifact in that session. Record:
  `docs/evidence/acceptance/guest-frame-rate-lower-than-host-20261021-a.md`. The four landed-work rows were
  judged by batch `20261012-c` (`docs/evidence/acceptance/guest-frame-rate-lower-than-host-20261012-c.md`).
- Priority: Medium
- Category: Performance / guest-side runtime
- Source: User report (2026-09-05) — the guest's frame rate is noticeably lower than the host's and the guest experiences frame drops.

## Completed in this cycle

- Landed opt-in whole-frame latency telemetry (`[Latency] Frame`) plus the
  existing per-domain summaries, with a configurable slow/frame-drop threshold
  (`Diagnostics:SlowFrameThresholdMs`).
- Removed avoidable per-frame allocations: `RemotePlayers` is now a cached
  `IReadOnlyList` rebuilt only on entity-table mutations, and hot consumers
  iterate it by index; the guest item-follow pump no longer takes a
  `Keys.ToList()` snapshot every frame.
- Selfcheck: `docs/evidence/selfchecks/tooling/guest-frame-rate-baseline-selfcheck.md`.
- Verification: full test suite 2283 passed / 0 failed; architecture, event
  replay, and entity-event dispatch gates pass.

## Observed symptom

- Guest client runs at a clearly lower frame rate than the host.
- The guest exhibits visible frame drops.

## Investigation direction

- Baseline guest vs host frame rate and frame-time distribution to confirm the
  magnitude and whether the drop is continuous or event-driven.
- Separate CUO overhead from environment factors:
  - remote clone rendering / player stream projection on the guest;
  - guest-side item/world non-authoritative simulation;
  - network receive, deserialization, and presentation work;
  - sandbox/background window behavior if the guest is running in a sandbox.
- Look for per-frame work that is guest-only or disproportionate to the host:
  remote clone creation/update, remote UI, state-stream application, audio or
  physics on non-authoritative proxies.
- Check whether the frame drops coincide with specific events (item spawns,
  player actions, world snapshots, remote inventory) or are baseline.

## Non-goals

- Not assuming the host is the performance baseline; the goal is first to
  identify the guest-only cost and then reduce/remove the avoidable overhead.
