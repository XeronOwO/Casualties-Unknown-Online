# Projection failure auto-recovery

- Status: Review
- Priority: High
- Category: Architecture / reliability
- Source: Loomi architecture review (2026-09-04)

Landed the lightweight `ProjectionHealthCoordinator` per-domain dirty/rebuild
loop. A projection exception is captured, the domain is marked dirty, the last
successful revision is tracked, and the affected domain is rebuilt from the
kernel read model on the main-thread pump; repeated failures escalate to a
degraded/diagnostic state. First production adoption: items, fluids, and
world-entities.

Selfcheck: `docs/evidence/selfchecks/architecture/projection-health-coordinator-selfcheck.md`.
