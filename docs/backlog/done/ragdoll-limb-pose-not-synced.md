# Ragdoll limb pose not synced remotely

- Status: Done
- Type: Bug
- Category: Players / presentation
- Source: user report 2026-08-31

Fixed: the 20 Hz player state stream now carries each limb's local position and
z rotation while the owner is ragdoll/dead/unconscious; the frozen remote clone
writes those exact poses instead of using only the generic lay-down clip.
Self-check: `docs/evidence/selfchecks/players/ragdoll-limb-pose-sync-selfcheck.md`.
