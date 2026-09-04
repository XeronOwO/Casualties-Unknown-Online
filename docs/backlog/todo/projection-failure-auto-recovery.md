# Projection failure auto-recovery

- Status: Todo
- Priority: High
- Category: Architecture / reliability
- Source: Loomi architecture review (2026-09-04)

Kernel commits must not roll back because a downstream Unity projection failed; that
principle is correct. Today a projection exception leaves the authoritative state
correct while the Unity scene / remote clone can be stale, and there is no generic
dirty marking or rebuild loop.

Goal: add a lightweight `ProjectionHealthCoordinator` (or equivalent):

- Track the last successfully applied revision per projection.
- Mark the affected domain dirty when a projection throws.
- Rebuild the dirty domain from the kernel read model at a main-thread safe point.
- Escalate to a degraded/diagnostic state after repeated failures.
- Prefer per-domain rebuild over full checkpoint replay.

Acceptance: simulated projection failure produces observable dirty marking and
automatic rebuild; repeated failures surface diagnostics; existing kernel and
projection tests remain green.
