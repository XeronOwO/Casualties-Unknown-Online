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
  the only place under `tools/acceptance/` where a window-message or key-state API may be named. A recipe
  file is written as UTF-8 WITHOUT a BOM (a BOM fails the client's evaluator as `(1,1): InteractiveHost`),
  and a nested lambda that captures a local fails as an internal error at the lambda's own header with no
  diagnostic — inline the value or pass it as a parameter, which the offline C# 7 gate cannot see; an
  inner parameter that shadows an outer local silently answers void, and a value a nested delegate must
  read is written as a literal inside it (a placeholder may appear once, so it cannot be substituted
  there). An eval
  ANSWER is one value with a size ceiling: a large one comes back truncated and the driver reports it as
  `answered with a truncated value` (exit 4, which reads like a protocol error while the recipe is fine), so
  a probe that must scan a whole map asks for the small projection — the shared cells, not the population
  (`acceptance/recipes/building-support-cells.cs`). A recipe that assembles its JSON BY HAND is validated
  only in the client: a stray brace surfaces as the driver's `ConvertFrom-Json` failure (`driver-error`,
  "Invalid array passed in, ',' expected"), which names a character offset in a value the batch log does
  not show — the offline gate parses the C# expression and never sees the string it builds, so the
  appended braces of every branch are counted by hand before the call and the answer is read once to
  confirm it parses. A reflected member read that answers its default is a
  defect in the READ, not a fact: `ItemInstanceId`
  exposes `Id` as a property over a private field, so a field-only read answers `0` for an id that IS
  stamped (`acceptance/recipes/container-sync-unreported.cs` reads the property first, the field as the
  fallback).
- `acceptance/session-environment.ps1` is the run's gate on the install's BepInEx trees: it classifies
  each tree by its own marker DLL (never by a folder name), refuses while a game process is running, and
  swaps only when the machine is free. The marker, process and parking names are machine facts.
- `acceptance/send-input.ps1` is the ONE sanctioned OS-level input path, and it exists by the user's own
  authorization (2026-10-24, asked for after the drag, the end-of-layer panel, the key-bind capture and the
  CPR button all stayed unreadable because "the tooling may not drive the mouse/keyboard"): it sends a
  real click, key or drag to a named window, in CLIENT coordinates, after bringing that window forward.
  The boundary it does not move: it is for an acceptance BATCH (the agent judging a ticket's rows), never a
  shipping feature's requirement, and a gesture it performs is read from the clients' own logs and probes —
  the input is the player's, the evidence is still theirs.
- Build, deploy and commit rules live in [`../AGENTS.md`](../AGENTS.md).
