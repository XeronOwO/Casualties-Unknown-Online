# Remote ragdoll state not visible

- Status: Done
- Type: Bug
- Category: Players / presentation
- Source: user report 2026-08-31

Pressing X enters ragdoll locally; from the other player's view, the character's center position is synced, but the character remains upright/standing and does not enter the soft/collapsed ragdoll state.

Fixed and accepted by the user. The render proxy now presents as standing to
`HandleVisuals` so the lying clip drives the visible limb transforms, with
`RenderProxyPoseTests` covering the rule. A newer, separate issue records the
remaining limb-pose mismatch: `docs/backlog/todo/ragdoll-limb-pose-not-synced.md`.
