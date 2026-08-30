# Remote ragdoll state not visible

- Status: In Progress
- Type: Bug
- Category: Players / presentation
- Source: user report 2026-08-31

Pressing X enters ragdoll locally; from the other player's view, the character's center position is synced, but the character remains upright/standing and does not enter the soft/collapsed ragdoll state.

Previous review rejection: the fix was made without first adding a regression
test that fails on the current code (test-first workflow in AGENTS.md). The
ticket is now being reworked with `RenderProxyPoseTests` plus the helper
extracted from `BodyPatches`.
