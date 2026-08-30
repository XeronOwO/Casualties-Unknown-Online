# Remote ragdoll state not visible

- Status: Review
- Type: Bug
- Category: Players / presentation
- Source: user report 2026-08-31

Pressing X enters ragdoll locally; from the other player's view, the character's center position is synced, but the character remains upright/standing and does not enter the soft/collapsed ragdoll state.

Fix implemented: the frozen render proxy now presents as standing during
`HandleVisuals` so the lying clip drives the visible limb transforms; see
`docs/evidence/selfchecks/players/ragdoll-render-proxy-limb-pose-selfcheck.md`.
Needs runtime acceptance.
