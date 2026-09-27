# tools/ — deployment, contracts and helpers

- `deploy.ps1` needs an explicit `-GameDir`, refuses sandbox paths and never deploys BepInEx's own DLLs.
- The contract toolchain snapshots a game build and classifies the differences between two builds; the
  update-day flow is [`../docs/development/game-update-runbook.md`](../docs/development/game-update-runbook.md).
- `acceptance/preflight.ps1` is the read-only dependency preflight for an agent acceptance run
  ([`../docs/acceptance/dependencies.md`](../docs/acceptance/dependencies.md)): it changes nothing on the
  machine and its machine values come from the gitignored `docs/acceptance/AGENTS.local.md`.
- Build, deploy and commit rules live in [`../AGENTS.md`](../AGENTS.md).
