# Duplicate unsynced item drops (guest-dug tree and world-spawned items)

- Status: Done
- Type: Bug
- Category: Items / world sync
- Source: user report 2026-08-31

Fixed by same-id materialization dedup: the originator's own committed-batch
echo no longer instantiates a second scene object beside the local original.
Self-check:
`docs/evidence/selfchecks/items/remote-world-item-same-id-dedup-selfcheck.md`.
Runtime acceptance remains the user's final step.
