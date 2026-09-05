# Guest frame rate lower than host with frame drops

- Status: Todo
- Priority: Medium
- Category: Performance / guest-side runtime
- Source: User report (2026-09-05) — the guest's frame rate is noticeably lower than the host's and the guest experiences frame drops.

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
