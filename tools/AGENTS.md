# tools/ — deployment, contracts and helpers

- `deploy.ps1` needs an explicit `-GameDir`, refuses sandbox paths and never deploys BepInEx's own DLLs.
- The contract toolchain snapshots a game build and classifies the differences between two builds; the
  update-day flow is [`../docs/development/game-update-runbook.md`](../docs/development/game-update-runbook.md).
- Build, deploy and commit rules live in [`../AGENTS.md`](../AGENTS.md).
