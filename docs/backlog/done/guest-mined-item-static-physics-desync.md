# Guest-mined item static-physics desync

- Status: Done
- Type: Bug
- Category: Items / world sync
- Source: user report 2026-08-31

Fixed with the same-id materialization dedup: the guest's local origin item is
the single scene object for its instance id, so the host position stream can
drive it into local physics and the settled/static state. Self-check:
`docs/evidence/selfchecks/items/remote-world-item-same-id-dedup-selfcheck.md`.
Runtime acceptance remains the user's final step.
