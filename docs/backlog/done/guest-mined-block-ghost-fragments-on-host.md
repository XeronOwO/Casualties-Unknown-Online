# Guest-mined block leaves ghost fragments on host

- Status: Done
- Type: Bug
- Category: World / block sync
- Source: user report 2026-08-31

Fixed: direct `SetBlock(0)` paths now clear the game's stale `BlockDamage`
entry/sprite. Self-check:
`docs/evidence/selfchecks/world/air-write-block-damage-cleanup-selfcheck.md`.
