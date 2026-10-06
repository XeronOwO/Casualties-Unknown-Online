# tools/ — deployment, contracts and helpers

- `deploy.ps1` needs an explicit `-GameDir`, refuses sandbox paths and never deploys BepInEx's own DLLs.
- The contract toolchain snapshots a game build and classifies the differences between two builds; the
  update-day flow is [`../docs/development/game-update-runbook.md`](../docs/development/game-update-runbook.md).
- `acceptance/preflight.ps1` is the read-only dependency preflight for an agent acceptance run
  ([`../docs/acceptance/dependencies.md`](../docs/acceptance/dependencies.md)): it changes nothing on the
  machine and its machine values come from the gitignored `docs/acceptance/AGENTS.local.md`.
- `acceptance/drive-in-process.ps1` drives a running client's Online UI through the in-process evaluator
  of an acceptance run: real control ids on the UI's own registered actions, never OS-level input. Its
  `-Action recipe` runs one committed scenario recipe from `acceptance/recipes/` for the gameplay states
  the UI vocabulary cannot reach — carry relations, forced body states, movement windows, a key the
  game's own gesture reads — one eval per invocation, with the declared `-RecipeArg` values substituted
  before anything is sent. Its `-Action declare` loads one committed eval declaration from
  `acceptance/driver/eval-declarations/` into a client (idempotent: a presence probe decides whether it is
  sent at all), which is how a recipe reaches a capability the evaluator has to be given before it can run
  — `recipes/key-hold.cs` holds a key the game's own gesture reads through the `window-key` declaration,
  the only place under `tools/acceptance/` where a window-message or key-state API may be named.
- `acceptance/session-environment.ps1` is the run's gate on the install's BepInEx trees: it classifies
  each tree by its own marker DLL (never by a folder name), refuses while a game process is running, and
  swaps only when the machine is free. The marker, process and parking names are machine facts.
- Build, deploy and commit rules live in [`../AGENTS.md`](../AGENTS.md).
