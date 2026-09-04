# Ragdoll limb pose not synced remotely

- Status: Done
- Type: Bug
- Category: Players / presentation
- Source: user report 2026-08-31

Fixed: the 20 Hz player state stream now carries each limb's **world-space**
position and z rotation while the owner is ragdoll/dead/unconscious; the frozen
remote clone writes those exact world transforms instead of using only the
generic lay-down clip or parent-relative local offsets. World-space is required:
the visible limb transforms are not reliably centered on the Body transform,
and local offsets left the clone upright with its lower half underground.
Self-check: `docs/evidence/selfchecks/players/ragdoll-limb-pose-sync-selfcheck.md`.
